using Player.Domain;
using UnityEngine;

namespace Player.PlayerInput
{
    public class LocalPlayerInput : MonoBehaviour, IPlayerInput
    {
        public bool IsOwner => true;

        public Vector2 MoveInput => new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));

        public bool JumpPressed => Input.GetButtonDown("Jump");
    }
}
