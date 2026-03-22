using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public enum GameState
{
    Loading,
    Intro,
    Countdown,
    Playing,
    /// <summary>로컬 플레이어 레이스 완료(1위 또는 제한 시간 내 완주).</summary>
    RaceComplete,
    /// <summary>로컬 플레이어 미완주로 레이스 종료(예: 10초 내 결승 미도착).</summary>
    GameOver
}

[RequireComponent(typeof(PhotonView))]
public class InGameManager : MonoBehaviourPunCallbacks
{
    public static InGameManager Instance { get; private set; }

    private const string ReadyKey = "inGameReady";

    [Header("Settings")]
    [SerializeField] private float _introDuration = 2f;
    [SerializeField] private int _countdownSeconds = 3;

    [Header("References")]
    [SerializeField] private PlayerSpawner _playerSpawner;

    public GameState CurrentState { get; private set; } = GameState.Loading;
    public event Action<GameState> OnGameStateChanged;
    /// <summary>레이스 시작 전 카운트다운 숫자(예: 3, 2, 1). 매 초 Invoke.</summary>
    public event Action<int> OnRaceCountdownTick;

    private GameObject _localPlayer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            PhotonNetwork.InRoom &&
            GetLocalPlayerTeam() != PhotonTeamManager.TeamNone
        );

        Debug.Log($"[InGameManager] 팀 확인 완료: {GetLocalPlayerTeam()}");

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

    #region Photon Callbacks

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

    #endregion

    #region Game Flow

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

    #endregion

    #region Helpers

    private void SetState(GameState newState)
    {
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);
    }

    /// <summary>로컬 플레이어 조작 가능 여부. <see cref="GameState.Playing"/>일 때만 true.</summary>
    public static bool IsLocalPlayerControllable =>
        Instance != null && Instance.CurrentState == GameState.Playing;

    /// <summary>결승 완료 등으로 Playing 종료(입력 불가).</summary>
    public void EnterLocalRaceComplete()
    {
        if (CurrentState != GameState.Playing) return;
        TrySaveFinalRankToPlayerProperties();
        SetState(GameState.RaceComplete);
        SetLocalRaceDoneProperty();
    }

    /// <summary>제한 시간 내 미완주 등으로 Playing 종료(입력 불가).</summary>
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

        int myTeam = GetLocalPlayerTeam();
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

    public static int GetLocalPlayerTeam()
    {
        return GetPlayerTeam(PhotonNetwork.LocalPlayer);
    }

    public static int GetPlayerTeam(Player player)
    {
        if (player.CustomProperties.TryGetValue(PhotonTeamManager.TeamKey, out object teamObj))
            return (int)teamObj;
        return PhotonTeamManager.TeamNone;
    }

    #endregion
}
