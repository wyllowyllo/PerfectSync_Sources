using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyRoomConnector : SingletonPunCallbacks<LobbyRoomConnector>
{
    private const string DefaultFixedLobbyRoomName = "PerfectSync_Lobby";

    protected override bool PersistAcrossScenes => true;

    [SerializeField] private string _fixedLobbyRoomName = DefaultFixedLobbyRoomName;

    public event Action OnLobbyRoomJoined;

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        TryJoinLobbyRoom();
    }

    public void TryJoinLobbyRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady || PhotonNetwork.InRoom)
            return;

        string name = string.IsNullOrWhiteSpace(_fixedLobbyRoomName)
            ? DefaultFixedLobbyRoomName
            : _fixedLobbyRoomName.Trim();

        var emptyQueue = JsonUtility.ToJson(new MatchQueueDto { entries = new MatchQueueEntryDto[0] });

        var roomOptions = new RoomOptions
        {
            MaxPlayers = byte.MaxValue,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = new Hashtable
            {
                { PhotonRoomTypes.Key, PhotonRoomTypes.Lobby },
                { LobbyRoomPropertyKeys.MatchQueue, emptyQueue }
            },
            CustomRoomPropertiesForLobby = new[] { PhotonRoomTypes.Key }
        };

        PhotonNetwork.JoinOrCreateRoom(name, roomOptions, TypedLobby.Default);
    }

    public void EnsureInLobbyWhenConnected()
    {
        TryJoinLobbyRoom();
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        if (GetCurrentRoomType() == PhotonRoomTypes.Lobby)
            OnLobbyRoomJoined?.Invoke();
    }

    private static string GetCurrentRoomType()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom?.CustomProperties == null)
            return string.Empty;

        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PhotonRoomTypes.Key, out object v)
            ? v as string
            : string.Empty;
    }
}
