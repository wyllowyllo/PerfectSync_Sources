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

        public bool CustomizeTogglePressed
        {
            get
            {
                bool val = _customizeTogglePressed;
                _customizeTogglePressed = false;
                return val;
            }
        }

        public event Action<HitData, int> OnHitReceived;
        public event Action OnDeathReceived;
        public event Action OnRespawnReceived;
        public event Action OnRecoveryReceived;

        private Vector2 _moveInput;
        private bool _jumpPressed;
        private bool _customizeTogglePressed;
        private bool? _isMyTeam;

        private bool CheckIsMyTeam()
        {
            if (_isMyTeam.HasValue) return _isMyTeam.Value;

            var owner = photonView.Owner;
            if (owner == null) return false;

            int ownerTeam = PhotonTeamManager.GetTeamRaw(owner);
            int myTeam = PhotonTeamManager.GetLocalTeamRaw();

            if (ownerTeam == PhotonTeamManager.TeamNone || myTeam == PhotonTeamManager.TeamNone)
                return false;

            _isMyTeam = (ownerTeam == myTeam);
            return _isMyTeam.Value;
        }

        private void Update()
        {
            if (!CheckIsMyTeam()) return;

            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            _jumpPressed |= Input.GetButtonDown("Jump");
            _customizeTogglePressed |= Input.GetKeyDown(KeyCode.C);

            if (Input.GetKeyDown(KeyCode.R))
                SendRecovery();
        }

        public void SendHit(HitData hit, int hitViewID)
        {
            OnHitReceived?.Invoke(hit, hitViewID);
            photonView.RPC(nameof(RpcReceiveHit), RpcTarget.Others,
                hit.Knockback, hit.HitPoint, hitViewID, hit.Torque, (byte)hit.Response);
        }

        public void SendDeath()
        {
            OnDeathReceived?.Invoke();
            photonView.RPC(nameof(RpcReceiveDeath), RpcTarget.Others);
        }

        [PunRPC]
        private void RpcReceiveHit(Vector3 knockback, Vector3 hitPoint, int hitViewID, Vector3 torque, byte response)
        {
            var hit = new HitData(knockback, hitPoint, torque, (EHitResponse)response);
            OnHitReceived?.Invoke(hit, hitViewID);
        }

        [PunRPC]
        private void RpcReceiveDeath()
        {
            OnDeathReceived?.Invoke();
        }

        public void SendRespawn()
        {
            OnRespawnReceived?.Invoke();
            photonView.RPC(nameof(RpcReceiveRespawn), RpcTarget.Others);
        }

        [PunRPC]
        private void RpcReceiveRespawn()
        {
            OnRespawnReceived?.Invoke();
        }

        public void SendRecovery()
        {
            OnRecoveryReceived?.Invoke();
            photonView.RPC(nameof(RpcReceiveRecovery), RpcTarget.Others);
        }

        [PunRPC]
        private void RpcReceiveRecovery()
        {
            OnRecoveryReceived?.Invoke();
        }
    }
}
