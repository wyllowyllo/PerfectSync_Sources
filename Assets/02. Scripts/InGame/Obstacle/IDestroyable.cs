using UnityEngine;

namespace InGame.Obstacle
{
    public interface IDestroyable
    {
        bool IsDestroyed { get; }
        bool IsHidden { get; }
        void Destroy(Vector3 force, bool isMaster);
        void Hide();
        void Respawn();
        void ApplyNetworkState(Vector3 position, Quaternion rotation);
    }
}
