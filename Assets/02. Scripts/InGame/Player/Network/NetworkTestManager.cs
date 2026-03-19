using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

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

        public static NetworkTestManager Instance { get; private set; }

        public bool IsHost { get; private set; }
        public int HostActorNumber { get; private set; }
        public int GuestActorNumber { get; private set; }

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
                MaxPlayers = 2,
                IsVisible = true,
                IsOpen = true
            };

            PhotonNetwork.JoinOrCreateRoom(_roomName, roomOptions, TypedLobby.Default);
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"[NetworkTestManager] Joined room: {PhotonNetwork.CurrentRoom.Name} " +
                      $"(Players: {PhotonNetwork.CurrentRoom.PlayerCount})");

            DetermineRoles();

            // Host Authority: Host만 TeamCharacter를 스폰한다.
            // PUN2가 자동으로 모든 클라이언트에 인스턴스를 생성한다.
            if (IsHost)
            {
                SpawnTeamCharacter();
            }
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            Debug.Log($"[NetworkTestManager] Player entered: {newPlayer.NickName} (Actor: {newPlayer.ActorNumber})");
            DetermineRoles();
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            Debug.Log($"[NetworkTestManager] Player left: {otherPlayer.NickName}");
        }

        private void DetermineRoles()
        {
            var players = PhotonNetwork.PlayerList;

            if (players.Length < 2)
            {
                IsHost = PhotonNetwork.IsMasterClient;
                HostActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
                GuestActorNumber = -1;
            }
            else
            {
                int minActor = int.MaxValue;
                int maxActor = int.MinValue;

                foreach (var player in players)
                {
                    if (player.ActorNumber < minActor) minActor = player.ActorNumber;
                    if (player.ActorNumber > maxActor) maxActor = player.ActorNumber;
                }

                HostActorNumber = minActor;
                GuestActorNumber = maxActor;
                IsHost = PhotonNetwork.LocalPlayer.ActorNumber == minActor;
            }

            Debug.Log($"[NetworkTestManager] Role: {(IsHost ? "HOST" : "GUEST")} " +
                      $"(Local Actor: {PhotonNetwork.LocalPlayer.ActorNumber})");
        }

        private void SpawnTeamCharacter()
        {
            Vector3 spawnPos = _spawnPosition;
            PhotonNetwork.Instantiate(_prefabName, spawnPos, Quaternion.identity);

            Debug.Log($"[NetworkTestManager] Spawned {_prefabName} at {spawnPos}");
        }
    }
}
