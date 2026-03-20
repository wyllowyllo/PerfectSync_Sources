using UnityEngine;

namespace InGame.Player.Data
{
    public readonly struct ImpactData
    {
        public readonly Vector3 Impulse;
        public readonly Vector3 HitPoint;
        public readonly float Magnitude;

        public ImpactData(Vector3 impulse, Vector3 hitPoint)
        {
            Impulse = impulse;
            HitPoint = hitPoint;
            Magnitude = impulse.magnitude;
        }
    }
}
