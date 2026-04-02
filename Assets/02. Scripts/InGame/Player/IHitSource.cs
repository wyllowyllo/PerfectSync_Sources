using UnityEngine;

namespace InGame.Player
{
    public interface IHitSource
    {
        bool TryComputeKnockback(
            Collision collision,
            out Vector3 knockback,
            out Vector3 torque,
            out EHitResponse response);
    }
}
