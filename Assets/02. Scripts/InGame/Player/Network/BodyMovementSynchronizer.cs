using Core;
using InGame.Player.Animation;
using InGame.Player.Movement;
using InGame.Player.Ragdoll;
using InGame.Race.Platform;
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
        private LaunchController _launchController;

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

        // 플랫폼 원형 외삽용.
        private Vector3 _platformAngularVelocity;
        private Vector3 _platformPivot;

        private const float SnapThreshold = 5f;
        private const float InterpolationFactor = 0.3f;
        private const float SmoothTime = 0.08f;
        private const float MaxExtrapolationTime = 0.2f;
        private const float VelocityBlendFactor = 0.5f;
        private const float GroundClampRayOriginOffset = 0.5f;

        private void Awake()
        {
            _transformView = GetComponent<PhotonTransformView>();
            _ragdollController = GetComponent<RagdollStateMachine>();
            _movement = GetComponent<PlayerMovement>();
            _animation = GetComponent<PlayerAnimation>();
            _launchController = GetComponent<LaunchController>();
        }

        private void Start()
        {
            if (_movement != null)
            {
                _movement.OnJumped += HandleJumped;
                _movement.OnDived += HandleDived;
            }

            if (_launchController != null)
                _launchController.OnLaunched += HandleLaunched;
        }

        private void OnDestroy()
        {
            if (_movement != null)
            {
                _movement.OnJumped -= HandleJumped;
                _movement.OnDived -= HandleDived;
            }

            if (_launchController != null)
                _launchController.OnLaunched -= HandleLaunched;
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
                // 외삽 타깃을 snap 판정 전에 계산: 낙하 중 stale _correctionTarget 비교로 인한 역방향 snap 방지.
                Vector3 target;
                if (!_firstSnapshot)
                {
                    float elapsed = Mathf.Min(
                        Mathf.Abs((float)(PhotonNetwork.Time - _lastReceiveServerTime)),
                        MaxExtrapolationTime);

                    if (_platformAngularVelocity.sqrMagnitude > 1e-6f)
                    {
                        // 원형 외삽: 회전 발판 위에서 원호 궤적을 따라 예측.
                        float angle = _platformAngularVelocity.magnitude * elapsed * Mathf.Rad2Deg;
                        Quaternion rot = Quaternion.AngleAxis(angle, _platformAngularVelocity.normalized);
                        Vector3 offset = _networkPosition - _platformPivot;
                        target = rot * offset + _platformPivot;

                        // 플레이어 자체 이동분만 선형으로 추가.
                        Vector3 tangentialVel = Vector3.Cross(_platformAngularVelocity, offset);
                        target += (_networkVelocity - tangentialVel) * elapsed;
                    }
                    else
                    {
                        target = _networkPosition + _networkVelocity * elapsed;
                    }

                    if (!_networkGrounded)
                        target += 0.5f * Physics.gravity * (elapsed * elapsed);
                }
                else
                {
                    target = _correctionTarget;
                }

                // Remote의 kinematic 바디는 콜라이더가 위치를 보정해주지 않으므로,
                // 보간/외삽이 지면 아래로 관통하는 것을 raycast로 방지.
                if (_movement != null && _movement.GroundLayer.value != 0)
                {
                    float radius = _movement.GroundCheckRadius;
                    float rayStartY = Mathf.Max(target.y, _networkPosition.y) + GroundClampRayOriginOffset;
                    var rayOrigin = new Vector3(target.x, rayStartY, target.z);
                    float rayDistance = rayStartY - target.y + radius;
                    if (Physics.SphereCast(rayOrigin, radius, Vector3.down, out RaycastHit hit,
                            rayDistance, _movement.GroundLayer))
                    {
                        target.y = Mathf.Max(target.y, hit.point.y);
                    }
                }

                float dist = Vector3.Distance(_rootBody.position, target);
                if (dist > SnapThreshold)
                {
                    _rootBody.MovePosition(target);
                    _rootBody.MoveRotation(_correctionRotation);
                    // 스냅 후 관성을 네트워크 속도로 유지: 0으로 리셋하면 SmoothDamp가
                    // 정지 상태에서 재시작 → target에 뒤처짐 → 재스냅 → 떨림 유발.
                    _smoothVelocity = _networkVelocity;
                }
                else if (dist > 0.01f)
                {
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

        private void HandleLaunched()
        {
            if (!photonView.IsMine || !_syncEnabled) return;
            photonView.RPC(nameof(RpcAnimTrampolineLaunch), RpcTarget.Others);
        }

        [PunRPC]
        private void RpcAnimTrampolineLaunch()
        {
            if (_animation != null) _animation.TrampolineLaunch();
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

                PlatformCarrier.TryGetMotionForRider(_rootBody, out Vector3 angVel, out Vector3 pivot);
                stream.SendNext(angVel);
                stream.SendNext(pivot);
            }
            else
            {
                Vector3 pos = (Vector3)stream.ReceiveNext();
                Quaternion rot = (Quaternion)stream.ReceiveNext();
                float speed = (float)stream.ReceiveNext();
                bool grounded = (bool)stream.ReceiveNext();
                Vector3 velocity = (Vector3)stream.ReceiveNext();
                Vector3 angVel = (Vector3)stream.ReceiveNext();
                Vector3 pivot = (Vector3)stream.ReceiveNext();

                if (_ragdollController != null && _ragdollController.IsRootManagedByRagdoll) return;

                // 공중 → 착지 전환 시 낙하 관성으로 인한 지면 관통 방지.
                if (grounded && !_networkGrounded)
                    _smoothVelocity = Vector3.zero;

                _platformAngularVelocity = angVel;
                _platformPivot = pivot;

                _networkPosition = pos;
                _networkVelocity = _firstSnapshot
                    ? velocity
                    : Vector3.Lerp(_networkVelocity, velocity, VelocityBlendFactor);
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
