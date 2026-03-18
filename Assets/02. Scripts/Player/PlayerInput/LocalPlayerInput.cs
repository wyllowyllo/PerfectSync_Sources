using System;
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

        public event Action<Vector3, Vector3> OnImpactReceived;
        public event Action OnDeathReceived;

        public void SendImpact(Vector3 impulse, Vector3 hitPoint)
            => OnImpactReceived?.Invoke(impulse, hitPoint);

        public void SendDeath()
            => OnDeathReceived?.Invoke();
    }
}
