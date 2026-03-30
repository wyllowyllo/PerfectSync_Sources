using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class MatchQueueManager : SingletonPunCallbacks<MatchQueueManager>
{
    private const string TypeSolo = "solo";
    private const string TypeParty = "party";
    private const string MatchRoomPrefix = "G-";

    private readonly List<QueueEntryModel> _queue = new();

    protected override bool PersistAcrossScenes => true;

    public override void OnEnable()
    {
        base.OnEnable();
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        _queue.Clear();
        if (IsLobbyRoom() && PhotonNetwork.IsMasterClient)
            LoadQueueFromRoomOrEmpty();
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        _queue.Clear();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        base.OnMasterClientSwitched(newMasterClient);
        if (PhotonNetwork.IsMasterClient && IsLobbyRoom())
        {
            LoadQueueFromRoomOrEmpty();
            TryProcessMatch();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (!IsLobbyRoom() || !PhotonNetwork.IsMasterClient)
            return;

        if (!changedProps.ContainsKey(LobbyMatchmakingKeys.Ready))
            return;

        string uid = GetStableUserId(targetPlayer);
        if (string.IsNullOrEmpty(uid))
            return;

        bool ready = targetPlayer.CustomProperties.TryGetValue(LobbyMatchmakingKeys.Ready, out object r) &&
                     r is bool rb && rb;

        if (!ready)
        {
            RemoveEntriesContainingUserId(uid);
            SaveQueueToRoom();
            TryProcessMatch();
            return;
        }

        TryEnqueueForReadyPlayer(targetPlayer);
        SaveQueueToRoom();
        TryProcessMatch();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);

        if (!IsLobbyRoom() || !PhotonNetwork.IsMasterClient)
            return;

        string uid = GetStableUserId(otherPlayer);
        if (!string.IsNullOrEmpty(uid))
            RemoveEntriesContainingUserId(uid);

        SaveQueueToRoom();
        TryProcessMatch();
    }

    private void LoadQueueFromRoomOrEmpty()
    {
        _queue.Clear();
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LobbyRoomPropertyKeys.MatchQueue, out object q))
            return;

        string json = q as string;
        if (string.IsNullOrEmpty(json))
            return;

        MatchQueueDto dto;
        try
        {
            dto = JsonUtility.FromJson<MatchQueueDto>(json);
        }
        catch
        {
            return;
        }

        if (dto?.entries == null)
            return;

        foreach (var e in dto.entries)
        {
            if (e?.userIds == null || e.userIds.Length == 0)
                continue;
            _queue.Add(new QueueEntryModel { Type = e.type, UserIds = e.userIds });
        }
    }

    private void SaveQueueToRoom()
    {
        if (!PhotonNetwork.IsMasterClient || !IsLobbyRoom())
            return;

        var arr = new MatchQueueEntryDto[_queue.Count];
        for (int i = 0; i < _queue.Count; i++)
        {
            var e = _queue[i];
            arr[i] = new MatchQueueEntryDto { type = e.Type, userIds = e.UserIds };
        }

        string json = JsonUtility.ToJson(new MatchQueueDto { entries = arr });
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { LobbyRoomPropertyKeys.MatchQueue, json } });
    }

    private void TryEnqueueForReadyPlayer(Player player)
    {
        string uid = GetStableUserId(player);
        if (string.IsNullOrEmpty(uid) || QueueContainsUserId(uid))
            return;

        string partyId = GetPartyId(player);
        if (string.IsNullOrEmpty(partyId))
        {
            _queue.Add(new QueueEntryModel { Type = TypeSolo, UserIds = new[] { uid } });
            return;
        }

        Player partner = FindPartyPartner(partyId, player);
        if (partner == null)
            return;

        if (!IsPlayerMatchReady(partner))
            return;

        string uidB = GetStableUserId(partner);
        if (string.IsNullOrEmpty(uidB) || QueueContainsUserId(uidB))
            return;

        string a = string.CompareOrdinal(uid, uidB) < 0 ? uid : uidB;
        string b = string.CompareOrdinal(uid, uidB) < 0 ? uidB : uid;
        _queue.Add(new QueueEntryModel { Type = TypeParty, UserIds = new[] { a, b } });
    }

    private static Player FindPartyPartner(string partyId, Player self)
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p == self)
                continue;
            if (GetPartyId(p) == partyId)
                return p;
        }

        return null;
    }

    private static bool IsPlayerMatchReady(Player p)
    {
        return p.CustomProperties.TryGetValue(LobbyMatchmakingKeys.Ready, out object v) && v is bool b && b;
    }

    private static string GetPartyId(Player player)
    {
        if (player.CustomProperties.TryGetValue(PhotonTeamManager.PartyIdKey, out object pid))
            return pid as string;
        return null;
    }

    private static string GetStableUserId(Player player)
    {
        if (player == null)
            return null;
        return string.IsNullOrEmpty(player.UserId) ? null : player.UserId;
    }

    private bool QueueContainsUserId(string uid)
    {
        foreach (var e in _queue)
        {
            foreach (var u in e.UserIds)
            {
                if (u == uid)
                    return true;
            }
        }

        return false;
    }

    private void RemoveEntriesContainingUserId(string uid)
    {
        for (int i = _queue.Count - 1; i >= 0; i--)
        {
            foreach (var u in _queue[i].UserIds)
            {
                if (u == uid)
                {
                    _queue.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private void TryProcessMatch()
    {
        if (!PhotonNetwork.IsMasterClient || !IsLobbyRoom())
            return;

        while (true)
        {
            if (!TryBuildBatchOfEight(out List<QueueEntryModel> batch, out List<Player> players))
                return;

            string roomName = MatchRoomPrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
            var actorNums = new int[players.Count];
            for (int i = 0; i < players.Count; i++)
                actorNums[i] = players[i].ActorNumber;

            var content = new object[] { roomName };
            var opts = new RaiseEventOptions { TargetActors = actorNums };
            PhotonNetwork.RaiseEvent(PhotonEventCodes.MatchConfirmed, content, opts, SendOptions.SendReliable);

            foreach (var e in batch)
                _queue.Remove(e);

            SaveQueueToRoom();
        }
    }

    private bool TryBuildBatchOfEight(out List<QueueEntryModel> batch, out List<Player> players)
    {
        batch = new List<QueueEntryModel>();
        players = new List<Player>();
        int slots = 8;

        foreach (var entry in _queue)
        {
            if (entry.UserIds == null || entry.UserIds.Length == 0)
                continue;

            if (entry.UserIds.Length > slots)
                continue;

            var resolved = new List<Player>();
            bool ok = true;
            foreach (var uid in entry.UserIds)
            {
                var p = FindPlayerByUserId(uid);
                if (p == null || !IsPlayerMatchReady(p))
                {
                    ok = false;
                    break;
                }

                resolved.Add(p);
            }

            if (!ok)
                continue;

            slots -= entry.UserIds.Length;
            batch.Add(entry);
            players.AddRange(resolved);

            if (slots == 0)
                return players.Count == 8;
        }

        batch.Clear();
        players.Clear();
        return false;
    }

    private static Player FindPlayerByUserId(string uid)
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (GetStableUserId(p) == uid)
                return p;
        }

        return null;
    }

    private static bool IsLobbyRoom()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom?.CustomProperties == null)
            return false;

        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PhotonRoomTypes.Key, out object t) &&
               (string)t == PhotonRoomTypes.Lobby;
    }

    private sealed class QueueEntryModel
    {
        public string Type;
        public string[] UserIds;
    }
}
