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
        private Vector3 _networkPosition;
        private Vector3 _networkVelocity;
        private double _lastReceiveServerTime;
        private bool _networkGrounded;
        private bool _firstSnapshot = true;
        private Vector3 _smoothVelocity;

        private const float SnapThreshold = 2.0f;
        private const float InterpolationFactor = 0.3f;
        private const float SmoothTime = 0.055f;
        private const float MaxExtrapolationTime = 0.2f;

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
            _networkPosition = Vector3.zero;
            _networkVelocity = Vector3.zero;
            _networkGrounded = false;
            _smoothVelocity = Vector3.zero;
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
                _smoothVelocity = Vector3.zero;
                return;
            }

            if (!photonView.IsMine && _syncEnabled && _hasCorrection)
            {
                float dist = Vector3.Distance(_rootBody.position, _correctionTarget);
                if (dist > SnapThreshold)
                {
                    _rootBody.MovePosition(_correctionTarget);
                    _rootBody.MoveRotation(_correctionRotation);
                    _smoothVelocity = Vector3.zero;
                }
                else if (dist > 0.01f)
                {
                    // 매 FixedUpdate마다 연속 외삽: 스냅샷 경계 타깃 점프 최소화.
                    Vector3 target;
                    if (!_firstSnapshot)
                    {
                        float elapsed = Mathf.Min(
                            Mathf.Abs((float)(PhotonNetwork.Time - _lastReceiveServerTime)),
                            MaxExtrapolationTime);
                        target = _networkPosition + _networkVelocity * elapsed;
                        // 공중일 때만 중력 가속도 반영 (포물선 예측).
                        if (!_networkGrounded)
                            target += 0.5f * Physics.gravity * (elapsed * elapsed);
                    }
                    else
                    {
                        target = _correctionTarget;
                    }

                    _rootBody.MovePosition(
                        Vector3.SmoothDamp(_rootBody.position, target,
                            ref _smoothVelocity, SmoothTime, Mathf.Infinity, Time.fixedDeltaTime));
                    _rootBody.MoveRotation(
                        Quaternion.Slerp(_rootBody.rotation, _correctionRotation, InterpolationFactor));
                }
            }
        }

        #region Animation RPC (Host-authoritative)

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

                _networkPosition = pos;
                _networkVelocity = velocity;
                _networkGrounded = grounded;
                _correctionTarget = pos;
                _correctionRotation = rot;
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
