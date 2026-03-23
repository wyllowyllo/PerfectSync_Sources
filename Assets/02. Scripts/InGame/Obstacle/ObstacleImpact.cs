using System.Collections.Generic;
using InGame.Player;
using InGame.Player.Ragdoll;
using UnityEngine;

namespace InGame.Obstacle
{
    public class ObstacleImpact : MonoBehaviour, IImpactSource
    {
        [SerializeField] private ObstacleImpactProfile _profile;

        private Dictionary<int, float> _lastHitTimes;

        private const int PruneThreshold = 16;

        private void Awake()
        {
            _lastHitTimes = new Dictionary<int, float>();
        }

        public bool TryComputeImpulse(Collision collision, out Vector3 impulse, out Vector3 torque)
        {
            impulse = Vector3.zero;
            torque = Vector3.zero;

            if (_profile == null) return false;

            int colliderId = collision.collider.GetInstanceID();

            if (_profile.Cooldown > 0f
                && _lastHitTimes.TryGetValue(colliderId, out float lastTime)
                && Time.time - lastTime < _profile.Cooldown)
            {
                return false;
            }

            impulse = _profile.ComputeImpulse(collision, transform);
            torque = _profile.ComputeTorque(impulse.magnitude);

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
