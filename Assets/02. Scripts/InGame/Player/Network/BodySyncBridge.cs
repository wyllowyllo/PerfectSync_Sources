using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    /// <summary>
    /// 바디 동기화 브릿지.
    /// IPunObservable로 소유자 위치를 권위적으로 보정.
    /// 양쪽 모두 물리 시뮬레이션을 실행하되, 비소유자가 소유자 위치로 부드럽게 보정된다.
    /// PhotonTransformView는 syncEnabled 시 비활성화됨.
    /// 래그돌 활성 중에는 보정을 중단한다.
    /// </summary>
    [DefaultExecutionOrder(100)] // PlayerMovement(0) 이후 실행
    public class BodySyncBridge : MonoBehaviourPun, IPunObservable
    {
        private PhotonTransformView _transformView;
        private IRagdoll _ragdoll;
        private bool _wasRagdollActive;

        // 위치 보정
        private bool _syncEnabled;
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
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// 동기화 활성화 설정. true이면 PhotonTransformView를 비활성화하고
        /// IPunObservable을 통한 위치 보정으로 전환한다.
        /// </summary>
        public void SetSyncEnabled(bool enabled)
        {
            _syncEnabled = enabled;
            if (_transformView != null)
                _transformView.enabled = !enabled;
            _hasCorrection = false;
        }

        private void LateUpdate()
        {
            if (_ragdoll == null) return;

            bool isActive = _ragdoll.IsRagdollActive;

            // PhotonTransformView가 있을 때만 활성/비활성 제어
            // (MergedPlayer에는 없으므로 null 체크 필요)
            if (_transformView != null)
                _transformView.enabled = !_syncEnabled && !isActive;

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
            if (_ragdoll != null && _ragdoll.IsRagdollActive) return;

            if (!photonView.IsMine && _syncEnabled && _hasCorrection)
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
            if (!_syncEnabled) return;

            if (stream.IsWriting)
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);
            }
            else
            {
                Vector3 pos = (Vector3)stream.ReceiveNext();
                Quaternion rot = (Quaternion)stream.ReceiveNext();
                if (_ragdoll != null && _ragdoll.IsRagdollActive) return;
                _correctionTarget = pos;
                _correctionRotation = rot;
                _hasCorrection = true;
            }
        }

        [PunRPC]
        private void RpcSyncPostRagdoll(Vector3 pos, Quaternion rot)
        {
            transform.position = pos;
            transform.rotation = rot;
        }
    }
}
