using System;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    public class NetworkTeamModeManager : MonoBehaviourPun
    {
        public event Action OnSwitchRequested;

        private PlayerFormController _playerFormController;

        private void Start()
        {
            _playerFormController = GetComponent<PlayerFormController>();
        }

        /// <summary>
        /// 외부에서 호출하여 모드 전환을 요청한다.
        /// PhotonView 소유자(팀 Host)만 RPC를 전송할 수 있다.
        /// 자동 타이머 시스템 등에서 이 메서드를 호출한다.
        /// </summary>
        public void RequestSwitch()
        {
            if (!photonView.IsMine) return;
            photonView.RPC(nameof(RpcRequestSwitch), RpcTarget.All);
        }

        [PunRPC]
        private void RpcRequestSwitch()
        {
            Debug.Log("[NetworkTeamModeManager] Mode switch requested via RPC");
            OnSwitchRequested?.Invoke();

            // 전환 후 DividedPlayer_B 소유권을 Guest(팀원)에게 이전
            TransferDividedPlayerBOwnership();
        }

        private void TransferDividedPlayerBOwnership()
        {
            if (_playerFormController == null)
                return;

            // 팀원(Guest) 찾기 — PhotonTeamManager를 통해 같은 팀의 다른 플레이어를 조회
            var guestPlayer = GetTeammate();
            if (guestPlayer == null)
                return;

            Transform avatarB = _playerFormController.SecondaryBodyTransform;
            if (avatarB == null)
                return;

            PhotonView bView = avatarB.GetComponent<PhotonView>();
            if (bView != null && photonView.IsMine)
            {
                bView.TransferOwnership(guestPlayer);
                Debug.Log($"[NetworkTeamModeManager] Transferred DividedPlayer_B ownership to {guestPlayer.NickName} (Actor: {guestPlayer.ActorNumber})");
            }
        }

        private Photon.Realtime.Player GetTeammate()
        {
            if (PhotonTeamManager.Instance == null) return null;

            int myTeam = PhotonTeamManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer);
            if (myTeam == PhotonTeamManager.TeamNone) return null;

            var members = PhotonTeamManager.Instance.GetTeamMembers(myTeam);
            foreach (var member in members)
            {
                if (member.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                    return member;
            }

            return null;
        }
    }
}
