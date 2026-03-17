using UnityEngine;

namespace Player.Ragdoll
{
    public enum ERagdollState { Animated, Ragdoll, BlendToAnim }

    public interface IRagdoll
    {
        ERagdollState CurrentState { get; }
        bool IsRagdollActive => CurrentState == ERagdollState.Ragdoll || CurrentState == ERagdollState.BlendToAnim;
        void OnHitImpact(Vector3 impulse, Vector3 hitPoint);
        void ForceRagdoll();
    }
}
