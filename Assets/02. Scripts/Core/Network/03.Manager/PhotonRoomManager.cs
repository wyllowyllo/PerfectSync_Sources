using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PhotonRoomManager : SingletonPunCallbacks<PhotonRoomManager>
{
    protected override bool PersistAcrossScenes => true;

    public event Action OnRoomJoined;
    public event Action OnRoomLeft;
    public event Action<short, string> OnRoomJoinFailed;
    public event Action<Player> OnOtherPlayerEntered;
    public event Action<Player> OnOtherPlayerLeft;

    protected override void Awake()
    {
        base.Awake();
    }

    public void JoinRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
            return;

        PhotonNetwork.JoinRoom(roomName);
    }

    public void LeaveRoom()
    {
        if (!PhotonNetwork.InRoom)
            return;

        PhotonNetwork.LeaveRoom();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        base.OnCreateRoomFailed(returnCode, message);
        OnRoomJoinFailed?.Invoke(returnCode, message);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
        OnRoomJoinFailed?.Invoke(returnCode, message);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        InitializeTeamForRoomType();
        OnRoomJoined?.Invoke();
    }

    private void InitializeTeamForRoomType()
    {
        if (PhotonTeamManager.Instance == null) return;

        if (GetCurrentRoomType() == PhotonRoomTypes.Lobby)
            PhotonTeamManager.Instance.LeaveTeam();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        OnOtherPlayerEntered?.Invoke(newPlayer);

        if (PhotonNetwork.IsMasterClient && GetCurrentRoomType() == PhotonRoomTypes.Game)
        {
            if (PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers)
                PhotonTeamManager.Instance?.AssignTeamsRandomly();
        }
    }

    public string GetCurrentRoomType()
    {
        if (!PhotonNetwork.InRoom) return string.Empty;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (props.TryGetValue(PhotonRoomTypes.Key, out object roomType))
            return (string)roomType;

        return string.Empty;
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        OnRoomLeft?.Invoke();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        OnOtherPlayerLeft?.Invoke(otherPlayer);
    }
}
