using System;
using System.Collections;
using DG.Tweening;
using InGame.Camera.PlayerCamera;
using TMPro;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[RequireComponent(typeof(PhotonView))]
public class InGameManager : SingletonPunCallbacks<InGameManager>
{
    private const int DefaultCountdownSeconds = 3;
    private const int CountdownBufferMs = 200;
    private const float AvatarInfoFadeDuration = 0.5f;

    protected override bool PersistAcrossScenes => false;

    [Header("Settings")]
    [SerializeField] private int _countdownSeconds = DefaultCountdownSeconds;

    [Header("References")]
    [SerializeField] private PlayerSpawner _playerSpawner;
    [SerializeField] private TextMeshProUGUI _avatarInfoText;

    public GameState CurrentState { get; private set; } = GameState.Loading;
    public bool IsLocalPaused { get; private set; }

    public event Action<GameState> OnGameStateChanged;
    public event Action<bool> OnLocalPauseChanged;
    public event Action<int> OnRaceCountdownTick;
    public event Action OnCeremonyReady;
    public event Action OnCeremonyReturnRequested;

    protected override void Awake()
    {
        base.Awake();
        if (IsDuplicateInstance) return;

        InGameLocalPlayerPropertyReset.ApplyForLobbyScene(clearTeamBecauseNotInRoom: false);
        CloseRoomToNewJoiners();
        ApplyCursorState();
    }

    public void NotifyLocalIntroDone()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { InGameRaceKeys.IntroDoneKey, true } });
    }

    private static void CloseRoomToNewJoiners()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        PhotonNetwork.CurrentRoom.IsOpen = false;
    }

    private IEnumerator Start()
    {
        if (IsDuplicateInstance) yield break;

        yield return new WaitUntil(() =>
            PhotonNetwork.InRoom &&
            PhotonTeamManager.GetLocalTeamRaw() != PhotonTeamManager.TeamNone &&
            AllPlayersHaveTeamAssigned()
        );

        CloseRoomToNewJoiners();

        int myTeam = PhotonTeamManager.GetLocalTeamRaw();
        if (IsHostOfTeam(myTeam))
        {
            PhotonNetwork.DestroyPlayerObjects(PhotonNetwork.LocalPlayer);
            _playerSpawner.SpawnTeamCharacter(myTeam);
        }

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
        SetState(GameState.Intro);

        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(MasterGameFlowRoutine());
    }

    private IEnumerator MasterGameFlowRoutine()
    {
        // 모든 클라이언트의 인트로 카메라 완료 대기.
        yield return new WaitUntil(AreAllPlayersIntroDone);

        // 카운트다운 시작 시각과 게임 시작 시각을 서버 타임스탬프 기준으로 계산.
        int countdownStartTime = PhotonNetwork.ServerTimestamp + CountdownBufferMs;
        int playStartTime = countdownStartTime + _countdownSeconds * 1000;

        photonView.RPC(nameof(RPC_StartCountdown), RpcTarget.All, countdownStartTime, playStartTime);
    }

    [PunRPC]
    private void RPC_StartCountdown(int countdownStartServerTime, int playStartServerTime)
    {
        SetState(GameState.Countdown);
        StartCoroutine(SyncedCountdownRoutine(countdownStartServerTime, playStartServerTime));
    }

    private IEnumerator SyncedCountdownRoutine(int countdownStartServerTime, int playStartServerTime)
    {
        // 카운트다운 시작 시각까지 대기.
        while (PhotonNetwork.ServerTimestamp < countdownStartServerTime)
            yield return null;

        for (int i = _countdownSeconds; i > 0; i--)
        {
            OnRaceCountdownTick?.Invoke(i);

            int nextTickTime = playStartServerTime - (i - 1) * 1000;
            while (PhotonNetwork.ServerTimestamp < nextTickTime)
                yield return null;
        }

        HideAvatarInfoText();
        SetState(GameState.Playing);
    }

    private void HideAvatarInfoText()
    {
        if (_avatarInfoText == null) return;

        _avatarInfoText.DOKill();
        _avatarInfoText.DOFade(0f, AvatarInfoFadeDuration)
            .SetLink(_avatarInfoText.gameObject)
            .OnComplete(() =>
            {
                if (_avatarInfoText != null)
                    _avatarInfoText.gameObject.SetActive(false);
            });
    }

    private bool AreAllPlayersIntroDone()
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!player.CustomProperties.TryGetValue(InGameRaceKeys.IntroDoneKey, out object val) || !(bool)val)
                return false;
        }
        return PhotonNetwork.PlayerList.Length > 0;
    }

    private void SetState(GameState newState)
    {
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);
        ApplyCursorState();
    }

    public static bool IsLocalPlayerControllable =>
        Instance != null
        && Instance.CurrentState == GameState.Playing
        && !Instance.IsLocalPaused;

    public void SetLocalPaused(bool paused)
    {
        if (IsLocalPaused == paused) return;
        IsLocalPaused = paused;
        ApplyCursorState();
        OnLocalPauseChanged?.Invoke(paused);
    }

    private void ApplyCursorState()
    {
        bool locked = !IsLocalPaused;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void EnterLocalRaceComplete()
    {
        if (CurrentState != GameState.Playing) return;
        SetState(GameState.RaceComplete);
        SetLocalRaceDoneProperty();
    }

    public void EnterLocalGameOver()
    {
        if (CurrentState != GameState.Playing) return;
        SetState(GameState.GameOver);
        SetLocalRaceDoneProperty();
    }

    public void EnterCeremony()
    {
        if (CurrentState < GameState.Playing) return;
        if (CurrentState == GameState.Ceremony) return;

        // 마스터가 자기 RaceRankingManager의 현재 순위를 팀 인덱스 기반 int 배열로 직렬화
        // 하여 RPC 인자에 실어 모든 클라이언트에 전파. 모든 클라이언트가 동일한 최종 순위를 사용.
        int[] ranksByTeamIndex = BuildRanksByTeamIndex();
        photonView.RPC(nameof(RPC_EnterCeremony), RpcTarget.All, ranksByTeamIndex);
    }

    /// <summary>RaceRankingManager.CurrentRankings를 "팀 인덱스 기반 순위 배열"로 직렬화.</summary>
    private static int[] BuildRanksByTeamIndex()
    {
        int[] ranks = new int[PhotonTeamManager.MaxTeams];
        if (RaceRankingManager.Instance == null) return ranks;

        foreach (var entry in RaceRankingManager.Instance.CurrentRankings)
        {
            int idx = entry.TeamNumber - 1;
            if (idx < 0 || idx >= ranks.Length) continue;
            ranks[idx] = entry.Rank;
        }
        return ranks;
    }

    [PunRPC]
    private void RPC_EnterCeremony(int[] ranksByTeamIndex)
    {
        if (CurrentState < GameState.Playing) return;
        if (CurrentState == GameState.Ceremony) return;

        // 모든 클라이언트가 동일한 최종 순위를 로컬 캐시에 저장
        RaceFinalRankingStore.Store(ranksByTeamIndex);

        // 자기 팀의 최종 순위를 Player.CustomProperties에 기록 (Awards 씬 등에서 사용)
        SaveLocalPlayerFinalRank();

        SetState(GameState.Ceremony);
        OnCeremonyReady?.Invoke();
    }

    private static void SaveLocalPlayerFinalRank()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;

        int myTeam = PhotonTeamManager.GetLocalTeamRaw();
        if (myTeam == PhotonTeamManager.TeamNone) return;

        if (!RaceFinalRankingStore.TryGetRank(myTeam, out int rank)) return;

        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable { { InGameRaceKeys.FinalRankKey, rank } });
    }

    public void RequestReturnToLobby()
    {
        OnCeremonyReturnRequested?.Invoke();
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
