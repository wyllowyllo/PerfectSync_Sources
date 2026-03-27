using Photon.Pun;
using Photon.Realtime;

public static class PhotonRoomSnapshotReader
{
    public static bool TryGetCurrent(out RoomSnapshot snapshot)
    {
        if (!PhotonNetwork.InRoom)
        {
            snapshot = RoomSnapshot.Invalid;
            return false;
        }

        var room = PhotonNetwork.CurrentRoom;
        snapshot = RoomSnapshot.Create(
            room.Name,
            room.PlayerCount,
            room.MaxPlayers,
            MapKind(room));
        return true;
    }

    private static RoomKind MapKind(Room room)
    {
        if (room.CustomProperties == null ||
            !room.CustomProperties.TryGetValue(PhotonRoomTypes.Key, out object v))
            return RoomKind.Unknown;

        var s = v as string;
        if (s == null) return RoomKind.Unknown;

        return s switch
        {
            PhotonRoomTypes.Lobby => RoomKind.Lobby,
            PhotonRoomTypes.Game => RoomKind.Game,
            _ => RoomKind.Unknown
        };
    }
}
