using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace InGame.Player.Test
{
    /// <summary>
    /// 단일 씬 테스트용 매니저.
    /// Photon 접속 → 방 생성 → 팀 자동 배정 → TeamCharacter 스폰을 한 클래스에서 처리한다.
    /// 로비/매칭 인프라 없이 단독으로 동작하므로, 빠른 단일 씬 테스트에 적합하다.
    /// </summary>
    public class StandaloneTestManager : MonoBehaviourPunCallbacks
    {
        [Header("Photon Settings")]
        [SerializeField] private string _gameVersion = "0.1";
        [SerializeField] private string _roomName = "TestRoom";
        [SerializeField] private string _nickName = "Player";

        [Header("Spawn Settings")]
        [SerializeField] private string _prefabName = "TeamCharacter";
        [SerializeField] private Vector3 _spawnPosition = Vector3.zero;
        [SerializeField] private float _teamSpawnSpacing = 5f;

        public static StandaloneTestManager Instance { get; private set; }

        private bool _hasSpawned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
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

            TrySpawnTeamCharacter();
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            Debug.Log($"[StandaloneTestManager] Player entered: {newPlayer.NickName} (Actor: {newPlayer.ActorNumber})");
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            Debug.Log($"[StandaloneTestManager] Player left: {otherPlayer.NickName}");
        }

        public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps)
        {
            if (changedProps.ContainsKey(PhotonTeamManager.TeamKey))
            {
                TrySpawnTeamCharacter();
            }
        }

        private void TrySpawnTeamCharacter()
        {
            if (_hasSpawned) return;

            int myTeam = PhotonTeamManager.GetLocalTeamRaw();
            if (myTeam == PhotonTeamManager.TeamNone) return;

            if (!InGameManager.IsHostOfTeam(myTeam)) return;

            _hasSpawned = true;
            SpawnTeamCharacter(myTeam);
        }

        public bool IsHostOfMyTeam()
        {
            int myTeam = PhotonTeamManager.GetLocalTeamRaw();
            if (myTeam == PhotonTeamManager.TeamNone)
                return PhotonNetwork.IsMasterClient;

            return InGameManager.IsHostOfTeam(myTeam);
        }

        private void AssignToAvailableTeam()
        {
            if (PhotonTeamManager.Instance == null)
            {
                Debug.LogWarning("[StandaloneTestManager] PhotonTeamManager.Instance is null — cannot assign team");
                return;
            }

            for (int team = 1; team <= PhotonTeamManager.MaxTeams; team++)
            {
                if (PhotonTeamManager.Instance.SetTeam(team))
                {
                    Debug.Log($"[StandaloneTestManager] Assigned to team {team}");
                    return;
                }
            }

            Debug.LogWarning("[StandaloneTestManager] All teams are full!");
        }

        private void SpawnTeamCharacter(int teamNumber)
        {
            Vector3 spawnPos = _spawnPosition + Vector3.right * (teamNumber - 1) * _teamSpawnSpacing;
            PhotonNetwork.Instantiate(_prefabName, spawnPos, Quaternion.identity);

            Debug.Log($"[StandaloneTestManager] Spawned {_prefabName} for team {teamNumber} at {spawnPos}");
        }
    }
}
