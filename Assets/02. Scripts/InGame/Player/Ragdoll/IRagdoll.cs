using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public enum ERagdollState { Animated, Stumble, Ragdolled, BlendToAnim, Dead }

    public interface IRagdoll
    {
        ERagdollState CurrentState { get; }
        bool IsRagdollActive { get; }
        void OnHitImpact(Vector3 impulse, Vector3 hitPoint, Vector3 torqueVector);
    }
}
