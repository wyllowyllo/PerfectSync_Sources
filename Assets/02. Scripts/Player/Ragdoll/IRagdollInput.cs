using UnityEngine;

namespace Player.Ragdoll
{
    public interface IRagdollInput : IRagdollState
    {
        void OnHitImpact(Vector3 impulse, Vector3 hitPoint);
    }
}
