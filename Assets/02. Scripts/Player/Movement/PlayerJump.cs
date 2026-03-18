using Player.Animation;
using UnityEngine;

namespace Player.Movement
{
    public class PlayerJump : MonoBehaviour
    {
        [Header("Jump")]
        [SerializeField] private float _jumpForce = 10f;

        [Header("Dive")]
        [SerializeField] private float _diveForce = 8f;
        [SerializeField] private float _diveCooldown = 0.5f;

        private PlayerAnimation _anim;
        private Rigidbody _rb;
        private float _lastDiveTime = -Mathf.Infinity;
        private bool _isDiving;

        public bool IsDiving => _isDiving;

        public void ClearDiving()
        {
            _isDiving = false;
        }

        private void Awake()
        {
            _anim = GetComponent<PlayerAnimation>();
            _rb = GetComponent<Rigidbody>();
        }

        public void Jump()
        {
            // 기존 수직 속도 제거 후 점프 임펄스 적용.
            Vector3 velocity = _rb.linearVelocity;
            velocity.y = 0f;
            _rb.linearVelocity = velocity;

            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _anim.TriggerJump();
        }

        public bool TryDive()
        {
            if (Time.time - _lastDiveTime < _diveCooldown)
                return false;

            Vector3 diveDirection = transform.forward + Vector3.down * 0.2f;
            _rb.AddForce(diveDirection.normalized * _diveForce, ForceMode.Impulse);
            _lastDiveTime = Time.time;
            _isDiving = true;
            _anim.TriggerDive();

            return true;
        }

        public void ResetState()
        {
            _anim.ResetToLocomotion();
            _isDiving = false;
        }
    }
}
