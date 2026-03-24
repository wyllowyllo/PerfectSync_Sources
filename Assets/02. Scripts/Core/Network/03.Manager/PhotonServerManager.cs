using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PhotonServerManager : SingletonPunCallbacks<PhotonServerManager>
{
    protected override bool PersistAcrossScenes => true;

    [Header("Connection Settings")]
    [SerializeField] private string _gameVersion = "0.0.1";
    [SerializeField] private string _nickName = "";
    [SerializeField] private bool _autoConnect = true;

    public event Action<string> OnNicknameSet;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        if (_autoConnect)
        {
            Connect();
        }
    }

    public void Connect()
    {
        if (string.IsNullOrEmpty(_nickName))
            _nickName = $"Player_{UnityEngine.Random.Range(1000, 9999)}";

        OnNicknameSet?.Invoke(_nickName);
        Connect(_nickName);
    }

    public void Connect(string nickName)
    {
        if (PhotonNetwork.IsConnected)
            return;

        PhotonNetwork.GameVersion = _gameVersion;
        PhotonNetwork.NickName = nickName;
        PhotonNetwork.AutomaticallySyncScene = true;

        // TODO : 어떤 수치가 가장 적당한지 검증 필요
        PhotonNetwork.SendRate = 40;
        PhotonNetwork.SerializationRate = 30;

        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
    }
}
