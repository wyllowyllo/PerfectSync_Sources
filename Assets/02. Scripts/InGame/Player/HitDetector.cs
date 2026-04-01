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

        private void OnCollisionEnter(Collision collision)
        {
            if (IsAnySourceInvincible()) return;
            if (!_isAuthority) return;

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
