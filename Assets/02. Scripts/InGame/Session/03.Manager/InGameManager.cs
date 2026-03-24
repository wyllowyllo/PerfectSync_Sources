using System;
using System.Collections;
using UnityEngine;
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

    private GameObject _localPlayer;
    private WaitForSeconds _waitIntro;

    protected override void Awake()
    {
        base.Awake();
        _waitIntro = new WaitForSeconds(_introDuration);
        InGameLocalPlayerPropertyReset.ApplyForLobbyScene(clearTeamBecauseNotInRoom: false);
        CloseRoomToNewJoiners();
    }

    private static void CloseRoomToNewJoiners()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        PhotonNetwork.CurrentRoom.IsOpen = false;
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            PhotonNetwork.InRoom &&
            PhotonTeamManager.GetLocalTeamRaw() != PhotonTeamManager.TeamNone
        );

        CloseRoomToNewJoiners();

        _localPlayer = _playerSpawner.SpawnByTeam();

        if (_localPlayer != null)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { InGameRaceKeys.ReadyKey, true } });
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

}
