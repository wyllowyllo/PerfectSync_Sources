using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace InGame.Player.Test
{
    /// <summary>
    /// 단일 씬 테스트용 네트워크 부트스트래퍼.
    /// Photon 접속 → 방 생성 → 전원 입장 대기 → 팀 일괄 배정까지 담당한다.
    /// 스폰과 게임 흐름은 씬에 배치된 InGameManager + PlayerSpawner가 처리한다.
    /// </summary>
    public class StandaloneTestManager : MonoBehaviourPunCallbacks
    {
        [Header("Photon Settings")]
        [SerializeField] private string _gameVersion = "0.1";
        [SerializeField] private string _roomName = "TestRoom";
        [SerializeField] private string _nickName = "Player";

        [Header("Test Settings")]
        [Tooltip("팀 배정 전 대기할 플레이어 수")]
        [SerializeField] private int _expectedPlayers = 2;

        public static StandaloneTestManager Instance { get; private set; }

        private bool _teamsAssigned;

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
                MaxPlayers = (byte)_expectedPlayers,
                IsVisible = true,
                IsOpen = true
            };

            PhotonNetwork.JoinOrCreateRoom(_roomName, roomOptions, TypedLobby.Default);
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"[StandaloneTestManager] Joined room: {PhotonNetwork.CurrentRoom.Name} " +
                      $"(Players: {PhotonNetwork.CurrentRoom.PlayerCount})");

            TryAssignTeamsWhenReady();
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            Debug.Log($"[StandaloneTestManager] Player entered: {newPlayer.NickName} (Actor: {newPlayer.ActorNumber})");
            TryAssignTeamsWhenReady();
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            Debug.Log($"[StandaloneTestManager] Player left: {otherPlayer.NickName}");
        }

        private void TryAssignTeamsWhenReady()
        {
            if (_teamsAssigned) return;
            if (!PhotonNetwork.IsMasterClient) return;
            if (PhotonNetwork.CurrentRoom.PlayerCount < _expectedPlayers) return;

            _teamsAssigned = true;
            PhotonTeamManager.Instance.AssignTeamsRandomly();
            Debug.Log($"[StandaloneTestManager] All {_expectedPlayers} players present – teams assigned");
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
