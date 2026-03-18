using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PhotonRoomManager : SingletonPunCallbacks<PhotonRoomManager>
{
    [Header("Default Room Settings")]
    [SerializeField] private byte _maxPlayers = 8;

    public event Action OnRoomJoined;
    public event Action OnRoomLeft;
    public event Action<short, string> OnRoomJoinFailed;
    public event Action<Player> OnOtherPlayerEntered;
    public event Action<Player> OnOtherPlayerLeft;

    #region Random Matching

    public void JoinRandomRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("[PhotonRoomManager] 마스터 서버에 연결되어 있지 않습니다.");
            return;
        }

        var filter = new Hashtable { { PhotonRoomTypes.KEY, PhotonRoomTypes.RANDOM } };
        Debug.Log("[PhotonRoomManager] 랜덤 방 입장 시도...");
        PhotonNetwork.JoinRandomRoom(filter, 0);
    }

    #endregion

    #region Custom Room

    public void CreateRoom(string roomName, byte maxPlayers, Hashtable customProperties = null, string[] lobbyProperties = null)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("[PhotonRoomManager] 마스터 서버에 연결되어 있지 않습니다.");
            return;
        }

        if (customProperties == null)
            customProperties = new Hashtable();

        customProperties[PhotonRoomTypes.KEY] = PhotonRoomTypes.CUSTOM;

        var allLobbyProps = new System.Collections.Generic.List<string> { PhotonRoomTypes.KEY };
        if (lobbyProperties != null)
            allLobbyProps.AddRange(lobbyProperties);

        var roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            CustomRoomProperties = customProperties,
            CustomRoomPropertiesForLobby = allLobbyProps.ToArray()
        };

        Debug.Log($"[PhotonRoomManager] 방 생성 시도... (Name: {roomName}, MaxPlayers: {maxPlayers})");
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void JoinRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("[PhotonRoomManager] 마스터 서버에 연결되어 있지 않습니다.");
            return;
        }

        Debug.Log($"[PhotonRoomManager] 방 입장 시도... (Name: {roomName})");
        PhotonNetwork.JoinRoom(roomName);
    }

    #endregion

    #region Leave

    public void LeaveRoom()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[PhotonRoomManager] 현재 방에 있지 않습니다.");
            return;
        }

        Debug.Log("[PhotonRoomManager] 방 퇴장 시도...");
        PhotonNetwork.LeaveRoom();
    }

    #endregion

    #region PUN Callbacks

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        base.OnJoinRandomFailed(returnCode, message);
        Debug.Log($"[PhotonRoomManager] 랜덤 방 입장 실패 (빈 방 없음). 새 방을 생성합니다.");

        var roomOptions = new RoomOptions
        {
            MaxPlayers = _maxPlayers,
            CustomRoomProperties = new Hashtable { { PhotonRoomTypes.KEY, PhotonRoomTypes.RANDOM } },
            CustomRoomPropertiesForLobby = new[] { PhotonRoomTypes.KEY }
        };
        PhotonNetwork.CreateRoom(null, roomOptions);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        base.OnCreateRoomFailed(returnCode, message);
        Debug.LogError($"[PhotonRoomManager] 방 생성 실패: [{returnCode}] {message}");
        OnRoomJoinFailed?.Invoke(returnCode, message);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
        Debug.LogError($"[PhotonRoomManager] 방 입장 실패: [{returnCode}] {message}");
        OnRoomJoinFailed?.Invoke(returnCode, message);
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        var room = PhotonNetwork.CurrentRoom;
        Debug.Log($"[PhotonRoomManager] 방 입장 완료. (Name: {room.Name}, Players: {room.PlayerCount}/{room.MaxPlayers}, IsMasterClient: {PhotonNetwork.IsMasterClient})");

        InitializeTeamForRoomType();
        OnRoomJoined?.Invoke();
    }

    private void InitializeTeamForRoomType()
    {
        if (PhotonTeamManager.Instance == null) return;

        string roomType = GetCurrentRoomType();

        if (roomType == PhotonRoomTypes.CUSTOM)
        {
            PhotonTeamManager.Instance.LeaveTeam();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        Debug.Log($"[PhotonRoomManager] 플레이어 입장: {newPlayer.NickName} (현재 {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
        OnOtherPlayerEntered?.Invoke(newPlayer);

        if (PhotonNetwork.IsMasterClient && GetCurrentRoomType() == PhotonRoomTypes.RANDOM)
        {
            if (PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers)
            {
                Debug.Log("[PhotonRoomManager] 인원이 모두 찼습니다. 랜덤 팀 배정을 시작합니다.");
                PhotonTeamManager.Instance?.AssignTeamsRandomly();
            }
        }
    }

    public string GetCurrentRoomType()
    {
        if (!PhotonNetwork.InRoom) return string.Empty;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (props.TryGetValue(PhotonRoomTypes.KEY, out object roomType))
            return (string)roomType;

        return string.Empty;
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        Debug.Log("[PhotonRoomManager] 방 퇴장 완료.");
        OnRoomLeft?.Invoke();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        Debug.Log($"[PhotonRoomManager] 플레이어 퇴장: {otherPlayer.NickName} (현재 {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
        OnOtherPlayerLeft?.Invoke(otherPlayer);
    }

    #endregion
}
