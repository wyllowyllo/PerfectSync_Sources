using UnityEngine;

namespace InGame.Player.Movement
{
    public interface ILaunchable
    {
        bool IsLaunching { get; }
        void Launch(Vector3 targetPosition);
    }
}
