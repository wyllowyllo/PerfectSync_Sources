using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyPartyService : SingletonPunCallbacks<LobbyPartyService>, IOnEventCallback
{
    public event Action<string> OnPartyInviteReceived;
    public event Action<bool> OnPartyInviteResponded;

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
            case LobbyPhotonEventCodes.PartyInvite:
                if (photonEvent.CustomData is Hashtable ht &&
                    ht.TryGetValue("fromUserId", out object fromObj))
                    OnPartyInviteReceived?.Invoke(fromObj as string);
                break;

            case LobbyPhotonEventCodes.PartyInviteResponse:
                if (photonEvent.CustomData is Hashtable rht &&
                    rht.TryGetValue("accepted", out object acc))
                    OnPartyInviteResponded?.Invoke(acc is bool b && b);
                break;

            case LobbyPhotonEventCodes.PartyDisband:
                ClearLocalPartyState();
                break;
        }
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

    public void SendPartyInvite(string targetUserId)
    {
        if (!TryFindPlayerByUserId(targetUserId, out var target))
            return;

        string from = string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId)
            ? PhotonNetwork.LocalPlayer.NickName
            : PhotonNetwork.LocalPlayer.UserId;

        var content = new Hashtable { { "fromUserId", from } };
        var opts = new RaiseEventOptions { TargetActors = new[] { target.ActorNumber } };
        PhotonNetwork.RaiseEvent(LobbyPhotonEventCodes.PartyInvite, content, opts, SendOptions.SendReliable);
    }

    public void SendPartyInviteResponse(Player toPlayer, bool accepted)
    {
        var content = new Hashtable { { "accepted", accepted } };
        var opts = new RaiseEventOptions { TargetActors = new[] { toPlayer.ActorNumber } };
        PhotonNetwork.RaiseEvent(LobbyPhotonEventCodes.PartyInviteResponse, content, opts, SendOptions.SendReliable);
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
                PhotonNetwork.RaiseEvent(LobbyPhotonEventCodes.PartyDisband, content, opts, SendOptions.SendReliable);
                break;
            }
        }

        ClearLocalPartyState();
    }

    private static void ClearLocalPartyState()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { PhotonTeamManager.PartyIdKey, null },
            { LobbyMatchmakingKeys.PartyMembers, null },
            { LobbyMatchmakingKeys.Ready, false }
        });
    }

    private static string GetPartyId(Player player)
    {
        return player.CustomProperties.TryGetValue(PhotonTeamManager.PartyIdKey, out object v) ? v as string : null;
    }
}
