using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace InGame.Player.Network
{
    public class NetworkTestManager : MonoBehaviourPunCallbacks
    {
        [Header("Photon Settings")]
        [SerializeField] private string _gameVersion = "0.1";
        [SerializeField] private string _roomName = "TestRoom";
        [SerializeField] private string _nickName = "Player";

        [Header("Spawn Settings")]
        [SerializeField] private string _prefabName = "TeamCharacter";
        [SerializeField] private Vector3 _spawnPosition = Vector3.zero;
        [SerializeField] private float _teamSpawnSpacing = 5f;

        public static NetworkTestManager Instance { get; private set; }

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

            Debug.Log("[NetworkTestManager] Connecting to Photon...");
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log("[NetworkTestManager] Connected to Master. Joining room...");

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
            Debug.Log($"[NetworkTestManager] Joined room: {PhotonNetwork.CurrentRoom.Name} " +
                      $"(Players: {PhotonNetwork.CurrentRoom.PlayerCount})");

            // 테스트용: 팀 미배정 시 자동으로 팀 1에 배정
            // SetCustomProperties는 비동기 — OnPlayerPropertiesUpdate 콜백에서 TrySpawnTeamCharacter 재호출됨
            if (PhotonTeamManager.Instance != null)
            {
                int myTeam = PhotonTeamManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer);
                if (myTeam == PhotonTeamManager.TeamNone)
                {
                    PhotonTeamManager.Instance.SetTeam(1);
                }
            }

            TrySpawnTeamCharacter();
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            Debug.Log($"[NetworkTestManager] Player entered: {newPlayer.NickName} (Actor: {newPlayer.ActorNumber})");
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            Debug.Log($"[NetworkTestManager] Player left: {otherPlayer.NickName}");
        }

        public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps)
        {
            if (changedProps.ContainsKey(PhotonTeamManager.TeamKey))
            {
                TrySpawnTeamCharacter();
            }
        }

        /// <summary>
        /// 로컬 플레이어가 팀에 배정되어 있고, 팀 내 Host(가장 작은 ActorNumber)인 경우
        /// TeamCharacter를 스폰한다.
        /// </summary>
        private void TrySpawnTeamCharacter()
        {
            if (_hasSpawned) return;
            if (PhotonTeamManager.Instance == null) return;

            int myTeam = PhotonTeamManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer);
            if (myTeam == PhotonTeamManager.TeamNone) return;

            if (!IsHostOfTeam(myTeam)) return;

            _hasSpawned = true;
            SpawnTeamCharacter(myTeam);
        }

        /// <summary>
        /// 현재 로컬 플레이어가 자신의 팀 내에서 Host(가장 작은 ActorNumber)인지 반환한다.
        /// </summary>
        public bool IsHostOfMyTeam()
        {
            if (PhotonTeamManager.Instance == null) return PhotonNetwork.IsMasterClient;

            int myTeam = PhotonTeamManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer);
            if (myTeam == PhotonTeamManager.TeamNone) return false;

            return IsHostOfTeam(myTeam);
        }

        /// <summary>
        /// 지정된 팀 번호에서 로컬 플레이어가 Host인지 반환한다.
        /// 팀 내 가장 작은 ActorNumber를 가진 플레이어가 Host이다.
        /// </summary>
        public bool IsHostOfTeam(int teamNumber)
        {
            if (PhotonTeamManager.Instance == null) return false;

            var members = PhotonTeamManager.Instance.GetTeamMembers(teamNumber);
            if (members.Count == 0) return false;

            int minActor = int.MaxValue;
            foreach (var member in members)
            {
                if (member.ActorNumber < minActor)
                    minActor = member.ActorNumber;
            }

            return PhotonNetwork.LocalPlayer.ActorNumber == minActor;
        }

        /// <summary>
        /// 지정된 팀의 Guest(팀원 중 Host가 아닌 플레이어)의 ActorNumber를 반환한다.
        /// 없으면 -1.
        /// </summary>
        public int GetGuestActorNumber(int teamNumber)
        {
            if (PhotonTeamManager.Instance == null) return -1;

            var members = PhotonTeamManager.Instance.GetTeamMembers(teamNumber);
            if (members.Count < 2) return -1;

            int minActor = int.MaxValue;
            foreach (var member in members)
            {
                if (member.ActorNumber < minActor)
                    minActor = member.ActorNumber;
            }

            foreach (var member in members)
            {
                if (member.ActorNumber != minActor)
                    return member.ActorNumber;
            }

            return -1;
        }

        private void SpawnTeamCharacter(int teamNumber)
        {
            Vector3 spawnPos = _spawnPosition + Vector3.right * (teamNumber - 1) * _teamSpawnSpacing;
            PhotonNetwork.Instantiate(_prefabName, spawnPos, Quaternion.identity);

            Debug.Log($"[NetworkTestManager] Spawned {_prefabName} for team {teamNumber} at {spawnPos}");
        }
    }
}
