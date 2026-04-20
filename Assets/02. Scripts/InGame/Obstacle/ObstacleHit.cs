using System;
using System.Collections.Generic;
using InGame.Player;
using UnityEngine;

namespace InGame.Obstacle
{
    public class ObstacleHit : MonoBehaviour, IHitSource
    {
        [SerializeField] private ObstacleHitProfile _profile;

        public ObstacleHitProfile Profile => _profile;

        public event Action<Collision> OnCollisionDetected;
        public event Action<Collision> OnCollisionEnded;

        private Dictionary<int, float> _lastHitTimes;

        private const int PruneThreshold = 16;

        private void Awake()
        {
            _lastHitTimes = new Dictionary<int, float>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            OnCollisionDetected?.Invoke(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            OnCollisionEnded?.Invoke(collision);
        }

        public bool TryComputeKnockback(
            Collision collision,
            out Vector3 knockback,
            out Vector3 torque,
            out EHitResponse response)
        {
            knockback = Vector3.zero;
            torque = Vector3.zero;
            response = EHitResponse.Default;

            if (!enabled) return false;
            if (_profile == null) return false;

            int colliderId = collision.collider.GetInstanceID();

            if (_profile.Cooldown > 0f
                && _lastHitTimes.TryGetValue(colliderId, out float lastTime)
                && Time.time - lastTime < _profile.Cooldown)
            {
                return false;
            }

            knockback = _profile.ComputeKnockback(collision, transform);
            torque = _profile.ComputeTorque(knockback.magnitude);
            response = _profile.Response;

            if (_profile.Cooldown > 0f)
            {
                _lastHitTimes[colliderId] = Time.time;

                if (_lastHitTimes.Count > PruneThreshold)
                    PruneStaleEntries();
            }

            return true;
        }

        private void PruneStaleEntries()
        {
            float expiry = _profile.Cooldown * 2f;
            float now = Time.time;

            var staleKeys = new List<int>();
            foreach (var kvp in _lastHitTimes)
            {
                if (now - kvp.Value > expiry)
                    staleKeys.Add(kvp.Key);
            }

            for (int i = 0; i < staleKeys.Count; i++)
                _lastHitTimes.Remove(staleKeys[i]);
        }
    }
}
