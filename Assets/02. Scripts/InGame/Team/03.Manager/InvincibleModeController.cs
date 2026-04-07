using System;
using System.Collections.Generic;
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

        [Header("Collision")]
        [Tooltip("충돌 무시할 장애물 레이어")]
        [SerializeField] private LayerMask _obstacleLayers;

        private TeamModeSynchronizer _synchronizer;
        private PlayerMovement _movement;
        private bool _isInvincible;
        private float _timer;
        private bool _isAuthority;

        // 충돌 무시 복원용 캐시.
        private readonly List<(Collider mine, Collider other)> _ignoredPairs = new();

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

            RefreshAllInvincibleCollisions();
            OnInvincibleEnter?.Invoke();
        }

        private void Deactivate()
        {
            _isInvincible = false;
            _timer = 0f;

            if (_movement != null)
                _movement.BuffSpeedMultiplier = 1f;

            RefreshAllInvincibleCollisions();
            OnInvincibleExit?.Invoke();
        }

        /// <summary>
        /// 모든 무적 컨트롤러의 충돌 무시 상태를 재계산한다.
        /// - 무적 플레이어끼리는 물리 충돌을 유지한다 (서로 막힘, 넉백 없음).
        /// - 비무적 플레이어와는 충돌을 무시한다 (관통 + OverlapSphere 넉백).
        /// - 장애물과는 충돌을 무시한다 (관통 + OverlapSphere 파괴).
        /// </summary>
        private static void RefreshAllInvincibleCollisions()
        {
            var all = FindObjectsByType<InvincibleModeController>(FindObjectsSortMode.None);

            // 1. 기존 충돌 무시 모두 복원.
            foreach (var c in all)
            {
                foreach (var (mine, other) in c._ignoredPairs)
                {
                    if (mine != null && other != null)
                        Physics.IgnoreCollision(mine, other, false);
                }
                c._ignoredPairs.Clear();
            }

            // 무적 컨트롤러가 하나도 활성이 아니면 복원만 하고 종료.
            bool anyInvincible = false;
            foreach (var c in all)
            {
                if (c._isInvincible) { anyInvincible = true; break; }
            }
            if (!anyInvincible) return;

            // 2. 장애물 콜라이더 수집.
            LayerMask obstacleMask = 0;
            foreach (var c in all)
            {
                if (c._obstacleLayers != 0) { obstacleMask = c._obstacleLayers; break; }
            }

            List<Collider> obstacleColliders = null;
            if (obstacleMask != 0)
            {
                var allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
                obstacleColliders = new List<Collider>();
                foreach (var col in allColliders)
                {
                    if (col != null && ((1 << col.gameObject.layer) & obstacleMask) != 0)
                        obstacleColliders.Add(col);
                }
            }

            // 3. 무적 컨트롤러별 충돌 무시 재설정.
            foreach (var c in all)
            {
                if (!c._isInvincible) continue;

                var myColliders = c.GetComponentsInChildren<Collider>(true);

                // 3a. 비무적 플레이어와 충돌 무시.
                foreach (var other in all)
                {
                    if (other == c) continue;
                    if (other._isInvincible) continue;

                    var otherColliders = other.GetComponentsInChildren<Collider>(true);
                    foreach (var myCol in myColliders)
                    {
                        foreach (var otherCol in otherColliders)
                        {
                            if (myCol == null || otherCol == null) continue;
                            Physics.IgnoreCollision(myCol, otherCol, true);
                            c._ignoredPairs.Add((myCol, otherCol));
                        }
                    }
                }

                // 3b. 장애물과 충돌 무시.
                if (obstacleColliders != null)
                {
                    foreach (var obstacleCol in obstacleColliders)
                    {
                        if (obstacleCol == null) continue;
                        foreach (var myCol in myColliders)
                        {
                            if (myCol == null) continue;
                            Physics.IgnoreCollision(myCol, obstacleCol, true);
                            c._ignoredPairs.Add((myCol, obstacleCol));
                        }
                    }
                }
            }
        }
    }
}
