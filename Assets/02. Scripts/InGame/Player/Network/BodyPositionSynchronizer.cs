using Core;
using InGame.Player.Movement;
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
    /// 회복 시 Owner의 root pos/rot/faceUp을 RPC로 전파하여 결과 동기화.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrderConstants.BodyPositionSynchronizer)]
    public class BodyPositionSynchronizer : MonoBehaviourPun, IPunObservable
    {
        private PhotonTransformView _transformView;
        private RagdollController _ragdollController;
        private PlayerMovement _movement;

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
            _ragdollController = GetComponent<RagdollController>();
            _movement = GetComponent<PlayerMovement>();
            _rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            if (_ragdollController != null)
                _ragdollController.OnRecoveryDataReady += HandleRecoveryDataReady;
        }

        private void OnDestroy()
        {
            if (_ragdollController != null)
                _ragdollController.OnRecoveryDataReady -= HandleRecoveryDataReady;
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
            if (_ragdollController == null) return;

            bool isActive = _ragdollController.IsRagdollActive;

            if (_transformView != null)
                _transformView.enabled = !_syncEnabled && !isActive;
        }

        private void HandleRecoveryDataReady(Vector3 rootPos, Quaternion rootRot, bool isFaceUp)
        {
            if (!photonView.IsMine) return;
            photonView.RPC(nameof(RpcSyncRecovery), RpcTarget.Others, rootPos, rootRot, isFaceUp);
        }

        [PunRPC]
        private void RpcSyncRecovery(Vector3 rootPos, Quaternion rootRot, bool isFaceUp)
        {
            if (!gameObject.activeInHierarchy) return;
            _ragdollController.ApplyRemoteRecovery(rootPos, rootRot, isFaceUp);
        }

        private void FixedUpdate()
        {
            if (_ragdollController != null && _ragdollController.IsRagdollActive) return;

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
                    bool airborne = _movement != null && !_movement.Grounded;
                    if (airborne)
                    {
                        // 공중에서는 XZ만 보정, Y축은 로컬 물리 유지
                        Vector3 corrected = Vector3.Lerp(_rb.position, _correctionTarget, CorrectionFactor);
                        corrected.y = _rb.position.y;
                        _rb.position = corrected;
                    }
                    else
                    {
                        _rb.position = Vector3.Lerp(_rb.position, _correctionTarget, CorrectionFactor);
                    }
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
                if (_ragdollController != null && _ragdollController.IsRagdollActive) return;
                _correctionTarget = pos;
                _correctionRotation = rot;
                _hasCorrection = true;
            }
        }
    }
}
