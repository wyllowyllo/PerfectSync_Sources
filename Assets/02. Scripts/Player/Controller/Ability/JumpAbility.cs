using UnityEngine;

namespace Player.Controller.Ability
{
    public class JumpAbility : MonoBehaviour
    {
        [Header("Jump")]
        [SerializeField] private float _jumpForce = 10f;

        [Header("Dive")]
        [SerializeField] private float _diveForce = 8f;
        [SerializeField] private float _diveCooldown = 0.5f;

        private Animator _animator;
        private Rigidbody _rb;
        private float _lastDiveTime = -Mathf.Infinity;
        private bool _isDiving;

        private static readonly int SSpeedHash = Animator.StringToHash("Speed");
        private static readonly int SIsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int SJumpHash = Animator.StringToHash("Jump");
        private static readonly int SDiveHash = Animator.StringToHash("Dive");

        private const float CoyoteTime = 0.1f;

        public bool IsDiving => _isDiving;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _rb = GetComponent<Rigidbody>();
        }

        public void Execute(bool isGrounded, float lastGroundedTime, float speed)
        {
            _animator.SetFloat(SSpeedHash, speed);
            _animator.SetBool(SIsGroundedHash, isGrounded);
        }

        public bool TryJump(float lastGroundedTime)
        {
            bool canJump = Time.time - lastGroundedTime <= CoyoteTime;
            if (!canJump)
                return false;

            // 기존 수직 속도 제거 후 점프 임펄스 적용.
            Vector3 velocity = _rb.linearVelocity;
            velocity.y = 0f;
            _rb.linearVelocity = velocity;

            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _animator.SetTrigger(SJumpHash);

            return true;
        }

        public bool TryDive()
        {
            if (Time.time - _lastDiveTime < _diveCooldown)
                return false;

            Vector3 diveDirection = transform.forward + Vector3.down * 0.2f;
            _rb.AddForce(diveDirection.normalized * _diveForce, ForceMode.Impulse);
            _lastDiveTime = Time.time;
            _isDiving = true;
            _animator.SetTrigger(SDiveHash);

            return true;
        }

        public void ResetState()
        {
            _animator.ResetTrigger(SJumpHash);
            _animator.ResetTrigger(SDiveHash);
            _animator.Play("Locomotion", 0, 0f);
            _animator.SetFloat(SSpeedHash, 0f);
            _animator.SetBool(SIsGroundedHash, true);
            _isDiving = false;
        }
    }
}
