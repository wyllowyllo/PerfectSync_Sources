using UnityEngine;

namespace Player.Domain
{
    public enum ERagdollState { Animated, Ragdoll, BlendToAnim, Dead }

    public interface IRagdoll
    {
        ERagdollState CurrentState { get; }
        bool IsRagdollActive { get; }
        void OnHitImpact(Vector3 impulse, Vector3 hitPoint);
    }
}
