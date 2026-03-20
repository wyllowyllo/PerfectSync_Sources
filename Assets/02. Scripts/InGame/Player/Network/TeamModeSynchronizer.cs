using System;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    // 모드 전환 RPC 브로드캐스트 담당 클래스
    public class TeamModeSynchronizer : MonoBehaviourPun
    {
        public event Action OnSwitchRequested;

        private PlayerFormController _playerFormController;

        private void Start()
        {
            _playerFormController = GetComponent<PlayerFormController>();
        }

        /// <summary>
        /// 외부에서 호출하여 모드 전환을 요청한다.
        /// Host → 바로 All 브로드캐스트, Guest → Host에게 릴레이 후 All 브로드캐스트.
        /// </summary>
        public void RequestSwitch()
        {
            if (_playerFormController != null && !_playerFormController.CanChangeForm()) return;

            if (photonView.IsMine)
                photonView.RPC(nameof(RpcRequestSwitch), RpcTarget.All);
            else
                photonView.RPC(nameof(RpcRelaySwitch), photonView.Owner);
        }

        [PunRPC]
        private void RpcRelaySwitch()
        {
            if (!photonView.IsMine) return;
            if (_playerFormController != null && !_playerFormController.CanChangeForm()) return;
            photonView.RPC(nameof(RpcRequestSwitch), RpcTarget.All);
        }

        [PunRPC]
        private void RpcRequestSwitch()
        {
            OnSwitchRequested?.Invoke();
        }
    }
}
