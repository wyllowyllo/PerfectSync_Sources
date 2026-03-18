using UnityEngine;

namespace Player.Domain
{
    public interface IPlayerInput
    {
        bool IsOwner { get; }
        Vector2 MoveInput { get; }
        bool JumpPressed { get; }
    }
}
