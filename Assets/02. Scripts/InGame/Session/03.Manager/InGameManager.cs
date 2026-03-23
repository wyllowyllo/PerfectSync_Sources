using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[RequireComponent(typeof(PhotonView))]
public class InGameManager : SingletonPunCallbacks<InGameManager>
{
    protected override bool PersistAcrossScenes => false;

    private const string ReadyKey = "inGameReady";

    [Header("Settings")]
    [SerializeField] private float _introDuration = 2f;
    [SerializeField] private int _countdownSeconds = 3;

    [Header("References")]
    [SerializeField] private PlayerSpawner _playerSpawner;

    public GameState CurrentState { get; private set; } = GameState.Loading;
    public event Action<GameState> OnGameStateChanged;
    public event Action<int> OnRaceCountdownTick;

    private GameObject _localPlayer;

    protected override void Awake()
    {
        base.Awake();
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            PhotonNetwork.InRoom &&
            PhotonTeamManager.GetLocalTeamRaw() != PhotonTeamManager.TeamNone
        );

        Debug.Log($"[InGameManager] 팀 확인 완료: {PhotonTeamManager.GetLocalTeamRaw()}");

        _localPlayer = _playerSpawner.SpawnByTeam();

        if (_localPlayer != null)
        {
            Debug.Log("[InGameManager] 로컬 플레이어 스폰 완료. Ready 전송.");
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { ReadyKey, true } });
        }
        else
        {
            Debug.LogError("[InGameManager] 로컬 플레이어 스폰 실패.");
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(ReadyKey)) return;
        if (CurrentState != GameState.Loading) return;

        Debug.Log($"[InGameManager] OnPlayerPropertiesUpdate: {targetPlayer.NickName} - {changedProps[ReadyKey]}");

        if (AreAllPlayersReady() && PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[InGameManager] 모든 플레이어 Ready. Intro 시작 RPC 전송.");
            photonView.RPC(nameof(RPC_StartIntro), RpcTarget.All);
        }
    }

    [PunRPC]
    private void RPC_StartIntro()
    {
        StartCoroutine(GameFlowRoutine());
    }

    private IEnumerator GameFlowRoutine()
    {
        SetState(GameState.Intro);
        Debug.Log("[InGameManager] Intro 시작");
        yield return new WaitForSeconds(_introDuration);

        SetState(GameState.Countdown);
        for (int i = _countdownSeconds; i > 0; i--)
        {
            OnRaceCountdownTick?.Invoke(i);
            Debug.Log($"[InGameManager] {i}...");
            yield return new WaitForSeconds(1f);
        }

        SetState(GameState.Playing);
        Debug.Log("[InGameManager] 게임 시작!");
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
            if (!player.CustomProperties.TryGetValue(ReadyKey, out object readyObj) || !(bool)readyObj)
                return false;
        }
        return PhotonNetwork.PlayerList.Length > 0;
    }

}
