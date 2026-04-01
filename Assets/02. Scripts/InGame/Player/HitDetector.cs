using System;
using InGame.Team;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Player
{
    public class HitDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask _hazardLayers;
        [SerializeField] private LayerMask _playerLayers;

        [FormerlySerializedAs("_minImpulse")]
        [SerializeField] private float _minKnockback = 3f;

        private bool _isAuthority;
        private InvincibleModeController _invincibleController;

        public event Action<HitData> OnHitDetected;

        public void SetInvincibleController(InvincibleModeController controller)
        {
            _invincibleController = controller;
        }

        public void SetAuthority(bool isAuthority)
        {
            _isAuthority = isAuthority;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isAuthority) return;

            // 자신이 무적이면 모든 피격 면역.
            if (_invincibleController != null && _invincibleController.IsInvincible) return;

            // 상대가 무적인 플레이어와 충돌 → 넉백 피격 처리 (피해자 주도).
            if ((_playerLayers & (1 << collision.gameObject.layer)) != 0)
            {
                var attackerInvincible = collision.gameObject.GetComponentInParent<InvincibleModeController>();
                if (attackerInvincible != null && attackerInvincible.IsInvincible)
                {
                    HandleHitByInvinciblePlayer(collision, attackerInvincible);
                    return;
                }
            }

            // 일반 장애물 피격.
            if ((_hazardLayers & (1 << collision.gameObject.layer)) == 0) return;

            Vector3 knockback;
            Vector3 torque;
            EHitResponse response;

            var source = collision.gameObject.GetComponent<IHitSource>();
            if (source != null)
            {
                if (!source.TryComputeKnockback(collision, out knockback, out torque, out response))
                    return;
            }
            else if (TryComputeFallbackKnockback(collision, out knockback, out torque))
            {
                response = EHitResponse.Default;
            }
            else
            {
                return;
            }

            Vector3 hitPoint = collision.GetContact(0).point;
            var hit = new HitData(knockback, hitPoint, torque, response);
            OnHitDetected?.Invoke(hit);
        }

        /// <summary>
        /// 무적 플레이어에게 충돌당했을 때 피해자 측에서 처리.
        /// 기존 OnHitDetected 파이프라인을 그대로 사용하여 즉시 래그돌 전환.
        /// </summary>
        private void HandleHitByInvinciblePlayer(Collision collision, InvincibleModeController attacker)
        {
            // 넉백 방향: 공격자 → 피해자(자신).
            Vector3 direction = (transform.position - collision.transform.position).normalized;
            float upwardBias = attacker.KnockbackUpwardBias;
            if (upwardBias > 0f)
                direction = Vector3.Lerp(direction, Vector3.up, upwardBias).normalized;

            Vector3 knockback = direction * attacker.KnockbackForce;
            Vector3 hitPoint = collision.GetContact(0).point;
            Vector3 torque = HitData.ComputeRandomTorque(knockback.magnitude);

            var hit = new HitData(knockback, hitPoint, torque, EHitResponse.Ragdoll);
            OnHitDetected?.Invoke(hit);
        }

        private bool TryComputeFallbackKnockback(Collision collision, out Vector3 knockback, out Vector3 torque)
        {
            knockback = collision.relativeVelocity;
            if (knockback.sqrMagnitude < _minKnockback * _minKnockback)
            {
                torque = Vector3.zero;
                return false;
            }

            torque = HitData.ComputeRandomTorque(knockback.magnitude);
            return true;
        }
    }
}
