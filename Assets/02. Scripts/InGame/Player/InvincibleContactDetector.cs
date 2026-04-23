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
        [Tooltip("카메라 쉐이크용 Impulse Source")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        [Tooltip("공격자 바디 PunchScale 연출")]
        [SerializeField] private PunchScaleEffect _punchScaleEffect;

        [Header("Obstacle Destroy")]
        [Tooltip("장애물에 가하는 힘 크기")]
        [SerializeField] private float _obstacleDestroyForce = 8f;

        [Tooltip("장애물 넉백 상향 비율 (0 = 수평, 1 = 완전 위)")]
        [SerializeField, Range(0f, 1f)] private float _obstacleUpwardBias = 0.4f;

        private InvincibleModeController _invincibleController;
        private TeamModeSynchronizer _synchronizer;
        private bool _isAuthority;

        private readonly Dictionary<int, float> _lastHitTimes = new();
        private const float HitCooldown = 0.5f;
        private const int PruneThreshold = 16;
        private const int MaxOverlapResults = 8;

        private readonly Collider[] _overlapBuffer = new Collider[MaxOverlapResults];
        private readonly List<(Collider mine, Collider other)> _ignoredPlayerPairs = new();

        // FOV Kick 등 외부 연출 훅.
        public event Action OnHitLocal;

        // 장애물 파괴 전용 연출 훅 (FOV 킥 분리용).
        public event Action OnObstacleDestroyLocal;

        public void SetAuthority(bool isAuthority) => _isAuthority = isAuthority;

        public void Initialize(InvincibleModeController controller, TeamModeSynchronizer synchronizer)
        {
            _invincibleController = controller;
            _synchronizer = synchronizer;

            // Non-authority는 RPC 수신 시 넉백 피드백 재생 (authority는 로컬에서 이미 재생).
            if (!_isAuthority)
            {
                _synchronizer.OnInvincibleHitApplied += HandleRemoteHitFeedback;
                _synchronizer.OnObstacleDestroyFeedback += HandleRemoteObstacleDestroyFeedback;
            }

            _invincibleController.OnInvincibleExit += RestoreIgnoredCollisions;
        }

        private void OnDestroy()
        {
            if (_synchronizer != null)
            {
                _synchronizer.OnInvincibleHitApplied -= HandleRemoteHitFeedback;
                _synchronizer.OnObstacleDestroyFeedback -= HandleRemoteObstacleDestroyFeedback;
            }

            if (_invincibleController != null)
                _invincibleController.OnInvincibleExit -= RestoreIgnoredCollisions;
        }

        private void HandleRemoteHitFeedback(Vector3 direction)
        {
            if (_invincibleController == null || !_invincibleController.IsInvincible) return;
            PlayHitFeedback(direction);
        }

        // 장애물 파괴는 이미 네트워크 확정된 상태이므로 IsInvincible 가드 없이 재생.
        private void HandleRemoteObstacleDestroyFeedback(Vector3 direction)
        {
            PlayObstacleDestroyFeedback(direction);
        }

        private float EffectiveObstacleRadius =>
            _obstacleDetectionRadius > 0f ? _obstacleDetectionRadius : _detectionRadius;

        private void FixedUpdate()
        {
            if (_invincibleController == null || !_invincibleController.IsInvincible) return;

            float obstacleRadius = EffectiveObstacleRadius;

            // Authority: 플레이어 넉백 + 장애물 파괴.
            // Non-authority: 장애물 로컬 예측 파괴만 (시각적 즉시 반응).
            float maxRadius = _isAuthority ? Mathf.Max(_detectionRadius, obstacleRadius) : obstacleRadius;
            LayerMask mask = _isAuthority ? (_playerLayers | _obstacleLayers) : _obstacleLayers;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, maxRadius, _overlapBuffer, mask);

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
                        TryDestroyObstacle(col);
                }
            }
        }

        // ── 장애물 파괴 ────────────────────────────────────────

        private void TryDestroyObstacle(Collider col)
        {
            var destroyable = col.GetComponentInParent<IDestroyable>();
            if (destroyable == null) return;
            if (destroyable.IsDestroyed) return;

            var manager = ObstacleDestroyManager.Instance;
            if (manager == null) return;

            int id = manager.GetId(destroyable);
            if (id < 0) return;

            // 쿨다운 체크.
            int key = id + 100000;
            if (_lastHitTimes.TryGetValue(key, out float lastTime)
                && Time.time - lastTime < HitCooldown)
                return;

            _lastHitTimes[key] = Time.time;

            // Force 방향: 자신 → 장애물 + 상향 bias.
            Vector3 obstaclePos = col.ClosestPoint(transform.position);
            Vector3 direction = (obstaclePos - transform.position).normalized;
            if (_obstacleUpwardBias > 0f)
                direction = Vector3.Lerp(direction, Vector3.up, _obstacleUpwardBias).normalized;

            Vector3 force = direction * _obstacleDestroyForce;

            if (_isAuthority)
            {
                manager.RequestDestroy(id, force);
                _synchronizer.BroadcastObstacleDestroyFeedback(direction);
                PlayObstacleDestroyFeedback(direction);
            }
            else
            {
                manager.PredictDestroy(id, force);
                // 피드백은 OnObstacleDestroyFeedback RPC 수신 시 재생.
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

            IgnoreCollisionWithTarget(other);
            PlayHitFeedback(direction);
        }

        // ── 충돌 무시 관리 ────────────────────────────────────────

        private void IgnoreCollisionWithTarget(Collider targetCollider)
        {
            var myColliders = _invincibleController.GetComponentsInChildren<Collider>(true);
            var targetView = targetCollider.GetComponentInParent<PhotonView>();
            if (targetView == null) return;

            var targetColliders = targetView.GetComponentsInChildren<Collider>(true);

            foreach (var myCol in myColliders)
            {
                foreach (var otherCol in targetColliders)
                {
                    if (myCol == null || otherCol == null) continue;
                    Physics.IgnoreCollision(myCol, otherCol, true);
                    _ignoredPlayerPairs.Add((myCol, otherCol));
                }
            }
        }

        private void RestoreIgnoredCollisions()
        {
            foreach (var (mine, other) in _ignoredPlayerPairs)
            {
                if (mine != null && other != null)
                    Physics.IgnoreCollision(mine, other, false);
            }
            _ignoredPlayerPairs.Clear();
        }

        // ── 피드백 ────────────────────────────────────────────────

        private void PlayObstacleDestroyFeedback(Vector3 direction)
        {
            var hitstop = HitstopEffect.Instance;
            if (hitstop != null)
                hitstop.PlayHeavy();

            if (_impulseSource != null)
                _impulseSource.GenerateImpulse(direction * 2.5f);

            if (_punchScaleEffect != null)
                _punchScaleEffect.Play();

            OnObstacleDestroyLocal?.Invoke();
        }

        private void PlayHitFeedback(Vector3 direction)
        {
            var hitstop = HitstopEffect.Instance;
            if (hitstop != null)
                hitstop.PlayLight();

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
