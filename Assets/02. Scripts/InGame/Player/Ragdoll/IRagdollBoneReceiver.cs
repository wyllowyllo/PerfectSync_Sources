namespace InGame.Player.Ragdoll
{
    public interface IRagdollBoneReceiver
    {
        void ApplySnapshot(RagdollBoneSnapshot snapshot);
        void Activate();
        void Deactivate();
        bool IsActive { get; }
    }
}
