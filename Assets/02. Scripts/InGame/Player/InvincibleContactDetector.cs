using System;
using System.Collections.Generic;
using InGame.Effect;
using InGame.Obstacle;
using InGame.Player.Network;
using InGame.Team;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// 무적 플레이어가 상대와 근접 시 넉백을 적용하는 가해자 주도 컴포넌트.
    /// Physics.IgnoreCollision으로 물리 충돌이 꺼져 있으므로 OverlapSphere로 감지한다.
    /// merged body에 부착 (HitDetector와 동일 GameObject).
    /// </summary>
    public class InvincibleContactDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask _playerLayers;

        [SerializeField] private LayerMask _obstacleLayers;

        [Tooltip("상대 감지 반경")]
        [SerializeField] private float _detectionRadius = 1.2f;

        [Tooltip("장애물 감지 반경 (0이면 _detectionRadius 사용)")]
        [SerializeField] private float _obstacleDetectionRadius;

        [Header("Hit Feedback")]
        [Tooltip("카메라 쉐이크용 Impulse Source. 없으면 쉐이크 생략.")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        [Tooltip("공격자 바디 PunchScale 연출. 없으면 생략.")]
        [SerializeField] private PunchScaleEffect _punchScaleEffect;

        private InvincibleModeController _invincibleController;
        private TeamModeSynchronizer _synchronizer;
        private bool _isAuthority;

        private readonly Dictionary<int, float> _lastHitTimes = new();
        private const float HitCooldown = 0.5f;
        private const int PruneThreshold = 16;
        private const int MaxOverlapResults = 8;

        private readonly Collider[] _overlapBuffer = new Collider[MaxOverlapResults];

        // 현재 프레임에서 freeze 중인 장애물 registry ID 집합.
        private readonly HashSet<int> _currentlyFrozenIds = new();

        // FOV Kick 등 외부 연출 훅.
        public event Action OnHitLocal;

        public void SetAuthority(bool isAuthority) => _isAuthority = isAuthority;

        public void Initialize(InvincibleModeController controller, TeamModeSynchronizer synchronizer)
        {
            _invincibleController = controller;
            _synchronizer = synchronizer;

            _invincibleController.OnInvincibleExit += HandleInvincibleExit;
        }

        private void OnDestroy()
        {
            if (_invincibleController != null)
                _invincibleController.OnInvincibleExit -= HandleInvincibleExit;
        }

        private float EffectiveObstacleRadius =>
            _obstacleDetectionRadius > 0f ? _obstacleDetectionRadius : _detectionRadius;

        private void FixedUpdate()
        {
            if (!_isAuthority) return;
            if (_invincibleController == null || !_invincibleController.IsInvincible) return;

            float obstacleRadius = EffectiveObstacleRadius;
            float maxRadius = Mathf.Max(_detectionRadius, obstacleRadius);

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, maxRadius, _overlapBuffer, _playerLayers | _obstacleLayers);

            var detectedObstacleIds = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                int layerBit = 1 << col.gameObject.layer;

                if ((layerBit & _playerLayers) != 0)
                {
                    float dist = Vector3.Distance(transform.position, col.ClosestPoint(transform.position));
                    if (dist <= _detectionRadius)
                        TryApplyKnockback(col);
                }
                else if ((layerBit & _obstacleLayers) != 0)
                {
                    float dist = Vector3.Distance(transform.position, col.ClosestPoint(transform.position));
                    if (dist <= obstacleRadius)
                        TryCollectFreezable(col, detectedObstacleIds);
                }
            }

            ProcessObstacleFreezeChanges(detectedObstacleIds);
        }

        // ── 장애물 Freeze ──────────────────────────────────────

        private void TryCollectFreezable(Collider col, HashSet<int> detectedIds)
        {
            var freezable = col.GetComponentInParent<IFreezable>();
            if (freezable == null) return;

            var manager = ObstacleFreezeManager.Instance;
            if (manager == null) return;

            int id = manager.GetId(freezable);
            if (id < 0) return;

            detectedIds.Add(id);
        }

        private void ProcessObstacleFreezeChanges(HashSet<int> detectedIds)
        {
            var manager = ObstacleFreezeManager.Instance;
            if (manager == null) return;

            // 새로 감지된 장애물 → freeze.
            var toFreeze = new List<int>();
            foreach (int id in detectedIds)
            {
                if (!_currentlyFrozenIds.Contains(id))
                    toFreeze.Add(id);
            }

            // 범위 이탈 장애물 → unfreeze.
            var toUnfreeze = new List<int>();
            foreach (int id in _currentlyFrozenIds)
            {
                if (!detectedIds.Contains(id))
                    toUnfreeze.Add(id);
            }

            if (toFreeze.Count > 0)
                manager.RequestFreeze(toFreeze.ToArray());

            if (toUnfreeze.Count > 0)
                manager.RequestUnfreeze(toUnfreeze.ToArray());

            _currentlyFrozenIds.Clear();
            foreach (int id in detectedIds)
                _currentlyFrozenIds.Add(id);
        }

        private void HandleInvincibleExit()
        {
            if (!_isAuthority) return;

            if (_currentlyFrozenIds.Count > 0)
            {
                var manager = ObstacleFreezeManager.Instance;
                if (manager != null)
                {
                    var ids = new int[_currentlyFrozenIds.Count];
                    _currentlyFrozenIds.CopyTo(ids);
                    manager.RequestUnfreeze(ids);
                }

                _currentlyFrozenIds.Clear();
            }
        }

// ── 플레이어 넉백 (기존) ────────────────────────────────

        private void TryApplyKnockback(Collider other)
        {
            if (other == null) return;
            if (other.transform.IsChildOf(transform)) return;

            // 상대가 어떤 무적 상태든 skip (팀 무적, 리스폰 무적 등).
            var victimSources = other.GetComponentsInParent<IInvincibilitySource>();
            foreach (var source in victimSources)
            {
                if (source.IsInvincible) return;
            }

            var victimView = other.GetComponentInParent<PhotonView>();
            if (victimView == null) return;

            int victimViewID = victimView.ViewID;

            if (_lastHitTimes.TryGetValue(victimViewID, out float lastTime)
                && Time.time - lastTime < HitCooldown)
                return;

            _lastHitTimes[victimViewID] = Time.time;
            if (_lastHitTimes.Count > PruneThreshold)
                PruneStaleEntries();

            // 넉백 방향: 자신 → 상대 방향 + 상향 bias.
            Vector3 direction = (other.transform.position - transform.position).normalized;
            if (_invincibleController.KnockbackUpwardBias > 0f)
                direction = Vector3.Lerp(direction, Vector3.up, _invincibleController.KnockbackUpwardBias).normalized;

            Vector3 knockback = direction * _invincibleController.KnockbackForce;
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 torque = HitData.ComputeRandomTorque(knockback.magnitude);

            _synchronizer.BroadcastInvincibleHit(victimViewID, knockback, hitPoint, torque, (byte)EHitResponse.Ragdoll);

            if (_impulseSource != null)
                _impulseSource.GenerateImpulse(direction);

            if (_punchScaleEffect != null)
                _punchScaleEffect.Play();

            OnHitLocal?.Invoke();
        }

        private void PruneStaleEntries()
        {
            float now = Time.time;
            var staleKeys = new List<int>();
            foreach (var kvp in _lastHitTimes)
            {
                if (now - kvp.Value > HitCooldown * 2f)
                    staleKeys.Add(kvp.Key);
            }

            for (int i = 0; i < staleKeys.Count; i++)
                _lastHitTimes.Remove(staleKeys[i]);
        }
    }
}
