using System;
using Photon.Pun;
using UnityEngine;

namespace InGame.UserInput
{
    public class RemotePlayerInput : MonoBehaviourPun, IPlayerInput
    {
        public bool IsOwner => false;

        public Vector2 MoveInput => _moveInput;
        public bool JumpPressed => _jumpPressed;

        public event Action<Vector3, Vector3> OnImpactReceived;
        public event Action OnDeathReceived;

        private Vector2 _moveInput;
        private bool _jumpPressed;

        public void SetInput(Vector2 moveInput, bool jumpPressed)
        {
            _moveInput = moveInput;
            _jumpPressed = jumpPressed;
        }

        public void SendImpact(Vector3 impulse, Vector3 hitPoint)
        {
            OnImpactReceived?.Invoke(impulse, hitPoint);
        }

        public void SendDeath()
        {
            OnDeathReceived?.Invoke();
        }
    }
}
