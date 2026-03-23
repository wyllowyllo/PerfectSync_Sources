using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public interface IImpactSource
    {
        bool TryComputeImpulse(Collision collision, out Vector3 impulse, out Vector3 torque);
    }
}
