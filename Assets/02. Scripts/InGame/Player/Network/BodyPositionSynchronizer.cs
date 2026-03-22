using Core;
using InGame.Player.Animation;
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
        [SerializeField] private PlayerAnimation _animation;

        private PhotonTransformView _transformView;
        private bool _syncEnabled;
        private Vector3 _correctionTarget;
        private Quaternion _correctionRotation;
        private bool _hasCorrection;

        private const float SnapThreshold = 2.0f;
        private const float CorrectionFactor = 0.15f;
        private const float InterpolationFactor = 0.3f;
        private const float VerticalVelocityThreshold = 0.5f;

        private void Awake()
        {
            _transformView = GetComponent<PhotonTransformView>();
        }

        private void Start()
        {
            if (_ragdollController != null)
            {
                _ragdollController.OnRecoveryDataReady += HandleRecoveryDataReady;
                _ragdollController.OnGuestSettled += HandleGuestSettled;
                _ragdollController.SetRecoveryAuthority(photonView.IsMine);
            }

            if (_movement != null)
            {
                _movement.OnJumped += HandleJumped;
                _movement.OnDived += HandleDived;
                _movement.OnDiveLanded += HandleDiveLanded;
            }
        }

        private void OnDestroy()
        {
            if (_ragdollController != null)
            {
                _ragdollController.OnRecoveryDataReady -= HandleRecoveryDataReady;
                _ragdollController.OnGuestSettled -= HandleGuestSettled;
            }

            if (_movement != null)
            {
                _movement.OnJumped -= HandleJumped;
                _movement.OnDived -= HandleDived;
                _movement.OnDiveLanded -= HandleDiveLanded;
            }
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

        private void HandleGuestSettled()
        {
            if (photonView.IsMine) return;
            photonView.RPC(nameof(RpcGuestSettled), RpcTarget.Others);
        }

        [PunRPC]
        private void RpcGuestSettled()
        {
            if (!gameObject.activeInHierarchy) return;
            _ragdollController.OnRemoteSettled();
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
                    _rootBody.MovePosition(_correctionTarget);
                    _rootBody.MoveRotation(_correctionRotation);
                }
                else if (dist > 0.01f)
                {
                    if (_rootBody.isKinematic)
                    {
                        // Host-authoritative 합체 모드: MovePosition으로 부드러운 보간.
                        _rootBody.MovePosition(
                            Vector3.Lerp(_rootBody.position, _correctionTarget, InterpolationFactor));
                        _rootBody.MoveRotation(
                            Quaternion.Slerp(_rootBody.rotation, _correctionRotation, InterpolationFactor));
                    }
                    else
                    {
                        // 분리 모드: 로컬 물리와 공존하는 보정.
                        bool skipVerticalCorrection = (_movement != null && !_movement.Grounded)
                            || Mathf.Abs(_rootBody.linearVelocity.y) > VerticalVelocityThreshold;
                        if (skipVerticalCorrection)
                        {
                            Vector3 corrected = Vector3.Lerp(
                                _rootBody.position, _correctionTarget, CorrectionFactor);
                            corrected.y = _rootBody.position.y;
                            _rootBody.position = corrected;
                        }
                        else
                        {
                            _rootBody.position = Vector3.Lerp(
                                _rootBody.position, _correctionTarget, CorrectionFactor);
                        }
                        _rootBody.rotation = Quaternion.Slerp(
                            _rootBody.rotation, _correctionRotation, CorrectionFactor);
                    }
                }
            }
        }

        #region Animation RPC (Host-authoritative 합체 모드)

        private void HandleJumped()
        {
            if (!photonView.IsMine) return;
            photonView.RPC(nameof(RpcAnimJump), RpcTarget.Others);
        }

        private void HandleDived()
        {
            if (!photonView.IsMine) return;
            photonView.RPC(nameof(RpcAnimDive), RpcTarget.Others);
        }

        private void HandleDiveLanded(bool active)
        {
            if (!photonView.IsMine) return;
            photonView.RPC(nameof(RpcAnimLand), RpcTarget.Others, active);
        }

        [PunRPC]
        private void RpcAnimJump()
        {
            if (_animation != null) _animation.Jump();
        }

        [PunRPC]
        private void RpcAnimDive()
        {
            if (_animation != null) _animation.Dive();
        }

        [PunRPC]
        private void RpcAnimLand(bool active)
        {
            if (_animation != null) _animation.Land(active);
        }

        #endregion

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
