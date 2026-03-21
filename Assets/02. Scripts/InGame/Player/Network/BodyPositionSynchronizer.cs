using Core;
using InGame.Player.Movement;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    [DefaultExecutionOrder(ExecutionOrderConstants.BodyPositionSynchronizer)]
    public class BodyPositionSynchronizer : MonoBehaviourPun, IPunObservable
    {
        [SerializeField] private Rigidbody _rootBody;
        [SerializeField] private RagdollStateMachine _ragdollController;
        [SerializeField] private PlayerMovement _movement;

        private PhotonTransformView _transformView;
        private bool _syncEnabled;
        private Vector3 _correctionTarget;
        private Quaternion _correctionRotation;
        private bool _hasCorrection;

        private const float SnapThreshold = 2.0f;
        private const float CorrectionFactor = 0.15f;

        private void Awake()
        {
            _transformView = GetComponent<PhotonTransformView>();
        }

        private void Start()
        {
            if (_ragdollController != null)
            {
                _ragdollController.OnRecoveryDataReady += HandleRecoveryDataReady;
                _ragdollController.SetRecoveryAuthority(photonView.IsMine);
            }
        }

        private void OnDestroy()
        {
            if (_ragdollController != null)
                _ragdollController.OnRecoveryDataReady -= HandleRecoveryDataReady;
        }

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

            bool isActive = _ragdollController.IsPhysicsRagdoll;

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
            _hasCorrection = false;
        }

        private void FixedUpdate()
        {
            if (_ragdollController != null && _ragdollController.IsPhysicsRagdoll)
            {
                _hasCorrection = false;
                return;
            }

            if (!photonView.IsMine && _syncEnabled && _hasCorrection)
            {
                float dist = Vector3.Distance(_rootBody.position, _correctionTarget);
                if (dist > SnapThreshold)
                {
                    _rootBody.position = _correctionTarget;
                    _rootBody.rotation = _correctionRotation;
                }
                else if (dist > 0.01f)
                {
                    bool airborne = _movement != null && !_movement.Grounded;
                    if (airborne)
                    {
                        Vector3 corrected = Vector3.Lerp(_rootBody.position, _correctionTarget, CorrectionFactor);
                        corrected.y = _rootBody.position.y;
                        _rootBody.position = corrected;
                    }
                    else
                    {
                        _rootBody.position = Vector3.Lerp(_rootBody.position, _correctionTarget, CorrectionFactor);
                    }
                    _rootBody.rotation = Quaternion.Slerp(_rootBody.rotation, _correctionRotation, CorrectionFactor);
                }
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (!_syncEnabled) return;

            if (stream.IsWriting)
            {
                stream.SendNext(_rootBody.position);
                stream.SendNext(_rootBody.rotation);
            }
            else
            {
                Vector3 pos = (Vector3)stream.ReceiveNext();
                Quaternion rot = (Quaternion)stream.ReceiveNext();
                if (_ragdollController != null && _ragdollController.IsPhysicsRagdoll) return;
                _correctionTarget = pos;
                _correctionRotation = rot;
                _hasCorrection = true;
            }
        }
    }
}
