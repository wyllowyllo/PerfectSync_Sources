using System;
using System.Collections.Generic;
using InGame.Player.Movement;
using InGame.Player.Network;
using UnityEngine;

namespace InGame.Team
{
    /// <summary>
    /// 무적 모드 상태 관리 컨트롤러.
    /// TeamCharacter 루트에 부착 (TeamModeSynchronizer, MergedBodyController와 동일 레벨).
    /// </summary>
    public class InvincibleModeController : MonoBehaviour
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

        // 충돌 무시 복원용 캐시.
        private readonly List<(Collider mine, Collider other)> _ignoredPairs = new();

        /// <summary>
        /// 비주얼/사운드 훅 (Rainbow Shader 등 추후 연결).
        /// </summary>
        public event Action OnInvincibleEnter;
        public event Action OnInvincibleExit;

        public bool IsInvincible => _isInvincible;
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
            if (_duration <= 0f) return; // 영구 모드

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

            SetPlayerCollisionIgnored(true);
            OnInvincibleEnter?.Invoke();
        }

        private void Deactivate()
        {
            _isInvincible = false;
            _timer = 0f;

            if (_movement != null)
                _movement.BuffSpeedMultiplier = 1f;

            SetPlayerCollisionIgnored(false);
            OnInvincibleExit?.Invoke();
        }

        /// <summary>
        /// 무적 플레이어의 콜라이더와 다른 팀 플레이어 콜라이더 간 물리 충돌을 토글한다.
        /// </summary>
        private void SetPlayerCollisionIgnored(bool ignore)
        {
            if (!ignore)
            {
                foreach (var (mine, other) in _ignoredPairs)
                {
                    if (mine != null && other != null)
                        Physics.IgnoreCollision(mine, other, false);
                }
                _ignoredPairs.Clear();
                return;
            }

            _ignoredPairs.Clear();

            var myColliders = GetComponentsInChildren<Collider>(true);

            foreach (var otherController in FindObjectsByType<InvincibleModeController>(FindObjectsSortMode.None))
            {
                if (otherController == this) continue;

                var otherColliders = otherController.GetComponentsInChildren<Collider>(true);
                foreach (var myCol in myColliders)
                {
                    foreach (var otherCol in otherColliders)
                    {
                        if (myCol == null || otherCol == null) continue;
                        Physics.IgnoreCollision(myCol, otherCol, true);
                        _ignoredPairs.Add((myCol, otherCol));
                    }
                }
            }
        }
    }
}
