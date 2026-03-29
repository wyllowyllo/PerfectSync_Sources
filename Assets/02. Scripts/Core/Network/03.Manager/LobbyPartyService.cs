using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyPartyService : SingletonPunCallbacks<LobbyPartyService>, IOnEventCallback
{
    // PARTY_INVITE_DEBUG_REMOVE: 아래 태그·Debug.Log 일괄 삭제
    private const string PartyInviteDebugTag = "[PARTY_INVITE_DEBUG]";

    public event Action<string> OnPartyInviteReceived;
    public event Action<bool> OnPartyInviteResponded;
    /// <summary>파티가 맺어진 뒤 상대 <see cref="Player"/> (닉네임 표시용).</summary>
    public event Action<Player> OnPartyPartnerLinked;
    /// <summary>로컬 파티 상태가 비워졌을 때 (해산·퇴장 등).</summary>
    public event Action OnPartyCleared;

    protected override bool PersistAcrossScenes => true;

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
                if (photonEvent.CustomData is Hashtable ht &&
                    ht.TryGetValue("fromUserId", out object fromObj))
                    OnPartyInviteReceived?.Invoke(fromObj as string);
                break;

            case PhotonEventCodes.PartyInviteResponse:
                if (photonEvent.CustomData is Hashtable rht &&
                    rht.TryGetValue("accepted", out object acc))
                    OnPartyInviteResponded?.Invoke(acc is bool b && b);
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
        if (otherPlayer == null || !PhotonNetwork.InRoom)
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

    public bool TryFindPlayerByUserId(string userId, out Player player)
    {
        player = null;
        if (string.IsNullOrEmpty(userId) || !PhotonNetwork.InRoom)
            return false;

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p.UserId == userId || (string.IsNullOrEmpty(p.UserId) && p.NickName == userId))
            {
                player = p;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 로비에 있는 상대를 User ID(또는 User ID 미설정 시 닉네임)로 찾아 파티를 맺고, 커스텀 프로퍼티를 양쪽에 맞춥니다.
    /// </summary>
    public bool TryFormPartyWithUserId(string userIdInput, out string errorMessage)
    {
        errorMessage = null;
        Debug.Log($"{PartyInviteDebugTag} TryFormPartyWithUserId begin");

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
        return true;
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

    public void SendPartyInvite(string targetUserId)
    {
        if (!TryFindPlayerByUserId(targetUserId, out var target))
            return;

        string from = string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId)
            ? PhotonNetwork.LocalPlayer.NickName
            : PhotonNetwork.LocalPlayer.UserId;

        var content = new Hashtable { { "fromUserId", from } };
        var opts = new RaiseEventOptions { TargetActors = new[] { target.ActorNumber } };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.PartyInvite, content, opts, SendOptions.SendReliable);
    }

    public void SendPartyInviteResponse(Player toPlayer, bool accepted)
    {
        var content = new Hashtable { { "accepted", accepted } };
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
