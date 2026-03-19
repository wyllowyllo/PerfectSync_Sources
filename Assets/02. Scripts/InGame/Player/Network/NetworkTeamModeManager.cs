using System;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    public class NetworkTeamModeManager : MonoBehaviourPun
    {
        private static NetworkTeamModeManager s_instance;

        public static NetworkTeamModeManager Instance => s_instance;

        public event Action OnSwitchRequested;

        private PlayerFormController _playerFormController;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(this);
                return;
            }

            s_instance = this;
        }

        private void Start()
        {
            _playerFormController = GetComponent<PlayerFormController>();
        }

        private void Update()
        {
            if (!photonView.IsMine)
                return;

            // Host만 모드 전환 트리거 가능
            if (!NetworkTestManager.Instance.IsHost)
                return;

            if (Input.GetKeyDown(KeyCode.M))
            {
                photonView.RPC(nameof(RpcRequestSwitch), RpcTarget.All);
            }
        }

        [PunRPC]
        private void RpcRequestSwitch()
        {
            Debug.Log("[NetworkTeamModeManager] Mode switch requested via RPC");
            OnSwitchRequested?.Invoke();

            // 전환 후 DividedPlayer_B 소유권을 Guest에게 이전
            TransferDividedPlayerBOwnership();
        }

        private void TransferDividedPlayerBOwnership()
        {
            if (_playerFormController == null)
                return;

            int guestActorNumber = NetworkTestManager.Instance.GuestActorNumber;
            if (guestActorNumber < 0)
                return;

            // DividedPlayer_B의 PhotonView 소유권을 Guest에게 이전
            Transform avatarB = _playerFormController.SecondaryBodyTransform;
            if (avatarB == null)
                return;

            PhotonView bView = avatarB.GetComponent<PhotonView>();
            if (bView != null && photonView.IsMine)
            {
                Photon.Realtime.Player guestPlayer = null;
                foreach (var player in PhotonNetwork.PlayerList)
                {
                    if (player.ActorNumber == guestActorNumber)
                    {
                        guestPlayer = player;
                        break;
                    }
                }

                if (guestPlayer != null)
                {
                    bView.TransferOwnership(guestPlayer);
                    Debug.Log($"[NetworkTeamModeManager] Transferred DividedPlayer_B ownership to Guest (Actor: {guestActorNumber})");
                }
            }
        }
    }
}
