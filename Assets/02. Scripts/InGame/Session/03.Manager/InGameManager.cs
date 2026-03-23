using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[RequireComponent(typeof(PhotonView))]
public class InGameManager : SingletonPunCallbacks<InGameManager>
{
    protected override bool PersistAcrossScenes => false;

    [Header("Settings")]
    [SerializeField] private float _introDuration = 2f;
    [SerializeField] private int _countdownSeconds = 3;

    [Header("References")]
    [SerializeField] private PlayerSpawner _playerSpawner;

    [Header("Debug")]
    [Tooltip("InGame 진입 직후(팀 배정 완료 시점) 방/파티/플레이어/게임 상태 로그")]
    [SerializeField] private bool _logRoomAndPartyOnEnter = true;

    public GameState CurrentState { get; private set; } = GameState.Loading;
    public event Action<GameState> OnGameStateChanged;
    public event Action<int> OnRaceCountdownTick;

    private GameObject _localPlayer;

    protected override void Awake()
    {
        base.Awake();
        InGameLocalPlayerPropertyReset.ApplyForLobbyScene(clearTeamBecauseNotInRoom: false);
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            PhotonNetwork.InRoom &&
            PhotonTeamManager.GetLocalTeamRaw() != PhotonTeamManager.TeamNone
        );

        if (_logRoomAndPartyOnEnter)
            LogInGameEnterContext();

        Debug.Log($"[InGameManager] 팀 확인 완료: {PhotonTeamManager.GetLocalTeamRaw()}");

        _localPlayer = _playerSpawner.SpawnByTeam();

        if (_localPlayer != null)
        {
            Debug.Log("[InGameManager] 로컬 플레이어 스폰 완료. Ready 전송.");
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { InGameRaceKeys.ReadyKey, true } });
        }
        else
        {
            Debug.LogError("[InGameManager] 로컬 플레이어 스폰 실패.");
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(InGameRaceKeys.ReadyKey)) return;
        if (CurrentState != GameState.Loading) return;

        Debug.Log($"[InGameManager] OnPlayerPropertiesUpdate: {targetPlayer.NickName} - {changedProps[InGameRaceKeys.ReadyKey]}");

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
            if (!player.CustomProperties.TryGetValue(InGameRaceKeys.ReadyKey, out object readyObj) || !(bool)readyObj)
                return false;
        }
        return PhotonNetwork.PlayerList.Length > 0;
    }

    private void LogInGameEnterContext()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[InGameManager] ========== InGame 입장 (방·파티·상태) ==========");
        sb.AppendLine($"  GameState={CurrentState}");
        sb.AppendLine($"  Scene={SceneManager.GetActiveScene().name}");
        sb.AppendLine($"  Photon connected={PhotonNetwork.IsConnected} inRoom={PhotonNetwork.InRoom} masterClient={PhotonNetwork.IsMasterClient} syncScene={PhotonNetwork.AutomaticallySyncScene} region={PhotonNetwork.CloudRegion}");

        if (PhotonNetwork.LocalPlayer != null)
        {
            var lp = PhotonNetwork.LocalPlayer;
            sb.AppendLine($"  Local actorNr={lp.ActorNumber} nick={lp.NickName} team={PhotonTeamManager.GetLocalTeamRaw()}");
        }
        else
            sb.AppendLine("  LocalPlayer=null");

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            sb.AppendLine("  CurrentRoom=null");
            sb.AppendLine("=====================================================");
            Debug.Log(sb.ToString());
            return;
        }

        var room = PhotonNetwork.CurrentRoom;
        sb.AppendLine($"  Room name={room.Name} players={room.PlayerCount}/{room.MaxPlayers} open={room.IsOpen} visible={room.IsVisible}");

        if (PhotonRoomSnapshotReader.TryGetCurrent(out var snap) && snap.IsValid)
            sb.AppendLine($"  RoomSnapshot kind={snap.Kind} count={snap.PlayerCount}/{snap.MaxPlayers}");
        else
            sb.AppendLine("  RoomSnapshot invalid/unknown kind");

        sb.AppendLine("  Room.CustomProperties:");
        if (room.CustomProperties != null && room.CustomProperties.Count > 0)
        {
            foreach (System.Collections.DictionaryEntry kv in room.CustomProperties)
                sb.AppendLine($"    {kv.Key} = {kv.Value}");
        }
        else
            sb.AppendLine("    (없음)");

        if (PhotonPartyManager.Instance != null)
        {
            var pm = PhotonPartyManager.Instance;
            sb.AppendLine($"  PhotonPartyManager PartyCode={pm.PartyCode ?? "(null)"} IsInParty={pm.IsInParty} IsPartyLeader={pm.IsPartyLeader}");
        }
        else
            sb.AppendLine("  PhotonPartyManager.Instance=null");

        sb.AppendLine("  PlayerList:");
        var list = PhotonNetwork.PlayerList;
        if (list == null || list.Length == 0)
            sb.AppendLine("    (없음)");
        else
        {
            foreach (var p in list)
            {
                int team = PhotonTeamManager.GetTeamRaw(p);
                sb.AppendLine($"    [{p.ActorNumber}] nick={p.NickName} isLocal={p.IsLocal} team={team}");
                if (p.CustomProperties == null || p.CustomProperties.Count == 0) continue;
                var props = new StringBuilder();
                foreach (System.Collections.DictionaryEntry kv in p.CustomProperties)
                    props.Append($"{kv.Key}={kv.Value} ");
                sb.AppendLine($"      CustomProperties: {props}");
            }
        }

        sb.AppendLine("=====================================================");
        Debug.Log(sb.ToString());
    }

}
