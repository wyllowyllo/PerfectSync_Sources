using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PhotonPartyManager : SingletonPunCallbacks<PhotonPartyManager>
{
    private const string READY_KEY = "ready";
    private const string TARGET_ROOM_KEY = "targetRoom";
    private const string PARTY_CODE_PREFIX = "P-";
    private const int PARTY_MAX_PLAYERS = 2;

    private string _targetRoom;
    private bool _wasPartyLeader;

    public string PartyCode { get; private set; }
    public bool IsInParty => PhotonNetwork.InRoom && GetCurrentRoomType() == PhotonRoomTypes.PARTY;
    public bool IsPartyLeader => IsInParty && PhotonNetwork.IsMasterClient;

    public event Action<string> OnPartyCreated;
    public event Action OnPartyJoined;
    public event Action OnPartyLeft;
    public event Action<Player, bool> OnPlayerReadyChanged;
    public event Action<string> OnMatchmakingStarted;

    #region Party Create / Join / Leave

    public void CreateParty()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("[PhotonPartyManager] 마스터 서버에 연결되어 있지 않습니다.");
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[PhotonPartyManager] 이미 방에 입장해 있습니다.");
            return;
        }

        string code = GeneratePartyCode();

        var roomOptions = new RoomOptions
        {
            MaxPlayers = PARTY_MAX_PLAYERS,
            IsVisible = false,
            CustomRoomProperties = new Hashtable { { PhotonRoomTypes.KEY, PhotonRoomTypes.PARTY } },
            CustomRoomPropertiesForLobby = new[] { PhotonRoomTypes.KEY }
        };

        Debug.Log($"[PhotonPartyManager] 파티 생성 시도... (Code: {code})");
        PhotonNetwork.CreateRoom(code, roomOptions);
    }

    public void JoinParty(string code)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("[PhotonPartyManager] 마스터 서버에 연결되어 있지 않습니다.");
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[PhotonPartyManager] 이미 방에 입장해 있습니다.");
            return;
        }

        Debug.Log($"[PhotonPartyManager] 파티 참가 시도... (Code: {code})");
        PhotonNetwork.JoinRoom(code);
    }

    public void LeaveParty()
    {
        if (!IsInParty)
        {
            Debug.LogWarning("[PhotonPartyManager] 파티에 입장해 있지 않습니다.");
            return;
        }

        Debug.Log("[PhotonPartyManager] 파티 퇴장 시도...");
        PartyCode = null;
        PhotonNetwork.LeaveRoom();
    }

    #endregion

    #region Ready

    public void SetReady(bool ready)
    {
        if (!IsInParty) return;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { READY_KEY, ready } });
        Debug.Log($"[PhotonPartyManager] 준비 상태: {ready}");
    }

    public bool IsPlayerReady(Player player)
    {
        if (player.CustomProperties.TryGetValue(READY_KEY, out object readyObj))
            return (bool)readyObj;

        return false;
    }

    public bool AreAllReady()
    {
        if (!IsInParty) return false;
        if (PhotonNetwork.CurrentRoom.PlayerCount < PARTY_MAX_PLAYERS) return false;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!IsPlayerReady(player))
                return false;
        }
        return true;
    }

    #endregion

    #region Matchmaking

    /// <summary>
    /// 파티장 전용. 랜덤 매칭 방 이름을 생성하고, 파티 방 CustomProperties에 저장 후
    /// 모든 파티원이 해당 방으로 이동하도록 합니다.
    /// </summary>
    public void StartMatchmaking()
    {
        if (!IsPartyLeader)
        {
            Debug.LogWarning("[PhotonPartyManager] 파티장만 매칭을 시작할 수 있습니다.");
            return;
        }

        if (!AreAllReady())
        {
            Debug.LogWarning("[PhotonPartyManager] 모든 파티원이 준비되지 않았습니다.");
            return;
        }

        string targetRoom = "R-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        Debug.Log($"[PhotonPartyManager] 매칭 시작. 대상 방: {targetRoom}");

        var props = new Hashtable { { TARGET_ROOM_KEY, targetRoom } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    #endregion

    #region PUN Callbacks

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        if (GetCurrentRoomType() != PhotonRoomTypes.PARTY) return;

        PartyCode = PhotonNetwork.CurrentRoom.Name;

        if (PhotonNetwork.IsMasterClient)
        {
            SetReady(true);
            Debug.Log($"[PhotonPartyManager] 파티 생성 완료. (Code: {PartyCode})");
            OnPartyCreated?.Invoke(PartyCode);
        }
        else
        {
            SetReady(false);
            Debug.Log($"[PhotonPartyManager] 파티 참가 완료. (Code: {PartyCode})");
        }

        SetPartyId(PartyCode);
        OnPartyJoined?.Invoke();
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();

        if (_targetRoom != null)
        {
            string room = _targetRoom;
            bool isLeader = _wasPartyLeader;
            _targetRoom = null;
            _wasPartyLeader = false;

            if (isLeader)
            {
                Debug.Log($"[PhotonPartyManager] 파티장: 랜덤 매칭 방 생성 → {room}");
                var roomOptions = new RoomOptions
                {
                    MaxPlayers = 8,
                    CustomRoomProperties = new Hashtable { { PhotonRoomTypes.KEY, PhotonRoomTypes.RANDOM } },
                    CustomRoomPropertiesForLobby = new[] { PhotonRoomTypes.KEY }
                };
                PhotonNetwork.CreateRoom(room, roomOptions);
            }
            else
            {
                Debug.Log($"[PhotonPartyManager] 파티원: 랜덤 매칭 방 입장 → {room}");
                PhotonNetwork.JoinRoom(room);
            }
            return;
        }

        PartyCode = null;
        Debug.Log("[PhotonPartyManager] 파티 퇴장 완료.");
        OnPartyLeft?.Invoke();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (!IsInParty) return;

        if (changedProps.ContainsKey(READY_KEY))
        {
            bool ready = (bool)changedProps[READY_KEY];
            Debug.Log($"[PhotonPartyManager] {targetPlayer.NickName} 준비 상태: {ready}");
            OnPlayerReadyChanged?.Invoke(targetPlayer, ready);
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        if (!IsInParty) return;

        if (propertiesThatChanged.ContainsKey(TARGET_ROOM_KEY))
        {
            string targetRoom = (string)propertiesThatChanged[TARGET_ROOM_KEY];
            Debug.Log($"[PhotonPartyManager] 매칭 방 확인: {targetRoom}. 파티 방을 퇴장합니다.");

            _targetRoom = targetRoom;
            _wasPartyLeader = PhotonNetwork.IsMasterClient;
            OnMatchmakingStarted?.Invoke(targetRoom);
            PhotonNetwork.LeaveRoom();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);

        if (!IsInParty) return;
        Debug.Log($"[PhotonPartyManager] 파티원 입장: {newPlayer.NickName}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);

        if (!IsInParty) return;
        Debug.Log($"[PhotonPartyManager] 파티원 퇴장: {otherPlayer.NickName}");
    }

    #endregion

    #region Utility

    private string GetCurrentRoomType()
    {
        if (!PhotonNetwork.InRoom) return string.Empty;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (props.TryGetValue(PhotonRoomTypes.KEY, out object roomType))
            return (string)roomType;

        return string.Empty;
    }

    private static string GeneratePartyCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var code = new char[6];
        for (int i = 0; i < code.Length; i++)
        {
            code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        }
        return PARTY_CODE_PREFIX + new string(code);
    }

    /// <summary>
    /// 파티 ID를 Player CustomProperties에 설정합니다.
    /// 랜덤 매칭 방에서 같은 파티원을 같은 팀으로 묶기 위해 사용합니다.
    /// </summary>
    private void SetPartyId(string partyId)
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable { { PhotonTeamManager.PARTY_ID_KEY, partyId } }
        );
    }

    #endregion
}
