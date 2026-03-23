using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Photon Pun 콜백만 받아 <see cref="LobbyManager"/>로 전달합니다. PunCallbacks는 한 컴포넌트에만 두기 위한 분리입니다.
/// </summary>
[DefaultExecutionOrder(-99)]
[DisallowMultipleComponent]
[RequireComponent(typeof(LobbyManager))]
public class LobbyPhotonBridge : MonoBehaviourPunCallbacks
{
    private LobbyManager _lobby;

    private void Awake()
    {
        _lobby = GetComponent<LobbyManager>();
    }

    public override void OnConnectedToMaster()
    {
        _lobby?.HandlePhotonConnectedToMaster();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        _lobby?.HandlePhotonDisconnected(cause);
    }

    public override void OnJoinedRoom()
    {
        _lobby?.HandlePhotonJoinedRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        _lobby?.HandlePhotonJoinRandomFailed(returnCode, message);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        _lobby?.HandlePhotonPlayerEnteredRoom(newPlayer);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        _lobby?.HandlePhotonPlayerLeftRoom(otherPlayer);
    }

    public override void OnLeftRoom()
    {
        _lobby?.HandlePhotonLeftRoom();
    }
}
