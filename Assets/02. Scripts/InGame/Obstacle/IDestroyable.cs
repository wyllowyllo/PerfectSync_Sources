using UnityEngine;

namespace InGame.Obstacle
{
    public interface IDestroyable
    {
        bool IsDestroyed { get; }
        bool IsHidden { get; }
        void Destroy(Vector3 force, Vector3 randomTorque);
        void Hide();
        void PrepareRespawn();
        void Respawn();
    }
}
