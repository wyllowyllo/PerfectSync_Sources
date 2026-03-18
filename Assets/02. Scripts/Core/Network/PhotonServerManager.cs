using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PhotonServerManager : SingletonPunCallbacks<PhotonServerManager>
{
    [Header("Connection Settings")]
    [SerializeField] private string _gameVersion = "0.0.1";
    [SerializeField] private string _nickName = "Player";
    [SerializeField] private bool _autoConnect = true;

    private void Start()
    {
        if (_autoConnect)
        {
            Connect();
        }
    }

    public void Connect()
    {
        Connect(_nickName);
    }

    public void Connect(string nickName)
    {
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("[PhotonServerManager] 이미 마스터 서버에 연결되어 있습니다.");
            return;
        }

        PhotonNetwork.GameVersion = _gameVersion;
        PhotonNetwork.NickName = nickName;
        PhotonNetwork.AutomaticallySyncScene = true;

        // TODO : 어떤 수치가 가장 적당한지 검증 필요
        PhotonNetwork.SendRate = 40;
        PhotonNetwork.SerializationRate = 30;

        Debug.Log($"[PhotonServerManager] 마스터 서버 접속 시도... (Version: {_gameVersion}, NickName: {nickName})");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        Debug.Log($"[PhotonServerManager] 마스터 서버 접속 완료. Region: {PhotonNetwork.CloudRegion}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        
        if (cause != DisconnectCause.None && cause != DisconnectCause.DisconnectByClientLogic)
        {
            Debug.LogWarning($"[PhotonServerManager] 마스터 서버 연결 해제/실패: {cause}");
        }
        else
        {
            Debug.Log($"[PhotonServerManager] 마스터 서버 연결 해제: {cause}");
        }
    }
}
