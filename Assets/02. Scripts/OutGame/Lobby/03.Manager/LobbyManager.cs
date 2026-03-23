using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

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

        StartCoroutine(DeferredInitialUi());
    }

    private IEnumerator DeferredInitialUi()
    {
        yield return null;
        ShowMainScreenRequested?.Invoke();
        NicknameFieldSet?.Invoke(PhotonNetwork.NickName);
        RefreshUIFromNetworkState();
    }

    protected override void OnDestroy()
    {
        if (PhotonServerManager.Instance != null)
            PhotonServerManager.Instance.OnNicknameSet -= OnNicknameSetFromServer;

        if (PhotonTeamManager.Instance != null)
            PhotonTeamManager.Instance.OnAllTeamsAssigned -= HandleAllTeamsAssigned;

        base.OnDestroy();
    }

    public void OnNicknameConfirmed(string nickname)
    {
        PhotonNetwork.NickName = nickname;
        Debug.Log($"[LobbyManager] 닉네임 변경: {nickname}");
    }

    public void RequestMatch(string nicknameTrimmed)
    {
        if (!string.IsNullOrEmpty(nicknameTrimmed))
            PhotonNetwork.NickName = nicknameTrimmed;

        MatchButtonInteractableChanged?.Invoke(false);
        PhotonRoomManager.Instance.JoinRandomRoom();
        MatchingStatusChanged?.Invoke("매칭을 찾고 있습니다...");
        ShowMatchingScreenRequested?.Invoke();
    }

    public void RequestLeaveRoom()
    {
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
            snap.Kind == RoomKind.Random &&
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
        MatchButtonInteractableChanged?.Invoke(PhotonNetwork.IsConnectedAndReady);
        ShowMainScreenRequested?.Invoke();
    }

    private void RefreshUIFromNetworkState()
    {
        MatchButtonInteractableChanged?.Invoke(PhotonNetwork.IsConnectedAndReady);

        if (PhotonNetwork.InRoom && PhotonRoomSnapshotReader.TryGetCurrent(out var snap))
        {
            PlayerCountChanged?.Invoke(snap.PlayerCount, snap.MaxPlayers);
            MatchingStatusChanged?.Invoke("매칭을 찾고 있습니다...");
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
            MatchingStatusChanged?.Invoke("매칭을 찾고 있습니다...");
        }
    }
}
