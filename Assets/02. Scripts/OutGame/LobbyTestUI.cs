using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class LobbyTestUI : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private Button _connectButton;
    [SerializeField] private Button _matchButton;
    [SerializeField] private TMP_Text _statusText;

    [Header("Settings")]
    [SerializeField] private float _countdownSeconds = 3f;
    [SerializeField] private string _inGameSceneName = "InGame";

    private Coroutine _countdownCoroutine;

    private void Start()
    {
        _nicknameInput.text = $"Player_{Random.Range(1000, 9999)}";

        _connectButton.onClick.AddListener(OnClickConnect);
        _matchButton.onClick.AddListener(OnClickMatch);

        UpdateButtonStates();
        SubscribeTeamEvents();
    }

    private void OnClickConnect()
    {
        if (PhotonNetwork.IsConnected)
        {
            SetStatus("이미 마스터 서버에 접속되어 있습니다.");
            UpdateButtonStates();
            return;
        }

        string nick = _nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(nick))
        {
            SetStatus("닉네임을 입력해주세요.");
            return;
        }

        _connectButton.interactable = false;
        PhotonServerManager.Instance.Connect(nick);
        SetStatus("마스터 서버 접속 중...");
    }

    private void OnClickMatch()
    {
        string nick = _nicknameInput.text.Trim();
        if (!string.IsNullOrEmpty(nick))
            PhotonNetwork.NickName = nick;

        _matchButton.interactable = false;
        PhotonRoomManager.Instance.JoinRandomRoom();
        SetStatus("랜덤 매칭 대기 중...");
    }

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        UpdateButtonStates();
        SetStatus($"마스터 서버 접속 완료. (Region: {PhotonNetwork.CloudRegion})");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        UpdateButtonStates();
        SetStatus($"연결 끊김: {cause}");
    }

    public override void OnJoinedRoom()
    {
        var room = PhotonNetwork.CurrentRoom;
        SetStatus($"방 입장! ({room.PlayerCount}/{room.MaxPlayers}명) 대기 중...");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        SetStatus("빈 방 없음 → 새 방 생성 중...");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        var room = PhotonNetwork.CurrentRoom;
        SetStatus($"{newPlayer.NickName} 입장! ({room.PlayerCount}/{room.MaxPlayers}명)");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.InRoom) return;
        var room = PhotonNetwork.CurrentRoom;
        SetStatus($"{otherPlayer.NickName} 퇴장. ({room.PlayerCount}/{room.MaxPlayers}명)");
    }

    #endregion

    public override void OnEnable()
    {
        base.OnEnable();
        SubscribeTeamEvents();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        UnsubscribeTeamEvents();
    }

    private void SubscribeTeamEvents()
    {
        if (PhotonTeamManager.Instance == null) return;
        PhotonTeamManager.Instance.OnAllTeamsAssigned -= HandleAllTeamsAssigned;
        PhotonTeamManager.Instance.OnAllTeamsAssigned += HandleAllTeamsAssigned;
    }

    private void UnsubscribeTeamEvents()
    {
        if (PhotonTeamManager.Instance == null) return;
        PhotonTeamManager.Instance.OnAllTeamsAssigned -= HandleAllTeamsAssigned;
    }

    private void HandleAllTeamsAssigned()
    {
        if (_countdownCoroutine != null) return;
        _countdownCoroutine = StartCoroutine(CountdownAndLoadScene());
    }

    private IEnumerator CountdownAndLoadScene()
    {
        float remaining = _countdownSeconds;

        while (remaining > 0f)
        {
            SetStatus($"팀 배정 완료! {Mathf.CeilToInt(remaining)}초 후 게임 시작...");
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        SetStatus("게임 씬으로 이동 중...");

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(_inGameSceneName);
        }
    }

    private void UpdateButtonStates()
    {
        bool connected = PhotonNetwork.IsConnectedAndReady;
        _connectButton.interactable = !connected;
        _matchButton.interactable = connected && !PhotonNetwork.InRoom;
    }

    private void SetStatus(string message)
    {
        _statusText.text = message;
        Debug.Log($"[LobbyTestUI] {message}");
    }
}
