using System;
using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyPartyService : SingletonPunCallbacks<LobbyPartyService>, IOnEventCallback
{
    private const string PartyInviteDebugTag = "[PARTY_INVITE_DEBUG]";
    public event Action<int, string> OnPartyInviteReceived;
    public event Action<bool> OnPartyInviteResponded;
    public event Action<Player> OnPartyPartnerLinked;
    public event Action OnPartyCleared;
    public event Action OnPendingPartyInviteInvalidated;

    private int _outgoingInviteTargetActor = -1;
    private int _pendingInviterActor = -1;

    private Coroutine _partnerWaitCoroutine;
    private const float PartnerWaitTimeoutSeconds = 5f;

    /// <summary>파티 재동기화 대기 시작 (UI: 레디 버튼 비활성화, 안내 표시).</summary>
    public static event Action PartyWaitStarted;
    /// <summary>파티 재동기화 대기 중 매 프레임 남은 시간(초) 브로드캐스트.</summary>
    public static event Action<float> PartyWaitTick;
    /// <summary>파티 재동기화 대기 종료 (성공/타임아웃/중단 모두 포함).</summary>
    public static event Action PartyWaitEnded;

    protected override bool PersistAcrossScenes => true;

    public bool LocalPlayerHasParty =>
        PhotonNetwork.LocalPlayer != null && !string.IsNullOrEmpty(GetPartyId(PhotonNetwork.LocalPlayer));

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        base.OnDisable();
    }

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case PhotonEventCodes.PartyInvite:
                HandlePartyInviteEvent(photonEvent);
                break;

            case PhotonEventCodes.PartyInviteResponse:
                HandlePartyInviteResponseEvent(photonEvent);
                break;

            case PhotonEventCodes.PartyDisband:
                Debug.Log($"{PartyInviteDebugTag} OnEvent PartyDisband → ClearLocalPartyState");
                ClearLocalPartyState();
                break;

            case PhotonEventCodes.PartyStateSync:
                HandlePartyStateSync(photonEvent);
                break;
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (otherPlayer == null)
            return;

        if (otherPlayer.ActorNumber == _pendingInviterActor)
        {
            ClearPendingInviter();
            OnPendingPartyInviteInvalidated?.Invoke();
        }

        if (otherPlayer.ActorNumber == _outgoingInviteTargetActor)
            _outgoingInviteTargetActor = -1;

        if (!PhotonNetwork.InRoom)
            return;

        // 로비 방에서만 파티원 이탈을 "파티 해제"로 간주.
        // 게임 방에서의 이탈은 시상식 후 정상 퇴장이므로 pid를 유지해야 재결합 가능.
        if (!IsLobbyRoom())
            return;

        string myParty = GetPartyId(PhotonNetwork.LocalPlayer);
        if (string.IsNullOrEmpty(myParty))
            return;

        string otherParty = GetPartyId(otherPlayer);
        if (otherParty != myParty)
            return;

        Debug.Log($"{PartyInviteDebugTag} Party partner left room → ClearLocalPartyState actor={otherPlayer.ActorNumber}");
        ClearLocalPartyState();
    }

    // ── 게임 종료 후 로비 복귀 시 파티 재동기화 ──

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        if (!IsLobbyRoom())
            return;

        TryResyncPartyOnLobbyEnter();
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        StopPartnerWait();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);

        if (!IsLobbyRoom() || newPlayer == null)
            return;

        if (_partnerWaitCoroutine == null)
            return;

        TryLinkIfPartyPartner(newPlayer);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (!IsLobbyRoom() || targetPlayer == null || changedProps == null)
            return;

        if (!changedProps.ContainsKey(PhotonTeamManager.PartyIdKey))
            return;

        if (_partnerWaitCoroutine == null)
            return;

        TryLinkIfPartyPartner(targetPlayer);
    }

    private void TryResyncPartyOnLobbyEnter()
    {
        string myPid = GetPartyId(PhotonNetwork.LocalPlayer);
        if (string.IsNullOrEmpty(myPid))
            return;

        // 1) 이미 로비 방에 파티원이 있으면 즉시 링크
        if (TryGetPartyPartner(out Player partner))
        {
            Debug.Log($"{PartyInviteDebugTag} Party resync: partner already present → re-link actor={partner.ActorNumber}");
            OnPartyPartnerLinked?.Invoke(partner);
            return;
        }

        // 2) 아직 안 들어왔으면 기다림 모드 (5초 타임아웃)
        Debug.Log($"{PartyInviteDebugTag} Party resync: waiting for partner (timeout {PartnerWaitTimeoutSeconds}s)");
        StartPartnerWait();
    }

    private void StartPartnerWait()
    {
        if (_partnerWaitCoroutine != null)
            StopCoroutine(_partnerWaitCoroutine);
        _partnerWaitCoroutine = StartCoroutine(WaitForPartnerRoutine());
    }

    private void StopPartnerWait()
    {
        if (_partnerWaitCoroutine != null)
        {
            StopCoroutine(_partnerWaitCoroutine);
            _partnerWaitCoroutine = null;
            PartyWaitEnded?.Invoke();
        }
    }

    private IEnumerator WaitForPartnerRoutine()
    {
        PartyWaitStarted?.Invoke();

        float remaining = PartnerWaitTimeoutSeconds;
        while (remaining > 0f)
        {
            PartyWaitTick?.Invoke(remaining);
            yield return null;
            remaining -= Time.deltaTime;
        }

        _partnerWaitCoroutine = null;
        PartyWaitEnded?.Invoke();

        // 타임아웃 → 파티원이 안 돌아옴 → 로컬 파티 정리
        if (PhotonNetwork.InRoom && IsLobbyRoom())
        {
            Debug.Log($"{PartyInviteDebugTag} Partner wait timeout → ClearLocalPartyState");
            ClearLocalPartyState();
        }
    }

    private void TryLinkIfPartyPartner(Player candidate)
    {
        if (candidate == PhotonNetwork.LocalPlayer)
            return;

        string myPid = GetPartyId(PhotonNetwork.LocalPlayer);
        if (string.IsNullOrEmpty(myPid))
        {
            StopPartnerWait();
            return;
        }

        string candidatePid = GetPartyId(candidate);
        if (candidatePid != myPid)
            return;

        Debug.Log($"{PartyInviteDebugTag} Partner resync succeeded actor={candidate.ActorNumber}");
        StopPartnerWait();
        OnPartyPartnerLinked?.Invoke(candidate);
    }

    private static bool IsLobbyRoom()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom?.CustomProperties == null)
            return false;

        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PhotonRoomTypes.Key, out object v)
            && v as string == PhotonRoomTypes.Lobby;
    }

    public bool TryFindPlayerByUserId(string userId, out Player player)
    {
        player = null;
        if (string.IsNullOrEmpty(userId) || !PhotonNetwork.InRoom)
            return false;

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (!string.IsNullOrEmpty(p.UserId) && p.UserId == userId)
            {
                player = p;
                return true;
            }
        }

        return false;
    }

    public bool TryGetPartyPartner(out Player partner)
    {
        partner = null;
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return false;

        string myParty = GetPartyId(PhotonNetwork.LocalPlayer);
        if (string.IsNullOrEmpty(myParty))
            return false;

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (p == PhotonNetwork.LocalPlayer)
                continue;
            if (GetPartyId(p) == myParty)
            {
                partner = p;
                return true;
            }
        }

        return false;
    }

    public bool TrySendPartyInviteByUserId(string userIdInput, out string errorMessage)
    {
        errorMessage = null;
        Debug.Log($"{PartyInviteDebugTag} TrySendPartyInviteByUserId begin");

        if (!PhotonNetwork.InRoom)
        {
            errorMessage = "방에 있지 않습니다.";
            Debug.LogWarning($"{PartyInviteDebugTag} fail: not in room");
            return false;
        }

        string trimmed = (userIdInput ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errorMessage = "User ID를 입력해 주세요.";
            Debug.LogWarning($"{PartyInviteDebugTag} fail: empty input");
            return false;
        }

        if (trimmed.Length > 128)
        {
            errorMessage = "입력이 너무 깁니다.";
            Debug.LogWarning($"{PartyInviteDebugTag} fail: input too long");
            return false;
        }

        if (!TryFindPlayerByUserId(trimmed, out var target))
        {
            errorMessage = "같은 로비에 해당 플레이어가 없습니다.";
            Debug.LogWarning($"{PartyInviteDebugTag} fail: no player matched for '{trimmed}'");
            return false;
        }

        if (target == PhotonNetwork.LocalPlayer)
        {
            errorMessage = "자기 자신은 초대할 수 없습니다.";
            Debug.LogWarning($"{PartyInviteDebugTag} fail: self-invite");
            return false;
        }

        _outgoingInviteTargetActor = target.ActorNumber;
        SendPartyInvite(target);
        return true;
    }

    public bool TryFindPlayerByInviteCode(string inviteCode, out Player player)
    {
        player = null;
        if (string.IsNullOrEmpty(inviteCode) || !PhotonNetwork.InRoom)
            return false;

        string normalizedCode = inviteCode.Trim().ToUpperInvariant();

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p.CustomProperties.TryGetValue("inviteCode", out object codeObj) &&
                codeObj is string code &&
                code == normalizedCode)
            {
                player = p;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 초대 코드로 파티 초대를 보냅니다.
    /// </summary>
    public bool TrySendPartyInviteByInviteCode(string inviteCodeInput, out string errorMessage)
    {
        errorMessage = null;
        Debug.Log($"{PartyInviteDebugTag} TrySendPartyInviteByInviteCode begin");

        if (!PhotonNetwork.InRoom)
        {
            errorMessage = "방에 있지 않습니다.";
            return false;
        }

        string trimmed = (inviteCodeInput ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(trimmed))
        {
            errorMessage = "초대 코드를 입력해 주세요.";
            return false;
        }

        if (trimmed.Length > 10)
        {
            errorMessage = "입력이 너무 깁니다.";
            return false;
        }

        if (!TryFindPlayerByInviteCode(trimmed, out var target))
        {
            errorMessage = "같은 로비에 해당 초대 코드를 가진 플레이어가 없습니다.";
            return false;
        }

        if (target == PhotonNetwork.LocalPlayer)
        {
            errorMessage = "자기 자신은 초대할 수 없습니다.";
            return false;
        }

        _outgoingInviteTargetActor = target.ActorNumber;
        SendPartyInvite(target);
        return true;
    }

    public void RespondToPendingPartyInvite(bool accept)
    {
        if (_pendingInviterActor < 0 || !PhotonNetwork.InRoom)
        {
            ClearPendingInviter();
            return;
        }

        Player inviter = PhotonNetwork.CurrentRoom?.GetPlayer(_pendingInviterActor);
        ClearPendingInviter();

        if (inviter == null)
            return;

        SendPartyInviteResponse(inviter, accept);
    }

    private void ClearPendingInviter()
    {
        _pendingInviterActor = -1;
    }

    private void HandlePartyInviteEvent(EventData photonEvent)
    {
        if (photonEvent.CustomData is not Hashtable ht ||
            !ht.TryGetValue("fromUserId", out object fromObj))
            return;

        string fromUserId = fromObj as string ?? string.Empty;
        int fromActor = -1;
        if (ht.TryGetValue("fromActor", out object acObj))
            fromActor = acObj is int ia ? ia : Convert.ToInt32(acObj);

        if (fromActor < 0 && TryFindPlayerByUserId(fromUserId, out var p))
            fromActor = p.ActorNumber;

        if (fromActor < 0)
            return;

        _pendingInviterActor = fromActor;
        OnPartyInviteReceived?.Invoke(fromActor, fromUserId);
    }

    private void HandlePartyInviteResponseEvent(EventData photonEvent)
    {
        if (photonEvent.CustomData is not Hashtable rht ||
            !rht.TryGetValue("accepted", out object accObj) ||
            !rht.TryGetValue("fromActor", out object respActorObj))
            return;

        int respondentActor = respActorObj is int ra ? ra : Convert.ToInt32(respActorObj);
        bool accepted = accObj is bool b && b;

        if (_outgoingInviteTargetActor < 0 || respondentActor != _outgoingInviteTargetActor)
            return;

        _outgoingInviteTargetActor = -1;
        OnPartyInviteResponded?.Invoke(accepted);

        if (!accepted)
            return;

        Player target = PhotonNetwork.CurrentRoom?.GetPlayer(respondentActor);
        if (target == null || target == PhotonNetwork.LocalPlayer)
            return;

        FormPartyWithTargetAndRaiseStateSync(target);
    }

    private void FormPartyWithTargetAndRaiseStateSync(Player target)
    {
        LeavePartyAndNotifyPartner();

        string partyId = "P-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string a = string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId)
            ? PhotonNetwork.LocalPlayer.NickName
            : PhotonNetwork.LocalPlayer.UserId;
        string b = string.IsNullOrEmpty(target.UserId) ? target.NickName : target.UserId;
        string membersJson = PartyMembersJson.Build(a, b);

        Debug.Log($"{PartyInviteDebugTag} ApplyPartyState local partyId={partyId} membersJson={membersJson}");
        ApplyPartyState(partyId, membersJson);

        var content = new Hashtable
        {
            { "partyId", partyId },
            { "membersJson", membersJson },
            { "fromActor", PhotonNetwork.LocalPlayer.ActorNumber }
        };
        var opts = new RaiseEventOptions { TargetActors = new[] { target.ActorNumber } };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.PartyStateSync, content, opts, SendOptions.SendReliable);
        Debug.Log($"{PartyInviteDebugTag} RaiseEvent PartyStateSync → actor {target.ActorNumber}");

        OnPartyPartnerLinked?.Invoke(target);
    }

    private void HandlePartyStateSync(EventData photonEvent)
    {
        if (photonEvent.CustomData is not Hashtable syncHt ||
            !syncHt.TryGetValue("partyId", out object pidObj) ||
            !syncHt.TryGetValue("membersJson", out object mjObj) ||
            !syncHt.TryGetValue("fromActor", out object actorObj))
        {
            Debug.LogWarning($"{PartyInviteDebugTag} PartyStateSync malformed payload");
            return;
        }

        string partyId = pidObj as string;
        string membersJson = mjObj as string;
        if (string.IsNullOrEmpty(partyId) || string.IsNullOrEmpty(membersJson))
        {
            Debug.LogWarning($"{PartyInviteDebugTag} PartyStateSync empty partyId or membersJson");
            return;
        }

        int fromActor = actorObj is int ia ? ia : Convert.ToInt32(actorObj);
        Debug.Log($"{PartyInviteDebugTag} HandlePartyStateSync fromActor={fromActor} partyId={partyId}");

        LeavePartyAndNotifyPartner();

        ApplyPartyState(partyId, membersJson);

        Player inviter = PhotonNetwork.CurrentRoom?.GetPlayer(fromActor);
        if (inviter == null)
        {
            Debug.LogWarning($"{PartyInviteDebugTag} PartyStateSync inviter not found actor={fromActor}");
            return;
        }

        OnPartyPartnerLinked?.Invoke(inviter);
    }

    private void SendPartyInvite(Player target)
    {
        string from = string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId)
            ? PhotonNetwork.LocalPlayer.NickName
            : PhotonNetwork.LocalPlayer.UserId;

        var content = new Hashtable
        {
            { "fromUserId", from },
            { "fromActor", PhotonNetwork.LocalPlayer.ActorNumber }
        };
        var opts = new RaiseEventOptions { TargetActors = new[] { target.ActorNumber } };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.PartyInvite, content, opts, SendOptions.SendReliable);
    }

    public void SendPartyInviteResponse(Player toPlayer, bool accepted)
    {
        var content = new Hashtable
        {
            { "accepted", accepted },
            { "fromActor", PhotonNetwork.LocalPlayer.ActorNumber }
        };
        var opts = new RaiseEventOptions { TargetActors = new[] { toPlayer.ActorNumber } };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.PartyInviteResponse, content, opts, SendOptions.SendReliable);
    }

    public void FormPartyWith(Player other)
    {
        string partyId = "P-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string a = string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId)
            ? PhotonNetwork.LocalPlayer.NickName
            : PhotonNetwork.LocalPlayer.UserId;
        string b = string.IsNullOrEmpty(other.UserId) ? other.NickName : other.UserId;
        ApplyPartyState(partyId, PartyMembersJson.Build(a, b));
    }

    public void ApplyPartyState(string partyId, string membersJson)
    {
        var ht = new Hashtable
        {
            { PhotonTeamManager.PartyIdKey, partyId },
            { LobbyMatchmakingKeys.PartyMembers, membersJson },
            { LobbyMatchmakingKeys.Ready, false }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
    }

    public void LeavePartyAndNotifyPartner()
    {
        if (!PhotonNetwork.InRoom)
        {
            ClearLocalPartyState();
            return;
        }

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p == PhotonNetwork.LocalPlayer)
                continue;
            if (GetPartyId(p) == GetPartyId(PhotonNetwork.LocalPlayer) && !string.IsNullOrEmpty(GetPartyId(p)))
            {
                var content = new Hashtable { { "reason", "leave" } };
                var opts = new RaiseEventOptions { TargetActors = new[] { p.ActorNumber } };
                PhotonNetwork.RaiseEvent(PhotonEventCodes.PartyDisband, content, opts, SendOptions.SendReliable);
                break;
            }
        }

        ClearLocalPartyState();
    }

    private void ClearLocalPartyState()
    {
        if (PhotonNetwork.LocalPlayer == null)
            return;

        Debug.Log($"{PartyInviteDebugTag} ClearLocalPartyState");
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { PhotonTeamManager.PartyIdKey, null },
            { LobbyMatchmakingKeys.PartyMembers, null },
            { LobbyMatchmakingKeys.Ready, false }
        });
        OnPartyCleared?.Invoke();
    }

    private static string GetPartyId(Player player)
    {
        return player.CustomProperties.TryGetValue(PhotonTeamManager.PartyIdKey, out object v) ? v as string : null;
    }
}
