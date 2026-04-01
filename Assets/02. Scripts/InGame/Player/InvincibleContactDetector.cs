using System.Collections.Generic;
using InGame.Player.Network;
using InGame.Team;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// 무적 플레이어가 상대와 충돌 시 넉백을 적용하는 가해자 주도 컴포넌트.
    /// merged body에 부착 (HitDetector와 동일 GameObject).
    /// </summary>
    public class InvincibleContactDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask _playerLayers;

        private InvincibleModeController _invincibleController;
        private TeamModeSynchronizer _synchronizer;
        private bool _isAuthority;

        private readonly Dictionary<int, float> _lastHitTimes = new();
        private const float HitCooldown = 0.5f;
        private const int PruneThreshold = 16;

        public void SetAuthority(bool isAuthority) => _isAuthority = isAuthority;

        public void Initialize(InvincibleModeController controller, TeamModeSynchronizer synchronizer)
        {
            _invincibleController = controller;
            _synchronizer = synchronizer;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isAuthority) return;
            if (_invincibleController == null || !_invincibleController.IsInvincible) return;
            if ((_playerLayers & (1 << collision.gameObject.layer)) == 0) return;

            // 상대 팀의 무적 컨트롤러 확인 → 상대도 무적이면 skip.
            var victimInvincible = collision.gameObject.GetComponentInParent<InvincibleModeController>();
            if (victimInvincible != null && victimInvincible.IsInvincible) return;

            // 상대의 PhotonView 획득.
            var victimView = collision.gameObject.GetComponentInParent<PhotonView>();
            if (victimView == null) return;

            int victimViewID = victimView.ViewID;

            // 쿨다운 체크.
            if (_lastHitTimes.TryGetValue(victimViewID, out float lastTime)
                && Time.time - lastTime < HitCooldown)
                return;

            _lastHitTimes[victimViewID] = Time.time;
            if (_lastHitTimes.Count > PruneThreshold)
                PruneStaleEntries();

            // 넉백 방향: 자신 → 상대 방향 + 상향 bias.
            Vector3 direction = (collision.transform.position - transform.position).normalized;
            if (_invincibleController.KnockbackUpwardBias > 0f)
                direction = Vector3.Lerp(direction, Vector3.up, _invincibleController.KnockbackUpwardBias).normalized;

            Vector3 knockback = direction * _invincibleController.KnockbackForce;
            Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            Vector3 torque = HitData.ComputeRandomTorque(knockback.magnitude);

            _synchronizer.BroadcastInvincibleHit(
                victimViewID, knockback, hitPoint, torque, (byte)EHitResponse.Ragdoll);
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
