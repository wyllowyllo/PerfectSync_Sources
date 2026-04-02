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
        if (photonEvent.Code != PhotonEventCodes.MatchConfirmed)
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
        // JoinOrCreateRoom은 게임 서버 종료 직후에는 호출할 수 없음. OnConnectedToMaster에서 처리.
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        TryJoinPendingGameRoom();
    }

    private void TryJoinPendingGameRoom()
    {
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
