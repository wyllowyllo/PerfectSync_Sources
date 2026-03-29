using System;
using InGame.Team;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    // 모드 전환 및 슬롯머신 RPC 브로드캐스트 담당 클래스
    public class TeamModeSynchronizer : MonoBehaviourPun
    {
        public event Action OnSwitchRequested;
        public event Action<ETeamMode> OnModeChangeRequested;
        public event Action<int[], bool> OnSlotSpinReceived;

        private PlayerFormController _playerFormController;

        private void Start()
        {
            _playerFormController = GetComponent<PlayerFormController>();
        }

        // ── 모드 전환 ──────────────────────────────────────────

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

        // ── 모드 지정 전환 ──────────────────────────────────────

        public void RequestModeChange(ETeamMode targetMode)
        {
            if (_playerFormController != null && !_playerFormController.CanChangeForm()) return;

            int mode = (int)targetMode;
            if (photonView.IsMine)
                photonView.RPC(nameof(RpcRequestModeChange), RpcTarget.All, mode);
            else
                photonView.RPC(nameof(RpcRelayModeChange), photonView.Owner, mode);
        }

        [PunRPC]
        private void RpcRelayModeChange(int mode)
        {
            if (!photonView.IsMine) return;
            if (_playerFormController != null && !_playerFormController.CanChangeForm()) return;
            photonView.RPC(nameof(RpcRequestModeChange), RpcTarget.All, mode);
        }

        [PunRPC]
        private void RpcRequestModeChange(int mode)
        {
            OnModeChangeRequested?.Invoke((ETeamMode)mode);
        }

        // ── 슬롯머신 스핀 ──────────────────────────────────────

        public void BroadcastSlotSpin(int[] symbols, bool isMatch)
        {
            photonView.RPC(nameof(RpcSlotSpin), RpcTarget.All,
                symbols[0], symbols[1], symbols[2], isMatch);
        }

        [PunRPC]
        private void RpcSlotSpin(int s0, int s1, int s2, bool isMatch)
        {
            int[] symbols = { s0, s1, s2 };
            OnSlotSpinReceived?.Invoke(symbols, isMatch);
        }
    }
}
