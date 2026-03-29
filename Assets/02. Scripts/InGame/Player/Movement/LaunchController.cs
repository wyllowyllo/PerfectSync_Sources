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

        [Header("Blend")]
        [SerializeField] private float _blendOutDuration = 0.5f;

        private PlayerMovement _movement;
        private PlayerJump _playerJump;
        private PlayerAnimation _anim;
        private RagdollStateMachine _ragdollStateMachine;

        private bool _isLaunching;
        private bool _isBlending;

        public bool IsLaunching => _isLaunching;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _playerJump = GetComponent<PlayerJump>();
            _anim = GetComponent<PlayerAnimation>();
            _ragdollStateMachine = GetComponent<RagdollStateMachine>();
        }

        public void Launch(Vector3 targetPosition)
        {
            if (_isLaunching) return;

            float height = targetPosition.y - _rootBody.position.y;
            if (height <= 0f) return;

            // 래그돌 활성 중이면 즉시 복구.
            if (_ragdollStateMachine.IsRagdollActive)
                _ragdollStateMachine.ForceRecover();

            // 다이브 상태 정리.
            if (_playerJump.IsDiving)
                _playerJump.ClearDiving();

            // vy = √(2gh) — 목표 높이에 정확히 도달하는 상향 속도.
            float gravity = _movement.Gravity;
            float vy = Mathf.Sqrt(2f * gravity * height);

            // 정점까지 걸리는 시간: t = vy / g.
            float timeToApex = vy / gravity;

            // 그 시간 안에 목표 XZ에 도달하는 수평 속도.
            float vx = (targetPosition.x - _rootBody.position.x) / timeToApex;
            float vz = (targetPosition.z - _rootBody.position.z) / timeToApex;

            _rootBody.linearVelocity = new Vector3(vx, vy, vz);

            _movement.MomentumBlend = 1f;
            _isBlending = false;
            _anim.Jump();
            _isLaunching = true;
        }

        private void FixedUpdate()
        {
            // 정점 도달 후 블렌드 아웃: 발사 모멘텀 → 입력 제어로 서서히 전환.
            if (_isBlending)
            {
                float blend = _movement.MomentumBlend
                    - Time.fixedDeltaTime / _blendOutDuration;

                if (blend <= 0f)
                {
                    _movement.MomentumBlend = 0f;
                    _isBlending = false;
                }
                else
                {
                    _movement.MomentumBlend = blend;
                }
            }

            if (!_isLaunching) return;

            // 발사 중 래그돌 활성 → 비상 해제.
            if (_ragdollStateMachine.IsRagdollActive)
            {
                AbortLaunch();
                return;
            }

            // 정점 도달: 상승 속도가 0 이하 → 블렌드 아웃 시작.
            if (_rootBody.linearVelocity.y <= 0f)
                CompleteLaunch();
        }

        private void CompleteLaunch()
        {
            _isLaunching = false;
            _isBlending = true;
            // MomentumBlend를 즉시 0으로 하지 않음 — FixedUpdate에서 서서히 감쇠.
        }

        private void AbortLaunch()
        {
            _isLaunching = false;
            _isBlending = false;
            _movement.MomentumBlend = 0f;
        }

        private void OnDisable()
        {
            if (_isLaunching || _isBlending)
                AbortLaunch();
        }
    }
}
