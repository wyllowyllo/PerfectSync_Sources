using Player.Ragdoll;
using UnityEngine;

namespace Player.Controller
{
    [RequireComponent(typeof(Animator), typeof(Rigidbody), typeof(RagdollController))]
    [RequireComponent(typeof(PlayerJump), typeof(PlayerAnimation))]
    public class PlayerMovement : MonoBehaviour, IControllableBody
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _accelerationTime = 0.15f;
        [SerializeField] private float _decelerationTime = 0.1f;
        [SerializeField] private float _rotationSpeed = 10f;

        [Header("Air Control")]
        [SerializeField, Range(0f, 1f)] private float _airControlFactor = 0.6f;

        [Header("Ground Check")]
        [SerializeField] private float _groundCheckRadius = 0.3f;
        [SerializeField] private Vector3 _groundCheckOffset = new Vector3(0f, 0.1f, 0f);
        [SerializeField] private LayerMask _groundLayer;

        private Rigidbody _capsuleRb;
        private IRagdoll _ragdoll;
        private PlayerJump _playerJump;
        private PlayerAnimation anim;
        private Vector3 _currentVelocity;
        private ERagdollState _previousRagdollState;
        private float _lastGroundedTime;
        private bool _jumpRequested;
        private bool _isGrounded;
        private bool _initialized;
        private Vector3 _inputDirection;

        // 상수
        private const float CoyoteTime = 0.1f;

        public Vector3 Velocity
        {
            get => _capsuleRb.linearVelocity;
            set
            {
                _capsuleRb.linearVelocity = value;
                _currentVelocity = new Vector3(value.x, 0f, value.z);
            }
        }
        public Transform BodyTransform => transform;
        public bool IsRagdollActive => _ragdoll.IsRagdollActive;

        private void Start()
        {
            _previousRagdollState = ERagdollState.Animated;
        }

        private void Update()
        {
            ERagdollState currentRagdollState = _ragdoll.CurrentState;

            // 래그돌 복귀 감지 → 속도 초기화.
            if (_previousRagdollState != ERagdollState.Animated && currentRagdollState == ERagdollState.Animated)
            {
                _currentVelocity = Vector3.zero;
            }

            _previousRagdollState = currentRagdollState;

            if (currentRagdollState != ERagdollState.Animated)
                return;

            _isGrounded = IsGrounded();
            if (_isGrounded)
                _lastGroundedTime = Time.time;

            // 다이브 중 착지 → 래그돌 진입.
            if (_playerJump.IsDiving && _isGrounded)
            {
                _playerJump.ClearDiving();
                _ragdoll.ForceRagdoll();
                return;
            }

            // 다이브 중에는 입력 가속을 적용하지 않음.
            if (!_playerJump.IsDiving)
            {
                float speedMultiplier = _isGrounded ? 1f : _airControlFactor;
                Accelerate(_inputDirection * _moveSpeed * speedMultiplier);
                RotateToVelocity();
            }

            // 애니메이션 파라미터 갱신.
            float speed = _playerJump.IsDiving
                ? new Vector3(_capsuleRb.linearVelocity.x, 0f, _capsuleRb.linearVelocity.z).magnitude
                : _currentVelocity.magnitude;
            anim.UpdateLocomotion(_isGrounded, speed);

            if (_jumpRequested)
            {
                bool canJump = Time.time - _lastGroundedTime <= CoyoteTime;
                if (canJump)
                {
                    _playerJump.Jump();
                }
                else if (_playerJump.TryDive())
                {
                    _currentVelocity = Vector3.zero;
                }
                _jumpRequested = false;
            }
        }

        private void FixedUpdate()
        {
            if (_ragdoll.CurrentState != ERagdollState.Animated)
                return;

            // 다이브 중에는 물리 임펄스가 수평 속도를 제어하도록 덮어쓰지 않음.
            if (_playerJump.IsDiving)
                return;

            Vector3 velocity = _capsuleRb.linearVelocity;
            velocity.x = _currentVelocity.x;
            velocity.z = _currentVelocity.z;
            _capsuleRb.linearVelocity = velocity;
        }

        public void ApplyInput(Vector3 worldDirection, bool jump)
        {
            _inputDirection = worldDirection;
            _jumpRequested |= jump;
        }

        private void Accelerate(Vector3 targetVelocity)
        {
            float smoothTime = targetVelocity.sqrMagnitude > _currentVelocity.sqrMagnitude
                ? _accelerationTime
                : _decelerationTime;

            if (smoothTime > 0f)
                _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, Time.deltaTime / smoothTime);
            else
                _currentVelocity = targetVelocity;
        }

        private void RotateToVelocity()
        {
            if (_currentVelocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_currentVelocity), Time.deltaTime * _rotationSpeed);
            }
        }

        private bool IsGrounded()
        {
            Vector3 origin = transform.position + _groundCheckOffset;
            return Physics.CheckSphere(origin, _groundCheckRadius, _groundLayer);
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                _capsuleRb = GetComponent<Rigidbody>();
                _ragdoll = GetComponent<IRagdoll>();
                _playerJump = GetComponent<PlayerJump>();
                anim = GetComponent<PlayerAnimation>();
                _initialized = true;
            }

            _capsuleRb.isKinematic = false;
        }

        private void OnDisable()
        {
            if (!_initialized)
                return;

            _capsuleRb.isKinematic = true;
            _currentVelocity = Vector3.zero;
            _jumpRequested = false;
        }
    }
}
