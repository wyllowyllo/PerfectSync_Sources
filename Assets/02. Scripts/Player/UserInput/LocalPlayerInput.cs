using UnityEngine;

namespace Player.UserInput
{
    public class LocalPlayerInput : MonoBehaviour, IPlayerInput
    {
        public bool IsOwner => true;

        public Vector2 MoveInput => new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));

        public bool JumpPressed => Input.GetButtonDown("Jump");

        public bool DivePressed => Input.GetKeyDown(KeyCode.LeftShift);
    }
}
