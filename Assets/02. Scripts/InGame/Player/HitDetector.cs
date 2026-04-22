using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Player
{
    public class HitDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask _hazardLayers;

        [FormerlySerializedAs("_minImpulse")]
        [SerializeField] private float _minKnockback = 3f;

        private bool _isAuthority;
        private readonly List<IInvincibilitySource> _invincibilitySources = new();

        public event Action<HitData> OnHitDetected;

        // 무적 모드 RPC 경로로 받은 피격 통지. 모든 클라이언트에서 발행 (authority 가드 없음).
        public event Action<HitData> OnInvincibleHitReceived;

        public void ApplyExternalHit(HitData hit)
        {
            if (IsAnySourceInvincible()) return;
            if (!_isAuthority) return;
            OnHitDetected?.Invoke(hit);
        }

        public void NotifyInvincibleHit(HitData hit)
        {
            OnInvincibleHitReceived?.Invoke(hit);
        }

        public void AddInvincibilitySource(IInvincibilitySource source)
        {
            if (!_invincibilitySources.Contains(source))
                _invincibilitySources.Add(source);
        }

        public void SetAuthority(bool isAuthority)
        {
            _isAuthority = isAuthority;
        }

        private bool IsAnySourceInvincible()
        {
            foreach (var source in _invincibilitySources)
            {
                if (source.IsInvincible)
                    return true;
            }
            return false;
        }

        [SerializeField, Range(0f, 1f)]
        [Tooltip("이 값보다 contact normal의 Y가 크면 '위에서 밟음'으로 판정하여 넉백 무시 (0.5 ≈ 60°)")]
        private float _topContactThreshold = 0.5f;

        private void OnCollisionEnter(Collision collision)
        {
            if (IsAnySourceInvincible()) return;
            if (!_isAuthority) return;

            if ((_hazardLayers & (1 << collision.gameObject.layer)) == 0) return;

            if (collision.contactCount > 0 && collision.GetContact(0).normal.y > _topContactThreshold)
                return;

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

        private const float MaxFallbackKnockback = 50f;

        private bool TryComputeFallbackKnockback(Collision collision, out Vector3 knockback, out Vector3 torque)
        {
            knockback = collision.relativeVelocity;
            if (knockback.sqrMagnitude < _minKnockback * _minKnockback)
            {
                torque = Vector3.zero;
                return false;
            }

            // 솔버 디페네트레이션 방어: relativeVelocity 기반 knockback 크기 제한.
            if (knockback.sqrMagnitude > MaxFallbackKnockback * MaxFallbackKnockback)
                knockback = knockback.normalized * MaxFallbackKnockback;

            torque = HitData.ComputeRandomTorque(knockback.magnitude);
            return true;
        }
    }
}
