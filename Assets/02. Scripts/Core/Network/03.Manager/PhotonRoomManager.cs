using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PhotonRoomManager : SingletonPunCallbacks<PhotonRoomManager>
{
    protected override bool PersistAcrossScenes => true;

    [Header("Default Room Settings")]
    [SerializeField] private byte _maxPlayers = 8;

    public event Action OnRoomJoined;
    public event Action OnRoomLeft;
    public event Action<short, string> OnRoomJoinFailed;
    public event Action<Player> OnOtherPlayerEntered;
    public event Action<Player> OnOtherPlayerLeft;

    protected override void Awake()
    {
        base.Awake();
    }

    #region Random Matching

    public void JoinRandomRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
            return;

        var filter = new Hashtable { { PhotonRoomTypes.Key, PhotonRoomTypes.Random } };
        PhotonNetwork.JoinRandomRoom(filter, 0);
    }

    #endregion

    #region Custom Room

    public void CreateRoom(string roomName, byte maxPlayers, Hashtable customProperties = null, string[] lobbyProperties = null)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
            return;

        if (customProperties == null)
            customProperties = new Hashtable();

        customProperties[PhotonRoomTypes.Key] = PhotonRoomTypes.Custom;

        var allLobbyProps = new System.Collections.Generic.List<string> { PhotonRoomTypes.Key };
        if (lobbyProperties != null)
            allLobbyProps.AddRange(lobbyProperties);

        var roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            CustomRoomProperties = customProperties,
            CustomRoomPropertiesForLobby = allLobbyProps.ToArray()
        };

        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void JoinRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
            return;

        PhotonNetwork.JoinRoom(roomName);
    }

    #endregion

    #region Leave

    public void LeaveRoom()
    {
        if (!PhotonNetwork.InRoom)
            return;

        PhotonNetwork.LeaveRoom();
    }

    #endregion

    #region PUN Callbacks

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        base.OnJoinRandomFailed(returnCode, message);

        var roomOptions = new RoomOptions
        {
            MaxPlayers = _maxPlayers,
            CustomRoomProperties = new Hashtable { { PhotonRoomTypes.Key, PhotonRoomTypes.Random } },
            CustomRoomPropertiesForLobby = new[] { PhotonRoomTypes.Key }
        };
        PhotonNetwork.CreateRoom(null, roomOptions);
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

        string roomType = GetCurrentRoomType();

        if (roomType == PhotonRoomTypes.Custom)
        {
            PhotonTeamManager.Instance.LeaveTeam();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        OnOtherPlayerEntered?.Invoke(newPlayer);

        if (PhotonNetwork.IsMasterClient && GetCurrentRoomType() == PhotonRoomTypes.Random)
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

    #endregion
}
