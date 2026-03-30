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
            PublishUserId = true,
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

        if (GetCurrentRoomType() != PhotonRoomTypes.Lobby)
            return;

        var room = PhotonNetwork.CurrentRoom;
        if (room != null)
        {
            Debug.Log(
                $"[LobbyRoomConnector] 로비 방 입장 — 방 이름: {room.Name}, 인원: {room.PlayerCount}/{room.MaxPlayers}, " +
                $"방 타입(커스텀): {PhotonRoomTypes.Lobby}, 마스터 클라이언트: {room.MasterClientId}, " +
                $"로컬 닉네임: {PhotonNetwork.LocalPlayer?.NickName}");
        }

        OnLobbyRoomJoined?.Invoke();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);

        if (GetCurrentRoomType() != PhotonRoomTypes.Lobby || newPlayer == null)
            return;

        Debug.Log(
            $"[LobbyRoomConnector] 로비에 플레이어 입장 — 닉네임: {newPlayer.NickName}, ActorNumber: {newPlayer.ActorNumber}, " +
            $"UserId: {newPlayer.UserId}, IsLocal: {newPlayer.IsLocal}");
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
