using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

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

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        _lobby?.HandlePhotonPlayerPropertiesUpdate(targetPlayer, changedProps);
    }
}
