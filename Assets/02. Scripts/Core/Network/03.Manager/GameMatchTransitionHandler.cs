using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GameMatchTransitionHandler : SingletonPunCallbacks<GameMatchTransitionHandler>, IOnEventCallback
{
    private string _pendingGameRoom;

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
        if (photonEvent.Code != LobbyPhotonEventCodes.MatchConfirmed)
            return;

        if (photonEvent.CustomData is not object[] arr || arr.Length == 0)
            return;

        if (arr[0] is not string roomName || string.IsNullOrEmpty(roomName))
            return;

        if (!PhotonNetwork.InRoom)
            return;

        string lobbyName = PhotonNetwork.CurrentRoom.Name;
        _pendingGameRoom = roomName;

        var ht = new Hashtable
        {
            { LobbyMatchmakingKeys.LobbyRoomName, lobbyName },
            { LobbyMatchmakingKeys.Ready, false }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();

        if (string.IsNullOrEmpty(_pendingGameRoom))
            return;

        string room = _pendingGameRoom;
        _pendingGameRoom = null;

        var roomOptions = new RoomOptions
        {
            MaxPlayers = 8,
            IsVisible = false,
            IsOpen = true,
            CustomRoomProperties = new Hashtable { { PhotonRoomTypes.Key, PhotonRoomTypes.Game } },
            CustomRoomPropertiesForLobby = new[] { PhotonRoomTypes.Key }
        };

        PhotonNetwork.JoinOrCreateRoom(room, roomOptions, TypedLobby.Default);
    }
}
