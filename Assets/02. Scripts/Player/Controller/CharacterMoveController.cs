using Player.Ragdoll;
using UnityEngine;

namespace Player.Controller
{
    public class CharacterMoveController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Rigidbody _capsuleRb;
        [SerializeField] private RagdollController _ragdollController;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _accelerationTime = 0.15f;
        [SerializeField] private float _decelerationTime = 0.1f;
        [SerializeField] private float _rotationSpeed = 10f;

        [Header("Jump")]
        [SerializeField] private float _jumpForce = 10f;
        [SerializeField] private float _groundCheckRadius = 0.3f;
        [SerializeField] private Vector3 _groundCheckOffset = new Vector3(0f, 0.1f, 0f);
        [SerializeField] private LayerMask _groundLayer;

        [Header("Dive")]
        [SerializeField] private float _diveForce = 8f;
        [SerializeField] private float _diveCooldown = 0.5f;

        private Transform _cameraTransform;
        private Vector3 _currentVelocity;
        private ERagdollState _previousRagdollState;
        private float _lastGroundedTime;
        private float _lastDiveTime = -Mathf.Infinity;
        private bool _jumpRequested;
        private bool _diveRequested;
        private Vector3 _inputDirection;

        private const float CoyoteTime = 0.1f;
        private static readonly int s_speedHash = Animator.StringToHash("Speed");

        private void Start()
        {
            _previousRagdollState = ERagdollState.Animated;
        }

        private void Update()
        {
            ERagdollState currentRagdollState = _ragdollController.CurrentState;

            // 래그돌 복귀 감지 → 속도 초기화.
            if (_previousRagdollState != ERagdollState.Animated && currentRagdollState == ERagdollState.Animated)
            {
                _currentVelocity = Vector3.zero;
                UpdateAnimator();
            }

            _previousRagdollState = currentRagdollState;

            if (currentRagdollState != ERagdollState.Animated)
                return;

            if (IsGrounded())
                _lastGroundedTime = Time.time;

            Accelerate(_inputDirection * _moveSpeed);
            RotateToVelocity();
            UpdateAnimator();

            if (_jumpRequested)
            {
                TryJump();
                _jumpRequested = false;
            }

            if (_diveRequested)
            {
                TryDive();
                _diveRequested = false;
            }
        }

        private void FixedUpdate()
        {
            if (_ragdollController.CurrentState != ERagdollState.Animated)
                return;

            Vector3 velocity = _capsuleRb.linearVelocity;
            velocity.x = _currentVelocity.x;
            velocity.z = _currentVelocity.z;
            _capsuleRb.linearVelocity = velocity;
        }

        public void ApplyInput(Vector2 move, bool jump, bool dive)
        {
            _inputDirection = GetCameraRelativeDirection(move);
            _jumpRequested |= jump;
            _diveRequested |= dive;
        }

        public void SetCameraTransform(Transform cameraTransform)
        {
            _cameraTransform = cameraTransform;
        }

        public void SetPhysicsActive(bool active)
        {
            _capsuleRb.isKinematic = !active;
            enabled = active;

            if (!active)
            {
                _currentVelocity = Vector3.zero;
                _jumpRequested = false;
                _diveRequested = false;
            }
        }

        public Vector3 GetVelocity()
        {
            return _capsuleRb.linearVelocity;
        }

        public void SetVelocity(Vector3 velocity)
        {
            _capsuleRb.linearVelocity = velocity;
            _currentVelocity = new Vector3(velocity.x, 0f, velocity.z);
        }

        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            float h = input.x;
            float v = input.y;

            if (_cameraTransform == null)
            {
                var worldDir = new Vector3(h, 0f, v);
                return worldDir.sqrMagnitude > 1f ? worldDir.normalized : worldDir;
            }

            Vector3 camForward = _cameraTransform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = _cameraTransform.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector3 direction = camRight * h + camForward * v;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
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
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(_currentVelocity),
                    Time.deltaTime * _rotationSpeed);
            }
        }

        private void UpdateAnimator()
        {
            if (_animator != null)
                _animator.SetFloat(s_speedHash, _currentVelocity.magnitude);
        }

        private bool IsGrounded()
        {
            Vector3 origin = transform.position + _groundCheckOffset;
            return Physics.CheckSphere(origin, _groundCheckRadius, _groundLayer);
        }

        private void TryJump()
        {
            bool canJump = Time.time - _lastGroundedTime <= CoyoteTime;
            if (!canJump)
                return;

            // 기존 수직 속도 제거 후 점프 임펄스 적용.
            Vector3 velocity = _capsuleRb.linearVelocity;
            velocity.y = 0f;
            _capsuleRb.linearVelocity = velocity;

            _capsuleRb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);

            // 코요테 타임 소비.
            _lastGroundedTime = -Mathf.Infinity;
        }

        private void TryDive()
        {
            if (Time.time - _lastDiveTime < _diveCooldown)
                return;

            Vector3 diveDirection = transform.forward + Vector3.down * 0.2f;
            _capsuleRb.AddForce(diveDirection.normalized * _diveForce, ForceMode.Impulse);
            _lastDiveTime = Time.time;
        }
    }
}
