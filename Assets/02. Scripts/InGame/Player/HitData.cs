using UnityEngine;

namespace InGame.Player
{
    public readonly struct HitData
    {
        public readonly Vector3 Knockback;
        public readonly Vector3 HitPoint;
        public readonly Vector3 Torque;
        public readonly float Magnitude;

        public const float DefaultTorqueScale = 0.15f;

        public HitData(Vector3 knockback, Vector3 hitPoint, Vector3 torque)
        {
            Knockback = knockback;
            HitPoint = hitPoint;
            Torque = torque;
            Magnitude = knockback.magnitude;
        }

        public static Vector3 ComputeRandomTorque(float knockbackMagnitude, float scale = DefaultTorqueScale)
            => Random.insideUnitSphere * knockbackMagnitude * scale;
    }
}
