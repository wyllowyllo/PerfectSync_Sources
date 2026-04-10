using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// <see cref="MatchQueueManager"/>의 첫 번째 8인 배치 로직과 동일하게,
/// 방 커스텀 프로퍼티의 큐 JSON으로 현재 배치 진행 상황을 미리 볼 때 사용합니다.
/// </summary>
public readonly struct MatchQueueFirstBatchInfo
{
    public readonly int FilledSlots;
    public readonly bool LocalPlayerInBatch;
    public readonly bool BatchComplete;

    public MatchQueueFirstBatchInfo(int filledSlots, bool localInBatch, bool batchComplete)
    {
        FilledSlots = filledSlots;
        LocalPlayerInBatch = localInBatch;
        BatchComplete = batchComplete;
    }
}

public static class MatchQueueBatchPreview
{
    public static bool TryGetFirstBatchInfo(out MatchQueueFirstBatchInfo info)
    {
        info = default;

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom?.CustomProperties == null)
            return false;

        if (!PhotonRoomSnapshotReader.TryGetCurrent(out var snap) || snap.Kind != RoomKind.Lobby)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(LobbyRoomPropertyKeys.MatchQueue, out object q))
            return false;

        string json = q as string;
        if (string.IsNullOrEmpty(json))
            return false;

        MatchQueueDto dto;
        try
        {
            dto = JsonUtility.FromJson<MatchQueueDto>(json);
        }
        catch
        {
            return false;
        }

        if (dto?.entries == null || dto.entries.Length == 0)
            return false;

        string localUid = GetStableUserId(PhotonNetwork.LocalPlayer);
        int slots = 8;
        var acceptedUids = new System.Collections.Generic.HashSet<string>();

        foreach (var entry in dto.entries)
        {
            if (entry?.userIds == null || entry.userIds.Length == 0)
                continue;

            if (entry.userIds.Length > slots)
                continue;

            var resolved = new System.Collections.Generic.List<Player>();
            bool ok = true;
            foreach (var uid in entry.userIds)
            {
                if (string.IsNullOrEmpty(uid))
                {
                    ok = false;
                    break;
                }

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

            slots -= entry.userIds.Length;
            foreach (var uid in entry.userIds)
                acceptedUids.Add(uid);

            if (slots == 0)
            {
                bool localIn = !string.IsNullOrEmpty(localUid) && acceptedUids.Contains(localUid);
                info = new MatchQueueFirstBatchInfo(8, localIn, true);
                return true;
            }
        }

        int filled = 8 - slots;
        bool localInPartial = !string.IsNullOrEmpty(localUid) && acceptedUids.Contains(localUid);
        info = new MatchQueueFirstBatchInfo(filled, localInPartial, false);
        return true;
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

    private static bool IsPlayerMatchReady(Player p)
    {
        return p.CustomProperties.TryGetValue(LobbyMatchmakingKeys.Ready, out object v) && v is bool b && b;
    }

    private static string GetStableUserId(Player player)
    {
        if (player == null)
            return null;
        return string.IsNullOrEmpty(player.UserId) ? null : player.UserId;
    }
}
