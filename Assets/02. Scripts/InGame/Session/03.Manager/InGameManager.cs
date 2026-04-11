using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[RequireComponent(typeof(PhotonView))]
public class InGameManager : SingletonPunCallbacks<InGameManager>
{
    private const int DefaultCountdownSeconds = 3;
    private const float DefaultIntroDurationSeconds = 2f;

    protected override bool PersistAcrossScenes => false;

    [Header("Settings")]
    [SerializeField] private float _introDuration = DefaultIntroDurationSeconds;
    [SerializeField] private int _countdownSeconds = DefaultCountdownSeconds;

    [Header("References")]
    [SerializeField] private PlayerSpawner _playerSpawner;

    public GameState CurrentState { get; private set; } = GameState.Loading;
    public event Action<GameState> OnGameStateChanged;
    public event Action<int> OnRaceCountdownTick;

    private WaitForSeconds _waitIntro;
    private static bool _hasSpawned;

    protected override void Awake()
    {
        base.Awake();

        Debug.Log($"[InGameManager] Awake | InstanceID:{GetInstanceID()} | IsDuplicate:{IsDuplicateInstance} | _hasSpawned(static):{_hasSpawned} | Scene:{SceneManager.GetActiveScene().name}");

        if (IsDuplicateInstance) return;

        _waitIntro = new WaitForSeconds(_introDuration);
        InGameLocalPlayerPropertyReset.ApplyForLobbyScene(clearTeamBecauseNotInRoom: false);
        CloseRoomToNewJoiners();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public override void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDisable();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[InGameManager] OnSceneLoaded: {scene.name} (mode:{mode}) | InstanceID:{GetInstanceID()} | _hasSpawned(static):{_hasSpawned}");
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        _hasSpawned = false;
        Debug.Log("[InGameManager] OnLeftRoom → _hasSpawned reset to false");
    }

    private static void CloseRoomToNewJoiners()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        PhotonNetwork.CurrentRoom.IsOpen = false;
    }

    private IEnumerator Start()
    {
        Debug.Log($"[InGameManager] Start() BEGIN | InstanceID:{GetInstanceID()} | IsDuplicate:{IsDuplicateInstance} | _hasSpawned(static):{_hasSpawned}");

        if (IsDuplicateInstance) yield break;

        yield return new WaitUntil(() =>
            PhotonNetwork.InRoom &&
            PhotonTeamManager.GetLocalTeamRaw() != PhotonTeamManager.TeamNone &&
            AllPlayersHaveTeamAssigned()
        );

        // ── DEBUG: 전체 상태 스냅샷 (모든 클라이언트에서 출력) ──
        Debug.Log($"[InGameManager] ===== SPAWN DEBUG START (Local: {PhotonNetwork.LocalPlayer.NickName}, Actor:{PhotonNetwork.LocalPlayer.ActorNumber}, InstanceID:{GetInstanceID()}) =====");
        Debug.Log($"[InGameManager] IsMasterClient: {PhotonNetwork.IsMasterClient}, MasterClient Actor: {PhotonNetwork.MasterClient?.ActorNumber}");

        foreach (var player in PhotonNetwork.PlayerList)
        {
            int team = PhotonTeamManager.GetTeamRaw(player);
            int slot = PhotonTeamManager.GetTeamSlot(player);
            Debug.Log($"[InGameManager] Player: {player.NickName} (Actor:{player.ActorNumber}) | Team:{team} | Slot:{slot} | IsMaster:{player.IsMasterClient}");
        }

        CloseRoomToNewJoiners();

        int myTeam = PhotonTeamManager.GetLocalTeamRaw();
        bool isHost = IsHostOfTeam(myTeam);
        Debug.Log($"[InGameManager] MyTeam:{myTeam} | IsHostOfTeam:{isHost} | _hasSpawned(static):{_hasSpawned} | WillSpawn:{!_hasSpawned && isHost}");

        if (!_hasSpawned && isHost)
        {
            _hasSpawned = true;
            Debug.Log($"[InGameManager] >>> SPAWNING TeamCharacter for team {myTeam} | InstanceID:{GetInstanceID()}");
            _playerSpawner.SpawnTeamCharacter(myTeam);
        }

        Debug.Log($"[InGameManager] ===== SPAWN DEBUG END =====");
        SetLocalReady();
    }

    private void SetLocalReady()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { InGameRaceKeys.ReadyKey, true } });
    }

    private static bool AllPlayersHaveTeamAssigned()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return false;
        if (PhotonNetwork.PlayerList.Length < PhotonNetwork.CurrentRoom.MaxPlayers) return false;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (PhotonTeamManager.GetTeamRaw(player) == PhotonTeamManager.TeamNone)
                return false;
            if (PhotonTeamManager.GetTeamSlot(player) == PhotonTeamManager.SlotNone)
                return false;
        }
        return true;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(InGameRaceKeys.ReadyKey)) return;
        if (CurrentState != GameState.Loading) return;

        if (AreAllPlayersReady() && PhotonNetwork.IsMasterClient)
            photonView.RPC(nameof(RPC_StartIntro), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_StartIntro()
    {
        // ── DEBUG: 게임 시작 시점에 모든 클라이언트에서 TeamCharacter 수 확인 ──
        var allPhotonViews = FindObjectsOfType<PhotonView>();
        int teamCharacterCount = 0;
        foreach (var pv in allPhotonViews)
        {
            if (pv.gameObject.name.Contains("TeamCharacter"))
            {
                teamCharacterCount++;
                Debug.Log($"[InGameManager] FOUND TeamCharacter: {pv.gameObject.name} | ViewID:{pv.ViewID} | Owner:{pv.Owner?.NickName}(Actor:{pv.Owner?.ActorNumber}) | IsMine:{pv.IsMine}");
            }
        }
        Debug.Log($"[InGameManager] TOTAL TeamCharacters in scene: {teamCharacterCount} (expected: {PhotonTeamManager.MaxTeams}) | Observer: {PhotonNetwork.LocalPlayer.NickName}(Actor:{PhotonNetwork.LocalPlayer.ActorNumber})");

        StartCoroutine(GameFlowRoutine());
    }

    private IEnumerator GameFlowRoutine()
    {
        SetState(GameState.Intro);
        yield return _waitIntro;

        SetState(GameState.Countdown);
        for (int i = _countdownSeconds; i > 0; i--)
        {
            OnRaceCountdownTick?.Invoke(i);
            yield return CoroutineWaitCache.OneSecond;
        }

        SetState(GameState.Playing);
    }

    private void SetState(GameState newState)
    {
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);
    }

    public static bool IsLocalPlayerControllable =>
        Instance != null && Instance.CurrentState == GameState.Playing;

    public void EnterLocalRaceComplete()
    {
        if (CurrentState != GameState.Playing) return;
        TrySaveFinalRankToPlayerProperties();
        SetState(GameState.RaceComplete);
        SetLocalRaceDoneProperty();
    }

    public void EnterLocalGameOver()
    {
        if (CurrentState != GameState.Playing) return;
        TrySaveFinalRankToPlayerProperties();
        SetState(GameState.GameOver);
        SetLocalRaceDoneProperty();
    }

    private void TrySaveFinalRankToPlayerProperties()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
        if (RaceRankingManager.Instance == null) return;

        int myTeam = PhotonTeamManager.GetLocalTeamRaw();
        if (myTeam == PhotonTeamManager.TeamNone) return;

        foreach (var e in RaceRankingManager.Instance.CurrentRankings)
        {
            if (e.TeamNumber != myTeam) continue;

            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { InGameRaceKeys.FinalRankKey, e.Rank } });
            return;
        }
    }

    private void SetLocalRaceDoneProperty()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { InGameRaceKeys.RaceDoneKey, true } });
    }

    private bool AreAllPlayersReady()
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!player.CustomProperties.TryGetValue(InGameRaceKeys.ReadyKey, out object readyObj) || !(bool)readyObj)
                return false;
        }
        return PhotonNetwork.PlayerList.Length > 0;
    }

    // ── Team Utility ────────────────────────────────────

    public bool IsHostOfMyTeam()
    {
        int myTeam = PhotonTeamManager.GetLocalTeamRaw();
        if (myTeam == PhotonTeamManager.TeamNone) return false;
        return IsHostOfTeam(myTeam);
    }

    public static bool IsHostOfTeam(int teamNumber)
    {
        int myTeam = PhotonTeamManager.GetLocalTeamRaw();
        if (myTeam != teamNumber) return false;

        return PhotonTeamManager.IsLocalSlotHost();
    }

    public static int GetGuestActorNumber(int teamNumber)
    {
        int minActor = int.MaxValue;
        int otherActor = -1;
        int count = 0;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (PhotonTeamManager.GetTeamRaw(player) != teamNumber) continue;
            count++;
            if (player.ActorNumber < minActor)
            {
                otherActor = minActor == int.MaxValue ? -1 : minActor;
                minActor = player.ActorNumber;
            }
            else
            {
                otherActor = player.ActorNumber;
            }
        }

        return count < 2 ? -1 : otherActor;
    }

}
