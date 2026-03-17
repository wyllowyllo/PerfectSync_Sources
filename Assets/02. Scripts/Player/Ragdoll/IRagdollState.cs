namespace Player.Ragdoll
{
    public enum ERagdollState { Animated, Ragdoll, BlendToAnim }

    public interface IRagdollState
    {
        ERagdollState CurrentState { get; }
        bool IsRagdollActive => CurrentState == ERagdollState.Ragdoll || CurrentState == ERagdollState.BlendToAnim;
    }
}
