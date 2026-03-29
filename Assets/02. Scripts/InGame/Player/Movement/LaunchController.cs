using Core;
using InGame.Player.Animation;
using InGame.Player.Ragdoll;
using UnityEngine;

namespace InGame.Player.Movement
{
    [DefaultExecutionOrder(ExecutionOrderConstants.LaunchController)]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerJump))]
    [RequireComponent(typeof(RagdollStateMachine))]
    public class LaunchController : MonoBehaviour, ILaunchable
    {
        [Header("References")]
        [SerializeField] private Rigidbody _rootBody;

        private PlayerMovement _movement;
        private PlayerJump _playerJump;
        private PlayerAnimation _anim;
        private RagdollStateMachine _ragdollStateMachine;

        private bool _isLaunching;

        public bool IsLaunching => _isLaunching;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _playerJump = GetComponent<PlayerJump>();
            _anim = GetComponent<PlayerAnimation>();
            _ragdollStateMachine = GetComponent<RagdollStateMachine>();
        }

        public void Launch(float targetHeight)
        {
            if (_isLaunching) return;

            float height = targetHeight - _rootBody.position.y;
            if (height <= 0f) return;

            // 래그돌 활성 중이면 즉시 복구.
            if (_ragdollStateMachine.IsRagdollActive)
                _ragdollStateMachine.ForceRecover();

            // 다이브 상태 정리.
            if (_playerJump.IsDiving)
                _playerJump.ClearDiving();

            // v₀ = √(2gh) — 목표 높이에 정확히 도달하는 초기 속도.
            float v0 = Mathf.Sqrt(2f * _movement.Gravity * height);
            _rootBody.linearVelocity = new Vector3(0f, v0, 0f);

            _movement.LockXZ = true;
            _anim.Jump();
            _isLaunching = true;
        }

        private void FixedUpdate()
        {
            if (!_isLaunching) return;

            // 발사 중 래그돌 활성 → 비상 해제.
            if (_ragdollStateMachine.IsRagdollActive)
            {
                CompleteLaunch();
                return;
            }

            // 정점 도달: 상승 속도가 0 이하 → 제어 해제.
            if (_rootBody.linearVelocity.y <= 0f)
                CompleteLaunch();
        }

        private void CompleteLaunch()
        {
            _isLaunching = false;
            _movement.LockXZ = false;
        }

        private void OnDisable()
        {
            if (_isLaunching)
                CompleteLaunch();
        }
    }
}
