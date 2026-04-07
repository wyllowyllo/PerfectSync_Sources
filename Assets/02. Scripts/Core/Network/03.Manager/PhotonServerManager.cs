using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PhotonServerManager : SingletonPunCallbacks<PhotonServerManager>
{
    protected override bool PersistAcrossScenes => true;

    [Header("Connection Settings")]
    [SerializeField] private string _gameVersion = "0.0.1";
    [SerializeField] private bool _autoConnect = true;

    public event Action<string> OnNicknameSet;
    public event Action<string> OnLocalUserIdReady;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        if (_autoConnect)
            Connect();
    }

    public void Connect()
    {
        string nickName = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
        OnNicknameSet?.Invoke(nickName);
        Connect(nickName);
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

        ApplySessionAuthUserId();
        PhotonNetwork.ConnectUsingSettings();
    }

    private static void ApplySessionAuthUserId()
    {
        string uid = null;

        if (AuthService.Instance != null && AuthService.Instance.IsLoggedIn)
            uid = AuthService.Instance.CurrentUserEmail;

        if (string.IsNullOrEmpty(uid))
            uid = UserIdGenerator.CreateSessionUserId();

        PhotonNetwork.AuthValues = new Photon.Realtime.AuthenticationValues(uid);
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
        RaiseLocalUserIdReady();
    }

    private void RaiseLocalUserIdReady()
    {
        string id = null;
        if (PhotonNetwork.LocalPlayer != null && !string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId))
            id = PhotonNetwork.LocalPlayer.UserId;
        else if (PhotonNetwork.AuthValues != null && !string.IsNullOrEmpty(PhotonNetwork.AuthValues.UserId))
            id = PhotonNetwork.AuthValues.UserId;

        OnLocalUserIdReady?.Invoke(id ?? string.Empty);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
    }
}
