using UnityEngine;

namespace Player.UserInput
{
    public interface IPlayerInput
    {
        bool IsOwner { get; }
        Vector2 MoveInput { get; }
        bool JumpPressed { get; }
        bool DivePressed { get; }
    }
}
