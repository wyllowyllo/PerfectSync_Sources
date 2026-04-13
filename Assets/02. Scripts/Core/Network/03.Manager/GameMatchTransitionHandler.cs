using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GameMatchTransitionHandler : SingletonPunCallbacks<GameMatchTransitionHandler>, IOnEventCallback
{
    private string _pendingGameRoom;
    private bool _leaveDeferred;
    private int _pendingMapNumber = -1;

    protected override bool PersistAcrossScenes => true;

    /// <summary>
    /// 매치 확정 후 로비 퇴장을 미룰 때 호출됩니다. 인자는 게임 방 이름입니다.
    /// <see cref="CompletePendingMatchTransition"/>에서 실제 <see cref="PhotonNetwork.LeaveRoom"/>이 실행됩니다.
    /// </summary>
    public event Action<string> OnMatchConfirmedPendingLeave;

    [Header("Match UI")]
    [Tooltip("켜면 MatchConfirmed 직후 로비를 나가지 않고, LobbyMatchStatusUI 카운트다운 후 CompletePendingMatchTransition에서 나갑니다.")]
    [SerializeField] private bool _waitForMatchStatusCountdown = false;

    [Tooltip("카운트다운 UI가 Complete를 호출하지 않을 때 자동 퇴장까지 대기(초). 0이면 자동 호출 안 함.")]
    [SerializeField] private float _autoCompleteDeferredAfterSeconds = 15f;

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

        _pendingGameRoom = roomName;

        if (_waitForMatchStatusCountdown)
        {
            _leaveDeferred = true;
            OnMatchConfirmedPendingLeave?.Invoke(roomName);
            if (_autoCompleteDeferredAfterSeconds > 0f)
                Invoke(nameof(SafetyCompletePendingLeave), _autoCompleteDeferredAfterSeconds);
            return;
        }

        ApplyLeaveRoomAfterMatchConfirm();
    }

    /// <summary>
    /// 매치 확정 후 로비 퇴장을 실행합니다. 카운트다운 UI 끝에서 호출하세요.
    /// </summary>
    public void CompletePendingMatchTransition()
    {
        CancelInvoke(nameof(SafetyCompletePendingLeave));
        if (!_leaveDeferred)
            return;

        _leaveDeferred = false;
        ApplyLeaveRoomAfterMatchConfirm();
    }

    private void SafetyCompletePendingLeave()
    {
        CompletePendingMatchTransition();
    }

    private void ApplyLeaveRoomAfterMatchConfirm()
    {
        if (!PhotonNetwork.InRoom)
            return;

        // 로비 방의 맵 선택 정보를 저장 (방을 나가면 사라지므로)
        if (PhotonNetwork.CurrentRoom.CustomProperties
                .TryGetValue(MapSelectionManager.SelectedMapKey, out object mapVal))
            _pendingMapNumber = (int)mapVal;

        string lobbyName = PhotonNetwork.CurrentRoom.Name;

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

        var roomProps = new Hashtable { { PhotonRoomTypes.Key, PhotonRoomTypes.Game } };

        if (_pendingMapNumber >= 0)
        {
            roomProps[MapSelectionManager.SelectedMapKey] = _pendingMapNumber;
            _pendingMapNumber = -1;
        }

        var roomOptions = new RoomOptions
        {
            MaxPlayers = 8,
            IsVisible = false,
            IsOpen = true,
            CustomRoomProperties = roomProps,
            CustomRoomPropertiesForLobby = new[] { PhotonRoomTypes.Key }
        };

        PhotonNetwork.JoinOrCreateRoom(room, roomOptions, TypedLobby.Default);
    }
}
