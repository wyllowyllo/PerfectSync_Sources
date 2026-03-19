using System;
using Photon.Pun;
using UnityEngine;

namespace InGame.UserInput
{
    public class NetworkPlayerInput : MonoBehaviourPun, IPlayerInput, IPunObservable
    {
        public bool IsOwner => photonView.IsMine;

        public Vector2 MoveInput => _moveInput;
        public bool JumpPressed => _jumpPressed;

        public event Action<Vector3, Vector3> OnImpactReceived;
        public event Action OnDeathReceived;

        private Vector2 _moveInput;
        private bool _jumpPressed;

        private void Update()
        {
            if (!photonView.IsMine)
                return;

            _moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            _jumpPressed = Input.GetButtonDown("Jump");
        }

        public void SendImpact(Vector3 impulse, Vector3 hitPoint)
        {
            OnImpactReceived?.Invoke(impulse, hitPoint);
            photonView.RPC(nameof(RpcReceiveImpact), RpcTarget.Others,
                impulse, hitPoint);
        }

        public void SendDeath()
        {
            OnDeathReceived?.Invoke();
            photonView.RPC(nameof(RpcReceiveDeath), RpcTarget.Others);
        }

        [PunRPC]
        private void RpcReceiveImpact(Vector3 impulse, Vector3 hitPoint)
        {
            OnImpactReceived?.Invoke(impulse, hitPoint);
        }

        [PunRPC]
        private void RpcReceiveDeath()
        {
            OnDeathReceived?.Invoke();
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(_moveInput);
                stream.SendNext(_jumpPressed);
            }
            else
            {
                _moveInput = (Vector2)stream.ReceiveNext();
                _jumpPressed = (bool)stream.ReceiveNext();
            }
        }
    }
}
