using System;
using InGame.Player;
using InGame.Player.Movement;
using InGame.Player.Network;
using UnityEngine;

namespace InGame.Team
{
    public class InvincibleModeController : MonoBehaviour, IInvincibilitySource
    {
        [Header("Settings")]
        [Tooltip("무적 지속 시간 (초). 0 = 영구 지속")]
        [SerializeField] private float _duration = 10f;

        [Tooltip("이동 속도 배율 (기본 속도 기준)")]
        [SerializeField] private float _speedMultiplier = 1.5f;

        [Tooltip("상대 플레이어 넉백 크기")]
        [SerializeField] private float _knockbackForce = 20f;

        [Tooltip("넉백 방향의 상향 비율 (0 = 수평, 1 = 완전 위)")]
        [SerializeField, Range(0f, 1f)] private float _knockbackUpwardBias = 0.3f;

        private TeamModeSynchronizer _synchronizer;
        private PlayerMovement _movement;
        private bool _isInvincible;
        private float _timer;
        private bool _isAuthority;

        public event Action OnInvincibleEnter;
        public event Action OnInvincibleExit;

        public bool IsInvincible => _isInvincible;
        public float RemainingTime => _timer;
        public float KnockbackForce => _knockbackForce;
        public float KnockbackUpwardBias => _knockbackUpwardBias;

        public void SetAuthority(bool isAuthority) => _isAuthority = isAuthority;

        private void Start()
        {
            _synchronizer = GetComponent<TeamModeSynchronizer>();
            _movement = GetComponentInChildren<PlayerMovement>();

            _synchronizer.OnInvincibleModeChanged += HandleModeChanged;
        }

        private void OnDestroy()
        {
            if (_synchronizer != null)
                _synchronizer.OnInvincibleModeChanged -= HandleModeChanged;
        }

        private void Update()
        {
            if (!_isInvincible) return;
            if (!_isAuthority) return;
            if (_duration <= 0f) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                _synchronizer.BroadcastInvincibleMode(false);
        }

        private void HandleModeChanged(bool active)
        {
            if (active)
                Activate();
            else
                Deactivate();
        }

        private void Activate()
        {
            _isInvincible = true;
            _timer = _duration;

            if (_movement != null)
                _movement.BuffSpeedMultiplier = _speedMultiplier;

            OnInvincibleEnter?.Invoke();
        }

        private void Deactivate()
        {
            _isInvincible = false;
            _timer = 0f;

            if (_movement != null)
                _movement.BuffSpeedMultiplier = 1f;

            OnInvincibleExit?.Invoke();
        }

    }
}
