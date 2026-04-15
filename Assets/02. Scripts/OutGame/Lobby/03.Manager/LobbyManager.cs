using System;
using System.Collections;
using System.Collections.Generic;
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
    public event Action<string> InviteCodeChanged;

    [Header("Firebase 커스터마이징 복원 · 로비 로컬 캐릭터 프리뷰")]
    [SerializeField] private CustomizationPartItemsActivator[] _partItemsActivators;

    /// <summary>로비에서 로컬 플레이어 캐릭터 프리뷰에 커스터마이징을 적용하는 Activator 목록. 모든 원소는 본인 프리뷰용.</summary>
    public IReadOnlyList<CustomizationPartItemsActivator> PartItemsActivators => _partItemsActivators;

    [Header("Matchmaking")]
    [Tooltip("매칭 UI가 켜진 뒤, Photon에 Ready=true를 보내 큐에 올리기까지 대기하는 시간(초). 이 동안에는 큐에 포함되지 않습니다.")]
    [SerializeField] [Min(0f)] private float _secondsBeforeSendingReady = 3f;

    /// <summary>인스펙터의 초 단위 지연과 동일. UI(예: LobbyMatchStatusUI)에서 Looking 구간과 맞출 때 사용합니다.</summary>
    public float SecondsBeforeSendingReady => _secondsBeforeSendingReady;

    /// <summary>Ready를 아직 보내지 않고 지연 중이면 true (취소 시 코루틴 중단).</summary>
    public bool IsAwaitingReadyDelay => _awaitingReadyDelay;

    private LobbyStartSequence _startSequence;
    private bool _isGameStarting;
    private bool _pendingQueueAfterLobbyJoin;
    private string _pendingNicknameForQueue;
    private Coroutine _delayedReadyCoroutine;
    private bool _awaitingReadyDelay;

    private string _currentInviteCode;
    public string CurrentInviteCode => _currentInviteCode;

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

        // 초대 코드 발급
        AssignNewInviteCode();

        // Firebase 커스터마이징 + 닉네임 로드
        StartCoroutine(CoLoadCustomizationFromFirebase());
    }

    protected override void OnDestroy()
    {
        if (PhotonServerManager.Instance != null)
            PhotonServerManager.Instance.OnNicknameSet -= OnNicknameSetFromServer;

        if (PhotonTeamManager.Instance != null)
            PhotonTeamManager.Instance.OnAllTeamsAssigned -= HandleAllTeamsAssigned;

        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined -= HandleLobbyRoomJoined;

        StopDelayedReadyIfAny();
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
        if (_delayedReadyCoroutine != null)
            StopCoroutine(_delayedReadyCoroutine);

        MatchButtonInteractableChanged?.Invoke(false);

        // ── 파티 상태: Ready만 즉시 전송, 매칭 화면은 파티원 전원 Ready 시 전환 ──
        if (IsLocalPlayerInParty())
        {
            _awaitingReadyDelay = false;

            if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null)
                PhotonNetwork.LocalPlayer.SetCustomProperties(
                    new Hashtable { { LobbyMatchmakingKeys.Ready, true } });

            if (AreAllPartyMembersReady())
                ShowMatchingScreenRequested?.Invoke();

            return;
        }

        // ── 솔로: 기존 동작 (매칭 화면 먼저 → 딜레이 후 Ready 전송) ──
        ShowMatchingScreenRequested?.Invoke();
        _awaitingReadyDelay = true;
        MatchingStatusChanged?.Invoke(string.Empty);

        _delayedReadyCoroutine = StartCoroutine(CoSendReadyAfterDelay());
    }

    private IEnumerator CoSendReadyAfterDelay()
    {
        if (_secondsBeforeSendingReady > 0f)
            yield return new WaitForSeconds(_secondsBeforeSendingReady);

        _delayedReadyCoroutine = null;

        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            _awaitingReadyDelay = false;
            yield break;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { LobbyMatchmakingKeys.Ready, true } });
        _awaitingReadyDelay = false;
        MatchingStatusChanged?.Invoke("매칭 큐에 등록되었습니다...");
    }

    private void StopDelayedReadyIfAny()
    {
        if (_delayedReadyCoroutine != null)
        {
            StopCoroutine(_delayedReadyCoroutine);
            _delayedReadyCoroutine = null;
        }

        _awaitingReadyDelay = false;
    }

    public void CancelMatchReady()
    {
        StopDelayedReadyIfAny();

        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return;

        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable { { LobbyMatchmakingKeys.Ready, false } });
        MatchingStatusChanged?.Invoke(string.Empty);
        MatchButtonInteractableChanged?.Invoke(true);
        ShowMainScreenRequested?.Invoke();
    }

    public void RequestLeaveRoom()
    {
        if (PhotonNetwork.InRoom &&
            PhotonRoomSnapshotReader.TryGetCurrent(out var snap) &&
            snap.Kind == RoomKind.Lobby)
        {
            StopDelayedReadyIfAny();
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
        StopDelayedReadyIfAny();
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
        StopDelayedReadyIfAny();
        CancelCountdown();
        _isGameStarting = false;
        InGameLocalPlayerPropertyReset.ApplyForLobbyScene(clearTeamBecauseNotInRoom: true);
        MatchButtonInteractableChanged?.Invoke(PhotonNetwork.IsConnectedAndReady);
        ShowMainScreenRequested?.Invoke();
    }

    public void HandlePhotonPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps == null || !changedProps.ContainsKey(LobbyMatchmakingKeys.Ready))
            return;

        if (!IsLocalPlayerInParty())
            return;

        string localPartyId = GetLocalPartyId();
        string targetPartyId = GetPartyId(targetPlayer);
        if (localPartyId != targetPartyId)
            return;

        bool localReady = IsPlayerMatchReady(PhotonNetwork.LocalPlayer);
        if (!localReady)
            return;

        bool targetReady = targetPlayer.CustomProperties.TryGetValue(
            LobbyMatchmakingKeys.Ready, out object v) && v is bool b && b;

        if (targetReady && AreAllPartyMembersReady())
        {
            ShowMatchingScreenRequested?.Invoke();
        }
        else if (!targetReady && targetPlayer != PhotonNetwork.LocalPlayer)
        {
            ShowMainScreenRequested?.Invoke();
            MatchButtonInteractableChanged?.Invoke(true);
        }
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
        _startSequence.Begin();
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

    private void AssignNewInviteCode()
    {
        _currentInviteCode = InviteCodeGenerator.Generate();
        InviteCodeChanged?.Invoke(_currentInviteCode);

        if (PhotonNetwork.LocalPlayer != null)
        {
            var ht = new Hashtable { { "inviteCode", _currentInviteCode } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
        }

        Debug.Log($"[Lobby] 새 초대 코드 발급: {_currentInviteCode}");
    }

    private IEnumerator CoLoadCustomizationFromFirebase()
    {
        if (!FirebaseInitializer.Instance.IsInitialized)
            yield break;

        var task = FirebaseCustomizationRepository.Load();

        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            Debug.LogError($"[Lobby] 커스터마이징 로드 실패: {task.Exception}");
            yield break;
        }

        CustomizationSaveData data = task.Result;

        // 닉네임 복원
        if (!string.IsNullOrEmpty(data.Nickname))
        {
            PhotonNetwork.NickName = data.Nickname;
            NicknameFieldSet?.Invoke(data.Nickname);
        }

        // 커스터마이징 파츠 복원 (배열의 모든 Activator에 동일 적용)
        if (_partItemsActivators != null && _partItemsActivators.Length > 0)
        {
            foreach (CharacterCustomizationPart part in System.Enum.GetValues(typeof(CharacterCustomizationPart)))
            {
                int index = data.GetPartIndex(part);
                foreach (var activator in _partItemsActivators)
                {
                    if (activator != null)
                        activator.SetPartItemIndex(part, index);
                }
                CustomizationPhotonKeys.SetLocalPlayerSlotIndex(part, index);
            }

            Debug.Log("[Lobby] Firebase 커스터마이징 복원 완료");
        }
    }

    // ── 파티 매칭 헬퍼 ──

    private bool IsLocalPlayerInParty()
    {
        return LobbyPartyService.Instance != null &&
               LobbyPartyService.Instance.LocalPlayerHasParty;
    }

    private bool AreAllPartyMembersReady()
    {
        if (PhotonNetwork.LocalPlayer == null) return false;

        string partyId = GetLocalPartyId();
        if (string.IsNullOrEmpty(partyId)) return false;

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (GetPartyId(p) != partyId) continue;
            if (!IsPlayerMatchReady(p)) return false;
        }
        return true;
    }

    private static bool IsPlayerMatchReady(Player p)
    {
        return p != null &&
               p.CustomProperties.TryGetValue(LobbyMatchmakingKeys.Ready, out object v) &&
               v is bool b && b;
    }

    private static string GetLocalPartyId()
    {
        return GetPartyId(PhotonNetwork.LocalPlayer);
    }

    private static string GetPartyId(Player player)
    {
        if (player == null) return null;
        return player.CustomProperties.TryGetValue(PhotonTeamManager.PartyIdKey, out object pid)
            ? pid as string
            : null;
    }
}
