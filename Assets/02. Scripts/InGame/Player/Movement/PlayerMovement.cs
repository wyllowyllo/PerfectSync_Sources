using System;
using Core;
using InGame.Player.Animation;
using InGame.Player.Ragdoll;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Player.Movement
{
    [DefaultExecutionOrder(ExecutionOrderConstants.PlayerMovement)]
    [RequireComponent(typeof(RagdollStateMachine))]
    [RequireComponent(typeof(PlayerJump))]
    [RequireComponent(typeof(PlayerAnimation))]
    public class PlayerMovement : MonoBehaviour, IControllableBody
    {
        [Header("References")]
        [SerializeField] private Rigidbody _rootBody;
        [SerializeField] private Transform _groundCheckPoint;
        [FormerlySerializedAs("_physicsProfile")] [SerializeField] private Ragdoll.CharacterSpeedLimits speedLimits;

        private RagdollStateMachine _ragdollController;
        private PlayerJump _playerJump;
        private PlayerAnimation _anim;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _accelerationTime = 0.15f;
        [SerializeField] private float _decelerationTime = 0.1f;
        [SerializeField] private float _rotationSpeed = 10f;

        [Header("Air Control")]
        [SerializeField, Range(0f, 1f)] private float _airControlFactor = 0.6f;
        private float _airControlBoost = 1f;
        private float _buffSpeedMultiplier = 1f;

        [Header("Gravity")]
        [SerializeField] private float _gravity = 9.81f;
        private float _maxBodySpeed;

        [Header("Ground Check")]
        [SerializeField] private float _groundCheckRadius = 0.3f;
        [SerializeField] private LayerMask _groundLayer;

        private Vector3 _currentVelocity;
        private ERagdollState _previousRagdollState;
        private float _lastGroundedTime;
        private float _jumpBufferTime = float.NegativeInfinity;
        private float _groundLockoutUntil = float.NegativeInfinity;
        private bool _isGrounded;
        private Vector3 _inputDirection;
        private float _momentumBlend;

        private const float CoyoteTime = 0.15f;
        private const float JumpBufferDuration = 0.15f;
        private const float GroundLockoutDuration = 0.15f;

        // Host-authoritative 합체 모드: 애니메이션 트리거 동기화용.
        public event Action OnJumped;
        public event Action OnDived;
        public event Action OnLanded;

        public Vector3 Velocity
        {
            get => _rootBody.linearVelocity;
            set
            {
                _rootBody.linearVelocity = value;
                _currentVelocity = new Vector3(value.x, 0f, value.z);
            }
        }
        public Transform BodyTransform => _rootBody.transform;
        public bool IsRagdollActive => _ragdollController.IsRagdollActive;
        public bool Grounded => _isGrounded;
        public float Gravity => _gravity;
        public float MomentumBlend { get => _momentumBlend; set => _momentumBlend = value; }
        public float AirControlBoost { get => _airControlBoost; set => _airControlBoost = value; }
        public float BuffSpeedMultiplier { get => _buffSpeedMultiplier; set => _buffSpeedMultiplier = value; }
        public float CurrentSpeed => _currentVelocity.magnitude;
        public float GroundCheckRadius => _groundCheckRadius;
        public LayerMask GroundLayer => _groundLayer;

        private void Awake()
        {
            _ragdollController = GetComponent<RagdollStateMachine>();
            _playerJump = GetComponent<PlayerJump>();
            _anim = GetComponent<PlayerAnimation>();
            _rootBody.constraints = RigidbodyConstraints.FreezeRotation;

            _maxBodySpeed = speedLimits != null ? speedLimits.MaxBodySpeed : 50f;
        }

        private void Start()
        {
            _previousRagdollState = ERagdollState.Animated;
        }

        private void Update()
        {
            ERagdollState currentRagdollState = _ragdollController.CurrentState;

            // 풀 래그돌에서 복귀 시에만 속도 초기화 (Stumble 복귀는 속도 유지).
            if ((_previousRagdollState == ERagdollState.BlendToAnim || _previousRagdollState == ERagdollState.Ragdolled)
                && currentRagdollState == ERagdollState.Animated)
            {
                _currentVelocity = Vector3.zero;
            }

            _previousRagdollState = currentRagdollState;

            // 풀 래그돌/Dead 중에는 이동 불가.
            if (currentRagdollState != ERagdollState.Animated)
                return;

            // 발사 중(블렌드 1.0)에는 입력/점프 처리를 건너뛰고 에어본 애니메이션만 갱신.
            if (_momentumBlend >= 1f)
            {
                _isGrounded = false;
                _anim.Locomotion(false, 0f);
                _jumpBufferTime = float.NegativeInfinity;
                return;
            }

            bool wasGrounded = _isGrounded;
            if (Time.time < _groundLockoutUntil)
            {
                // 점프 직후 Impulse가 콜라이더를 지면 스피어 밖으로 밀어낼 때까지 판정 보류.
                // 이 시간 동안 wasGrounded=false → IsGrounded()=true 전이가 잡혀 Land 이벤트가 오발되는 것을 차단.
                _isGrounded = false;
            }
            else
            {
                _isGrounded = IsGrounded();
                if (_isGrounded)
                {
                    _lastGroundedTime = Time.time;
                    _airControlBoost = 1f;

                    if (!wasGrounded)
                        OnLanded?.Invoke();
                }
            }

            // 다이브 중 착지 → 이동 속도 초기화.
            if (_playerJump.IsDiving && _isGrounded)
            {
                _playerJump.ClearDiving();
                _currentVelocity = Vector3.zero;
            }

            // 다이브 중에는 입력 가속을 적용하지 않음.
            if (!_playerJump.IsDiving)
            {
                float speedMultiplier = (_isGrounded ? 1f : _airControlFactor * _airControlBoost) * _buffSpeedMultiplier;
                Accelerate(_inputDirection * _moveSpeed * speedMultiplier);
            }

            // 애니메이션 파라미터 갱신.
            float speed = _playerJump.IsDiving
                ? new Vector3(_rootBody.linearVelocity.x, 0f, _rootBody.linearVelocity.z).magnitude
                : _currentVelocity.magnitude;
            _anim.Locomotion(_isGrounded, speed);

            bool hasBufferedJump = Time.time - _jumpBufferTime <= JumpBufferDuration;
            if (hasBufferedJump)
            {
                bool canJump = Time.time - _lastGroundedTime <= CoyoteTime;
                if (canJump && _anim.CanAcceptJump())
                {
                    _playerJump.Jump();
                    OnJumped?.Invoke();
                    _jumpBufferTime = float.NegativeInfinity;

                    // 이륙 직후 콜라이더가 지면 스피어 내에 남아있는 동안 판정을 보류하여
                    // (1) 같은 착지에서의 2단 점프와 (2) 다음 프레임의 가짜 OnLanded 오발을 동시에 차단.
                    _groundLockoutUntil = Time.time + GroundLockoutDuration;
                    _isGrounded = false;
                    _lastGroundedTime = float.NegativeInfinity;
                }
                else if (!canJump
                         && _anim.CanAcceptDive()
                         && InGameManager.IsLocalPlayerControllable
                         && _playerJump.TryDive(_inputDirection))
                {
                    _currentVelocity = Vector3.zero;
                    OnDived?.Invoke();
                    _jumpBufferTime = float.NegativeInfinity;
                }
            }
        }

        private void FixedUpdate()
        {
            if (_rootBody.isKinematic) return;

            if (_ragdollController.CurrentState != ERagdollState.Animated)
                return;

            Vector3 velocity = _rootBody.linearVelocity;

            // 다이브 중에는 물리 임펄스가 수평 속도를 제어하도록 덮어쓰지 않음.
            if (!_playerJump.IsDiving)
            {
                if (_momentumBlend > 0f)
                {
                    // 발사 모멘텀 → 입력 제어로 부드럽게 전환.
                    velocity.x = Mathf.Lerp(_currentVelocity.x, velocity.x, _momentumBlend);
                    velocity.z = Mathf.Lerp(_currentVelocity.z, velocity.z, _momentumBlend);
                }
                else
                {
                    velocity.x = _currentVelocity.x;
                    velocity.z = _currentVelocity.z;
                }
            }

            velocity.y += (-_gravity - Physics.gravity.y) * Time.fixedDeltaTime;
            velocity.y = Mathf.Clamp(velocity.y, -_maxBodySpeed, _maxBodySpeed);

            // 솔버 디페네트레이션 방어: 다이브/모멘텀 중 XZ 포함 전체 속도 제한.
            float sqrSpeed = velocity.sqrMagnitude;
            if (sqrSpeed > _maxBodySpeed * _maxBodySpeed)
                velocity *= _maxBodySpeed / Mathf.Sqrt(sqrSpeed);

            _rootBody.linearVelocity = velocity;

            if (_currentVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_currentVelocity);
                _rootBody.MoveRotation(Quaternion.Slerp(_rootBody.rotation, targetRot, Time.fixedDeltaTime * _rotationSpeed));
            }
        }

        public void ApplyInput(Vector3 worldDirection, bool jump)
        {
            _inputDirection = worldDirection;
            if (jump) _jumpBufferTime = Time.time;
        }



        private void Accelerate(Vector3 targetVelocity)
        {
            float smoothTime = targetVelocity.sqrMagnitude > _currentVelocity.sqrMagnitude
                ? _accelerationTime
                : _decelerationTime;

            if (smoothTime > 0f)
            {
                float t = 1f - Mathf.Exp(-Time.deltaTime / smoothTime);
                _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, t);
            }
            else
            {
                _currentVelocity = targetVelocity;
            }
        }

        private bool IsGrounded()
        {
            return Physics.CheckSphere(_groundCheckPoint.position, _groundCheckRadius, _groundLayer);
        }

        private void OnEnable()
        {
            if (_rootBody != null)
                _rootBody.isKinematic = false;
        }

        private void OnDisable()
        {
            if (_rootBody != null)
            {
                _rootBody.isKinematic = true;
                _currentVelocity = Vector3.zero;
                _jumpBufferTime = float.NegativeInfinity;
                _momentumBlend = 0f;
                _airControlBoost = 1f;
            }
        }
    }
}
