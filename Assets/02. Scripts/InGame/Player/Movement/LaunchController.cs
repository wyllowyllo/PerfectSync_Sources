using System;
using Core;
using InGame.Player.Animation;
using InGame.Player.Ragdoll;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("_physicsProfile")] [SerializeField] private Ragdoll.CharacterSpeedLimits speedLimits;

        [Header("Transition")]
        [SerializeField] private float _transitionDuration = 0.4f;

        [Header("Post-Launch Air Control")]
        [SerializeField] private float _postLaunchAirControlBoost = 3f;

        private PlayerMovement _movement;
        private PlayerJump _playerJump;
        private PlayerAnimation _anim;
        private RagdollStateMachine _ragdollStateMachine;

        private bool _isLaunching;
        private float _transitionStartVy;
        private float _maxLaunchSpeed;

        public bool IsLaunching => _isLaunching;
        public event Action OnLaunched;
        public event Action OnApexReached;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _playerJump = GetComponent<PlayerJump>();
            _anim = GetComponent<PlayerAnimation>();
            _ragdollStateMachine = GetComponent<RagdollStateMachine>();

            _maxLaunchSpeed = speedLimits != null ? speedLimits.MaxLaunchSpeed : 60f;
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
            float vy = Mathf.Sqrt(2f * _movement.Gravity * height);
            if (vy > _maxLaunchSpeed)
                vy = _maxLaunchSpeed;

            // 수직 발사: XZ 고정, Y만 이동.
            _rootBody.linearVelocity = new Vector3(0f, vy, 0f);

            // 전환 시작 지점의 vy: 정점 _transitionDuration초 전의 상승 속도.
            // vy = g * t 이므로, 정점 t초 전의 속도 = g * t.
            _transitionStartVy = _movement.Gravity * _transitionDuration;

            _movement.MomentumBlend = 1f;
            _movement.AirControlBoost = 1f;
            _anim.TrampolineLaunch();
            _isLaunching = true;
            OnLaunched?.Invoke();
        }

        private void FixedUpdate()
        {
            if (!_isLaunching) return;

            // 발사 중 래그돌 활성 → 비상 해제.
            if (_ragdollStateMachine.IsRagdollActive)
            {
                AbortLaunch();
                return;
            }

            float vy = _rootBody.linearVelocity.y;

            // 상승 속도가 전환 시작 지점 이하 → 정점까지 서서히 블렌드.
            if (vy <= _transitionStartVy)
            {
                // vy: transitionStartVy → 0 을 t: 0 → 1 로 매핑.
                float t = 1f - Mathf.Clamp01(vy / _transitionStartVy);
                float smoothT = t * t * (3f - 2f * t);

                _movement.MomentumBlend = 1f - smoothT;
                _movement.AirControlBoost = Mathf.Lerp(1f, _postLaunchAirControlBoost, smoothT);
            }

            // 정점 도달: 전환 완료.
            if (vy <= 0f)
                CompleteLaunch();
        }

        private void CompleteLaunch()
        {
            _isLaunching = false;
            _movement.MomentumBlend = 0f;
            _movement.AirControlBoost = _postLaunchAirControlBoost;
            OnApexReached?.Invoke();
        }

        private void AbortLaunch()
        {
            _isLaunching = false;
            _movement.MomentumBlend = 0f;
            _movement.AirControlBoost = 1f;
        }

        private void OnDisable()
        {
            if (_isLaunching)
                AbortLaunch();
        }
    }
}
