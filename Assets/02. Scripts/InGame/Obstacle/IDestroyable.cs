using UnityEngine;

namespace InGame.Obstacle
{
    public interface IDestroyable
    {
        bool IsDestroyed { get; }
        void Destroy(Vector3 force, bool isMaster);
        void Respawn();
        void ApplyNetworkState(Vector3 position, Quaternion rotation);
    }
}
