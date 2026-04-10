using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace InGame.Player.Test
{
    /// <summary>
    /// 단일 씬 테스트용 네트워크 부트스트래퍼.
    /// Photon 접속 → 방 생성 → 전원 입장 대기 → 팀 일괄 배정까지 담당한다.
    /// 스폰과 게임 흐름은 씬에 배치된 InGameManager + PlayerSpawner가 처리한다.
    ///
    /// 주의: 이 매니저는 PhotonServerManager / LobbyRoomConnector 같은
    /// 기존 Persistent 싱글톤과 충돌하지 않도록 해당 오브젝트를 비활성화합니다.
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
            DisableConflictingSingletons();
            EnsureRequiredSingletons();
        }

        private void Start()
        {
            if (PhotonNetwork.IsConnected)
            {
                Debug.Log("[StandaloneTestManager] Already connected – leaving stale state...");
                PhotonNetwork.Disconnect();
                return; // OnDisconnected에서 재연결
            }

            ConnectToPhoton();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
                Debug.Log("[StandaloneTestManager] OnDestroy – Disconnected from Photon");
            }
        }

        private void OnApplicationQuit()
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
                Debug.Log("[StandaloneTestManager] OnApplicationQuit – Disconnected from Photon");
            }
        }

        private void ConnectToPhoton()
        {
            PhotonNetwork.NickName = _nickName + Random.Range(0, 9999);
            PhotonNetwork.GameVersion = _gameVersion;
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.SendRate = 40;
            PhotonNetwork.SerializationRate = 30;
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

            _teamsAssigned = false;
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

        public override void OnLeftRoom()
        {
            Debug.Log("[StandaloneTestManager] Left room");
            _teamsAssigned = false;
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[StandaloneTestManager] Disconnected: {cause}");

            // Play 모드 종료 중이면 재연결 안 함
            if (!gameObject.activeInHierarchy) return;

            // 의도적 disconnect 후 재연결 (stale 상태 정리용)
            if (cause == DisconnectCause.DisconnectByClientLogic)
            {
                ConnectToPhoton();
                return;
            }

            // 예기치 않은 연결 끊김: 재연결 시도
            Debug.Log("[StandaloneTestManager] Attempting reconnect...");
            ConnectToPhoton();
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

        /// <summary>
        /// 기존 Persistent 싱글톤이 이 테스트 씬의 연결/방 입장을 방해하지 않도록 비활성화한다.
        /// </summary>
        private void DisableConflictingSingletons()
        {
            if (PhotonServerManager.Instance != null)
            {
                PhotonServerManager.Instance.gameObject.SetActive(false);
                Debug.Log("[StandaloneTestManager] Disabled PhotonServerManager to prevent connection conflict");
            }

            if (LobbyRoomConnector.Instance != null)
            {
                LobbyRoomConnector.Instance.gameObject.SetActive(false);
                Debug.Log("[StandaloneTestManager] Disabled LobbyRoomConnector to prevent room join conflict");
            }
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
