using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace InGame.Player.Test
{
    /// <summary>
    /// 단일 씬 테스트용 네트워크 부트스트래퍼.
    /// Photon 접속 → 방 생성 → 팀 자동 배정까지만 담당한다.
    /// 스폰과 게임 흐름은 씬에 배치된 InGameManager + PlayerSpawner가 처리한다.
    /// </summary>
    public class StandaloneTestManager : MonoBehaviourPunCallbacks
    {
        [Header("Photon Settings")]
        [SerializeField] private string _gameVersion = "0.1";
        [SerializeField] private string _roomName = "TestRoom";
        [SerializeField] private string _nickName = "Player";

        public static StandaloneTestManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureRequiredSingletons();
        }

        private void Start()
        {
            PhotonNetwork.NickName = _nickName + Random.Range(0, 9999);
            PhotonNetwork.GameVersion = _gameVersion;
            PhotonNetwork.ConnectUsingSettings();

            Debug.Log("[StandaloneTestManager] Connecting to Photon...");
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log("[StandaloneTestManager] Connected to Master. Joining room...");

            RoomOptions roomOptions = new RoomOptions
            {
                MaxPlayers = 8,
                IsVisible = true,
                IsOpen = true
            };

            PhotonNetwork.JoinOrCreateRoom(_roomName, roomOptions, TypedLobby.Default);
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"[StandaloneTestManager] Joined room: {PhotonNetwork.CurrentRoom.Name} " +
                      $"(Players: {PhotonNetwork.CurrentRoom.PlayerCount})");

            if (PhotonTeamManager.GetLocalTeamRaw() == PhotonTeamManager.TeamNone)
                AssignToAvailableTeam();
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            Debug.Log($"[StandaloneTestManager] Player entered: {newPlayer.NickName} (Actor: {newPlayer.ActorNumber})");
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            Debug.Log($"[StandaloneTestManager] Player left: {otherPlayer.NickName}");
        }

        private void AssignToAvailableTeam()
        {
            if (PhotonTeamManager.Instance != null)
            {
                for (int team = 1; team <= PhotonTeamManager.MaxTeams; team++)
                {
                    if (PhotonTeamManager.Instance.SetTeam(team))
                    {
                        Debug.Log($"[StandaloneTestManager] Assigned to team {team}");
                        return;
                    }
                }

                Debug.LogWarning("[StandaloneTestManager] All teams are full!");
                return;
            }

            // PhotonTeamManager 인스턴스가 없을 때 직접 custom property로 팀 배정.
            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new Hashtable { { PhotonTeamManager.TeamKey, 1 } });
            Debug.Log("[StandaloneTestManager] Assigned team 1 via direct property (fallback)");
        }

        private void EnsureRequiredSingletons()
        {
            if (PhotonTeamManager.Instance == null)
            {
                new GameObject("[Test] PhotonTeamManager")
                    .AddComponent<PhotonTeamManager>();
                Debug.Log("[StandaloneTestManager] Created PhotonTeamManager singleton");
            }
        }
    }
}
