using InGame.Player.Animation;
using UnityEngine;

namespace InGame.Player.Movement
{
    [RequireComponent(typeof(PlayerAnimation))]
    public class PlayerJump : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody _rootBody;

        private PlayerAnimation _anim;

        [Header("Jump")]
        [SerializeField] private float _jumpForce = 10f;

        [Header("Dive")]
        [SerializeField] private float _diveForce = 8f;
        [SerializeField] private float _diveCooldown = 0.5f;

        private void Awake()
        {
            _anim = GetComponent<PlayerAnimation>();
        }

        private float _lastDiveTime = -Mathf.Infinity;
        private bool _isDiving;

        public bool IsDiving => _isDiving;

        public void ClearDiving()
        {
            _isDiving = false;
        }

        public void Jump()
        {
            // 기존 수직 속도 제거 후 점프 임펄스 적용.
            Vector3 velocity = _rootBody.linearVelocity;
            velocity.y = 0f;
            _rootBody.linearVelocity = velocity;

            _rootBody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _anim.Jump();
        }

        public bool TryDive(Vector3 inputDirection)
        {
            if (_isDiving)
                return false;

            if (Time.time - _lastDiveTime < _diveCooldown)
                return false;

            Vector3 forward = inputDirection.sqrMagnitude > 0.01f
                ? inputDirection.normalized
                : _rootBody.transform.forward;

            _rootBody.rotation = Quaternion.LookRotation(forward);

            Vector3 diveDirection = forward + Vector3.down * 0.2f;
            _rootBody.AddForce(diveDirection.normalized * _diveForce, ForceMode.Impulse);
            _lastDiveTime = Time.time;
            _isDiving = true;
            _anim.Dive();

            return true;
        }
    }
}
