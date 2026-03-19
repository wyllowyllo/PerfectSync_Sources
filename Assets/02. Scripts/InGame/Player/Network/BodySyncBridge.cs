using InGame.Player.Animation;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    /// <summary>
    /// 바디 동기화 브릿지.
    /// - 합체 모드: IPunObservable로 Host 위치를 권위적으로 보정.
    ///   양쪽 모두 물리 시뮬레이션을 실행하되 Guest가 Host 위치로 부드럽게 보정된다.
    ///   PhotonTransformView는 비활성화됨.
    /// - 분리 모드: PhotonTransformView 기반 위치/회전 동기화 + 트리거 RPC 릴레이.
    ///   래그돌 활성 시 transform 동기화를 중단하고, 종료 시 위치 보정 RPC를 전송한다.
    /// </summary>
    [DefaultExecutionOrder(100)] // PlayerMovement(0) 이후 실행
    public class BodySyncBridge : MonoBehaviourPun, IPunObservable
    {
        private PhotonTransformView _transformView;
        private IRagdoll _ragdoll;
        private PlayerAnimation _playerAnimation;
        private bool _wasRagdollActive;

        // 합체 모드 위치 보정
        private bool _isMergedMode;
        private Vector3 _correctionTarget;
        private Quaternion _correctionRotation;
        private bool _hasCorrection;
        private Rigidbody _rb;

        private const float SnapThreshold = 2.0f;
        private const float CorrectionFactor = 0.15f; // 매 FixedUpdate 15% 보정

        private void Awake()
        {
            _transformView = GetComponent<PhotonTransformView>();
            _ragdoll = GetComponent<IRagdoll>();
            _playerAnimation = GetComponent<PlayerAnimation>();
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// 합체 모드 설정. true이면 PhotonTransformView를 비활성화하고
        /// IPunObservable을 통한 위치 보정으로 전환한다.
        /// </summary>
        public void SetMergedMode(bool merged)
        {
            _isMergedMode = merged;
            if (_transformView != null)
                _transformView.enabled = !merged;
            _hasCorrection = false;
        }

        private void LateUpdate()
        {
            if (_ragdoll == null) return;

            bool isActive = _ragdoll.IsRagdollActive;

            // PhotonTransformView가 있을 때만 활성/비활성 제어
            // (MergedPlayer에는 없으므로 null 체크 필요)
            if (_transformView != null)
                _transformView.enabled = !_isMergedMode && !isActive;

            // 래그돌 종료 순간 감지 → 위치 강제 동기화
            if (_wasRagdollActive && !isActive && photonView.IsMine)
            {
                photonView.RPC(nameof(RpcSyncPostRagdoll), RpcTarget.Others,
                    transform.position, transform.rotation);
            }
            _wasRagdollActive = isActive;
        }

        private void FixedUpdate()
        {
            if (!photonView.IsMine && _isMergedMode && _hasCorrection)
            {
                float dist = Vector3.Distance(_rb.position, _correctionTarget);
                if (dist > SnapThreshold)
                {
                    // 큰 차이: 즉시 스냅
                    _rb.position = _correctionTarget;
                    _rb.rotation = _correctionRotation;
                }
                else if (dist > 0.01f)
                {
                    // 작은 차이: 부드럽게 위치 보정 (속도 건드리지 않음)
                    _rb.position = Vector3.Lerp(_rb.position, _correctionTarget, CorrectionFactor);
                    _rb.rotation = Quaternion.Slerp(_rb.rotation, _correctionRotation, CorrectionFactor);
                }
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (!_isMergedMode) return;

            if (stream.IsWriting) // Host
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);
            }
            else // Guest
            {
                _correctionTarget = (Vector3)stream.ReceiveNext();
                _correctionRotation = (Quaternion)stream.ReceiveNext();
                _hasCorrection = true;
            }
        }

        [PunRPC]
        private void RpcSyncPostRagdoll(Vector3 pos, Quaternion rot)
        {
            transform.position = pos;
            transform.rotation = rot;
        }

        /// <summary>
        /// Host sends trigger animation to guests via RPC.
        /// </summary>
        public void SendAnimTrigger(byte triggerId)
        {
            photonView.RPC(nameof(RpcAnimTrigger), RpcTarget.Others, triggerId);
        }

        [PunRPC]
        private void RpcAnimTrigger(byte triggerId)
        {
            if (_playerAnimation == null) return;

            switch (triggerId)
            {
                case 0: _playerAnimation.Jump(); break;
                case 1: _playerAnimation.Dive(); break;
                case 2: _playerAnimation.Land(true); break;
            }
        }
    }
}
