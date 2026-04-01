using System;
using System.Collections.Generic;
using InGame.Effect;
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

        [Tooltip("상대 감지 반경")]
        [SerializeField] private float _detectionRadius = 1.2f;

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

        // FOV Kick 등 외부 연출 훅.
        public event Action OnHitLocal;

        public void SetAuthority(bool isAuthority) => _isAuthority = isAuthority;

        public void Initialize(InvincibleModeController controller, TeamModeSynchronizer synchronizer)
        {
            _invincibleController = controller;
            _synchronizer = synchronizer;
        }

        private void FixedUpdate()
        {
            if (!_isAuthority) return;
            if (_invincibleController == null || !_invincibleController.IsInvincible) return;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _detectionRadius, _overlapBuffer, _playerLayers);

            for (int i = 0; i < count; i++)
            {
                TryApplyKnockback(_overlapBuffer[i]);
            }
        }

        private void TryApplyKnockback(Collider other)
        {
            if (other == null) return;
            if (other.transform.IsChildOf(transform)) return;

            // 상대도 무적이면 skip.
            var victimInvincible = other.GetComponentInParent<InvincibleModeController>();
            if (victimInvincible != null && victimInvincible.IsInvincible) return;

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
