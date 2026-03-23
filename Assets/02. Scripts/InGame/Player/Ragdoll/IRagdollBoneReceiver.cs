namespace InGame.Player.Ragdoll
{
    public interface IRagdollBoneReceiver
    {
        void ApplySnapshot(RagdollBoneSnapshot snapshot);
        void StartReceiving();
        void StopReceiving();
        bool IsReceiving { get; }
    }
}
