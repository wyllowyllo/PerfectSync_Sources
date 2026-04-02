namespace InGame.Obstacle
{
    public interface IFreezable
    {
        bool IsFrozen { get; }
        void Freeze();
        void Unfreeze();
    }
}
