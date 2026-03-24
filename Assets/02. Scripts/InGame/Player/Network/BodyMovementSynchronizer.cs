using Core;
using InGame.Player.Animation;
using InGame.Player.Movement;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    [DefaultExecutionOrder(ExecutionOrderConstants.BodyMovementSynchronizer)]
    [RequireComponent(typeof(RagdollStateMachine))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerAnimation))]
    public class BodyMovementSynchronizer : MonoBehaviourPun, IPunObservable
    {
        [SerializeField] private Rigidbody _rootBody;

        private RagdollStateMachine _ragdollController;
        private PlayerMovement _movement;
        private PlayerAnimation _animation;

        private PhotonTransformView _transformView;
        private bool _syncEnabled;
        private Vector3 _correctionTarget;
        private Quaternion _correctionRotation;
        private bool _hasCorrection;

        // 예측 보간 상태 (합체/kinematic 모드).
        private Vector3 _networkVelocity;
        private double _lastReceiveServerTime;
        private bool _firstSnapshot = true;

        private const float SnapThreshold = 2.0f;
        private const float CorrectionFactor = 0.15f;
        private const float InterpolationFactor = 0.3f;
        private const float PredictiveInterpolationFactor = 0.3f;
        private const float MaxExtrapolationTime = 0.2f;
        private const float VerticalVelocityThreshold = 0.5f;

        private void Awake()
        {
            _transformView = GetComponent<PhotonTransformView>();
            _ragdollController = GetComponent<RagdollStateMachine>();
            _movement = GetComponent<PlayerMovement>();
            _animation = GetComponent<PlayerAnimation>();
        }

        private void Start()
        {
            if (_movement != null)
            {
                _movement.OnJumped += HandleJumped;
                _movement.OnDived += HandleDived;
                _movement.OnDiveLanded += HandleDiveLanded;
            }
        }

        private void OnDestroy()
        {
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
            _firstSnapshot = true;
            _networkVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (_ragdollController == null) return;

            // BlendToAnim 중에도 외부 위치 보정을 억제해야 root lerp와 충돌하지 않음.
            bool isManaged = _ragdollController.IsRootManagedByRagdoll;

            if (_transformView != null)
                _transformView.enabled = !_syncEnabled && !isManaged;
        }

        private void FixedUpdate()
        {
            if (_ragdollController != null && _ragdollController.IsRootManagedByRagdoll)
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
                        // 예측 보간: _correctionTarget이 이미 네트워크 지연을 보상한 위치.
                        _rootBody.MovePosition(
                            Vector3.Lerp(_rootBody.position, _correctionTarget, PredictiveInterpolationFactor));
                        _rootBody.MoveRotation(
                            Quaternion.Slerp(_rootBody.rotation, _correctionRotation, PredictiveInterpolationFactor));
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
            if (!photonView.IsMine || !_syncEnabled) return;
            photonView.RPC(nameof(RpcAnimJump), RpcTarget.Others);
        }

        private void HandleDived()
        {
            if (!photonView.IsMine || !_syncEnabled) return;
            photonView.RPC(nameof(RpcAnimDive), RpcTarget.Others);
        }

        private void HandleDiveLanded(bool active)
        {
            if (!photonView.IsMine || !_syncEnabled) return;
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
                stream.SendNext(_movement != null ? _movement.CurrentSpeed : 0f);
                stream.SendNext(_movement != null && _movement.Grounded);
                stream.SendNext(_rootBody.linearVelocity);
            }
            else
            {
                Vector3 pos = (Vector3)stream.ReceiveNext();
                Quaternion rot = (Quaternion)stream.ReceiveNext();
                float speed = (float)stream.ReceiveNext();
                bool grounded = (bool)stream.ReceiveNext();
                Vector3 velocity = (Vector3)stream.ReceiveNext();

                if (_ragdollController != null && _ragdollController.IsRootManagedByRagdoll) return;

                // 예측 위치 계산: 수신 위치 + 속도 × 네트워크 지연.
                if (_rootBody.isKinematic && !_firstSnapshot)
                {
                    float lag = Mathf.Min(
                        Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime)),
                        MaxExtrapolationTime);
                    _correctionTarget = pos + velocity * lag;
                }
                else
                {
                    _correctionTarget = pos;
                }

                _correctionRotation = rot;
                _networkVelocity = velocity;
                _lastReceiveServerTime = info.SentServerTime;
                _hasCorrection = true;
                _firstSnapshot = false;

                // Host-authoritative: kinematic 바디에 Host의 애니메이션 파라미터 직접 적용.
                if (_rootBody.isKinematic && _animation != null)
                    _animation.Locomotion(grounded, speed);
            }
        }
    }
}
