using System;
using Core;
using InGame.Player;
using Photon.Pun;
using UnityEngine;

namespace InGame.UserInput
{
    [DefaultExecutionOrder(ExecutionOrderConstants.LocalPlayerInput)]
    public class LocalPlayerInput : MonoBehaviourPun, IPlayerInput
    {
        public bool IsOwner => photonView.IsMine;

        public Vector2 MoveInput => _moveInput;
        public bool JumpPressed
        {
            get
            {
                bool val = _jumpPressed;
                _jumpPressed = false;
                return val;
            }
        }

        public event Action<HitData, int> OnHitReceived;
        public event Action OnDeathReceived;

        private Vector2 _moveInput;
        private bool _jumpPressed;

        private void Update()
        {
            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            _jumpPressed |= Input.GetButtonDown("Jump");
        }

        public void SendHit(HitData hit, int hitViewID)
        {
            OnHitReceived?.Invoke(hit, hitViewID);
            photonView.RPC(nameof(RpcReceiveHit), RpcTarget.Others,
                hit.Knockback, hit.HitPoint, hitViewID, hit.Torque);
        }

        public void SendDeath()
        {
            OnDeathReceived?.Invoke();
            photonView.RPC(nameof(RpcReceiveDeath), RpcTarget.Others);
        }

        [PunRPC]
        private void RpcReceiveHit(Vector3 knockback, Vector3 hitPoint, int hitViewID, Vector3 torque)
        {
            var hit = new HitData(knockback, hitPoint, torque);
            OnHitReceived?.Invoke(hit, hitViewID);
        }

        [PunRPC]
        private void RpcReceiveDeath()
        {
            OnDeathReceived?.Invoke();
        }
    }
}
