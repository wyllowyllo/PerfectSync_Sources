namespace InGame.Player.Ragdoll
{
    public enum EPoseDirection
    {
        AnimToRagdoll,
        RagdollToAnim
    }

    public interface IPoseTransfer
    {
        void SetDirection(EPoseDirection direction);
        void CopyPose();
        void Stop();
    }
}
