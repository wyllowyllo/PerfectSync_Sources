using System;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public class ImpactDetector : MonoBehaviour
    {
        private const float TorqueScaleFactor = 0.15f;

        [SerializeField] private LayerMask _hazardLayers;
        [SerializeField] private float _minImpulse = 3f;

        private bool _isAuthority;

        public event Action<Vector3, Vector3, Vector3> OnImpactDetected;

        public void SetAuthority(bool isAuthority)
        {
            _isAuthority = isAuthority;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isAuthority) return;
            if ((_hazardLayers & (1 << collision.gameObject.layer)) == 0) return;

            Vector3 impulse;
            Vector3 torque;

            var source = collision.gameObject.GetComponent<IImpactSource>();
            if (source != null)
            {
                if (!source.TryComputeImpulse(collision, out impulse, out torque))
                    return;
            }
            else if (!TryComputeFallbackImpulse(collision, out impulse, out torque))
            {
                return;
            }

            Vector3 hitPoint = collision.GetContact(0).point;
            OnImpactDetected?.Invoke(impulse, hitPoint, torque);
        }

        private bool TryComputeFallbackImpulse(Collision collision, out Vector3 impulse, out Vector3 torque)
        {
            impulse = collision.relativeVelocity;
            if (impulse.sqrMagnitude < _minImpulse * _minImpulse)
            {
                torque = Vector3.zero;
                return false;
            }

            torque = UnityEngine.Random.insideUnitSphere * impulse.magnitude * TorqueScaleFactor;
            return true;
        }
    }
}
