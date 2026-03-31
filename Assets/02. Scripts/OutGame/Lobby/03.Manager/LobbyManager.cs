using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(LobbyPhotonBridge), typeof(LobbyStartSequence))]
public class LobbyManager : SingletonMonoBehaviour<LobbyManager>
{
    protected override bool PersistAcrossScenes => false;

    public event Action ShowMainScreenRequested;
    public event Action ShowMatchingScreenRequested;
    public event Action<bool> MatchButtonInteractableChanged;
    public event Action<string> MatchingStatusChanged;
    public event Action<int, int> PlayerCountChanged;
    public event Action<bool> LeaveButtonInteractableChanged;
    public event Action<string> NicknameFieldSet;

    private LobbyStartSequence _startSequence;
    private bool _isGameStarting;
    private bool _pendingQueueAfterLobbyJoin;
    private string _pendingNicknameForQueue;

    protected override void Awake()
    {
        base.Awake();
        _startSequence = GetComponent<LobbyStartSequence>();
    }

    private void Start()
    {
        if (PhotonServerManager.Instance != null)
            PhotonServerManager.Instance.OnNicknameSet += OnNicknameSetFromServer;

        if (PhotonTeamManager.Instance != null)
            PhotonTeamManager.Instance.OnAllTeamsAssigned += HandleAllTeamsAssigned;

        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined += HandleLobbyRoomJoined;

        StartCoroutine(DeferredInitialUi());
    }

    private IEnumerator DeferredInitialUi()
    {
        yield return null;
        InGameLocalPlayerPropertyReset.ApplyForLobbyScene(clearTeamBecauseNotInRoom: !PhotonNetwork.InRoom);
        ShowMainScreenRequested?.Invoke();
        NicknameFieldSet?.Invoke(PhotonNetwork.NickName);
        RefreshUIFromNetworkState();

        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.EnsureInLobbyWhenConnected();
    }

    protected override void OnDestroy()
    {
        if (PhotonServerManager.Instance != null)
            PhotonServerManager.Instance.OnNicknameSet -= OnNicknameSetFromServer;

        if (PhotonTeamManager.Instance != null)
            PhotonTeamManager.Instance.OnAllTeamsAssigned -= HandleAllTeamsAssigned;

        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined -= HandleLobbyRoomJoined;

        base.OnDestroy();
    }

    public void OnNicknameConfirmed(string nickname)
    {
        PhotonNetwork.NickName = nickname;
        NicknameFieldSet?.Invoke(PhotonNetwork.NickName);
    }

    public void RequestMatch(string nicknameTrimmed)
    {
        string trimmed = string.IsNullOrEmpty(nicknameTrimmed) ? string.Empty : nicknameTrimmed.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            PhotonNetwork.NickName = trimmed;

        if (!PhotonNetwork.InRoom)
        {
            _pendingQueueAfterLobbyJoin = true;
            _pendingNicknameForQueue = trimmed;
            MatchButtonInteractableChanged?.Invoke(false);
            ShowMatchingScreenRequested?.Invoke();
            MatchingStatusChanged?.Invoke("로비에 입장 중입니다...");
            LobbyRoomConnector.Instance?.EnsureInLobbyWhenConnected();
            return;
        }

        _pendingQueueAfterLobbyJoin = false;
        _pendingNicknameForQueue = null;
        EnqueueMatchmakingAfterInLobby();
    }

    private void HandleLobbyRoomJoined()
    {
        if (!_pendingQueueAfterLobbyJoin)
            return;

        _pendingQueueAfterLobbyJoin = false;
        if (!string.IsNullOrEmpty(_pendingNicknameForQueue))
            PhotonNetwork.NickName = _pendingNicknameForQueue;
        _pendingNicknameForQueue = null;
        EnqueueMatchmakingAfterInLobby();
    }

    private void EnqueueMatchmakingAfterInLobby()
    {
        MatchButtonInteractableChanged?.Invoke(false);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { LobbyMatchmakingKeys.Ready, true } });
        MatchingStatusChanged?.Invoke("매칭 큐에 등록되었습니다...");
        ShowMatchingScreenRequested?.Invoke();
    }

    public void RequestLeaveRoom()
    {
        if (PhotonNetwork.InRoom &&
            PhotonRoomSnapshotReader.TryGetCurrent(out var snap) &&
            snap.Kind == RoomKind.Lobby)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { LobbyMatchmakingKeys.Ready, false } });
            ShowMainScreenRequested?.Invoke();
            MatchButtonInteractableChanged?.Invoke(PhotonNetwork.IsConnectedAndReady);
            return;
        }

        PhotonRoomManager.Instance.LeaveRoom();
    }

    public void HandlePhotonConnectedToMaster()
    {
        MatchButtonInteractableChanged?.Invoke(true);
    }

    public void HandlePhotonDisconnected(DisconnectCause cause)
    {
        MatchButtonInteractableChanged?.Invoke(false);
        ShowMainScreenRequested?.Invoke();
        CancelCountdown();
    }

    public void HandlePhotonJoinedRoom()
    {
        _isGameStarting = false;
        UpdateMatchingUI();
    }

    public void HandlePhotonJoinRandomFailed(short returnCode, string message)
    {
        MatchingStatusChanged?.Invoke("빈 방 없음 → 새 방 생성 중...");
    }

    public void HandlePhotonPlayerEnteredRoom(Player newPlayer)
    {
        UpdateMatchingUI();
    }

    public void HandlePhotonPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.InRoom) return;

        if (PhotonRoomSnapshotReader.TryGetCurrent(out var snap) &&
            snap.Kind == RoomKind.Game &&
            _isGameStarting)
        {
            CancelCountdown();
            MatchingStatusChanged?.Invoke("플레이어가 나갔습니다. 다시 대기 중...");
            LeaveButtonInteractableChanged?.Invoke(true);
        }

        UpdateMatchingUI();
    }

    public void HandlePhotonLeftRoom()
    {
        CancelCountdown();
        _isGameStarting = false;
        InGameLocalPlayerPropertyReset.ApplyForLobbyScene(clearTeamBecauseNotInRoom: true);
        MatchButtonInteractableChanged?.Invoke(PhotonNetwork.IsConnectedAndReady);
        ShowMainScreenRequested?.Invoke();
    }

    private void RefreshUIFromNetworkState()
    {
        MatchButtonInteractableChanged?.Invoke(PhotonNetwork.IsConnectedAndReady);

        if (PhotonNetwork.InRoom && PhotonRoomSnapshotReader.TryGetCurrent(out var snap))
        {
            PlayerCountChanged?.Invoke(snap.PlayerCount, snap.MaxPlayers);
            MatchingStatusChanged?.Invoke(snap.Kind == RoomKind.Lobby ? "로비에 있습니다." : "매칭을 찾고 있습니다...");
        }
    }

    private void OnNicknameSetFromServer(string nickname)
    {
        NicknameFieldSet?.Invoke(nickname);
    }

    private void HandleAllTeamsAssigned()
    {
        if (_startSequence != null && _startSequence.IsRunning) return;
        _isGameStarting = true;
        _startSequence.Begin(msg => MatchingStatusChanged?.Invoke(msg));
    }

    private void CancelCountdown()
    {
        _startSequence?.Cancel();
        _isGameStarting = false;
    }

    private void UpdateMatchingUI()
    {
        if (!PhotonNetwork.InRoom) return;

        if (PhotonRoomSnapshotReader.TryGetCurrent(out var snap))
        {
            PlayerCountChanged?.Invoke(snap.PlayerCount, snap.MaxPlayers);
            MatchingStatusChanged?.Invoke(snap.Kind == RoomKind.Lobby ? "로비에 있습니다." : "매칭을 찾고 있습니다...");
        }
    }
}
