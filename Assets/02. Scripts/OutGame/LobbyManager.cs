using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("Screens")]
    [SerializeField] private GameObject _mainScreen;
    [SerializeField] private GameObject _matchingScreen;

    [Header("UI")]
    [SerializeField] private NicknameUI _nicknameUI;
    [SerializeField] private MatchButtonUI _matchButtonUI;
    [SerializeField] private MatchingScreenUI _matchingScreenUI;

    [Header("Settings")]
    [SerializeField] private float _countdownSeconds = 3f;
    [SerializeField] private string _inGameSceneName = "InGame";

    private Coroutine _countdownCoroutine;
    private bool _isGameStarting;

    private void Start()
    {
        ShowMainScreen();
        _matchButtonUI.SetInteractable(PhotonNetwork.IsConnectedAndReady);

        _nicknameUI.OnConfirmClicked += HandleNicknameConfirm;
        _matchButtonUI.OnMatchClicked += HandleMatchClicked;
        _matchingScreenUI.OnLeaveClicked += HandleLeaveClicked;

        if (PhotonServerManager.Instance != null)
            PhotonServerManager.Instance.OnNicknameSet += HandleNicknameSet;

        if (PhotonTeamManager.Instance != null)
            PhotonTeamManager.Instance.OnAllTeamsAssigned += HandleAllTeamsAssigned;

        _nicknameUI.SetNickname(PhotonNetwork.NickName);
    }

    private void OnDestroy()
    {
        _nicknameUI.OnConfirmClicked -= HandleNicknameConfirm;
        _matchButtonUI.OnMatchClicked -= HandleMatchClicked;
        _matchingScreenUI.OnLeaveClicked -= HandleLeaveClicked;

        if (PhotonServerManager.Instance != null)
            PhotonServerManager.Instance.OnNicknameSet -= HandleNicknameSet;

        if (PhotonTeamManager.Instance != null)
            PhotonTeamManager.Instance.OnAllTeamsAssigned -= HandleAllTeamsAssigned;
    }

    #region UI Event Handlers

    private void HandleNicknameSet(string nickname)
    {
        _nicknameUI.SetNickname(nickname);
    }

    private void HandleNicknameConfirm(string nickname)
    {
        PhotonNetwork.NickName = nickname;
        Debug.Log($"[LobbyManager] 닉네임 변경: {nickname}");
    }

    private void HandleMatchClicked()
    {
        string nick = _nicknameUI.Nickname.Trim();
        if (!string.IsNullOrEmpty(nick))
            PhotonNetwork.NickName = nick;

        _matchButtonUI.SetInteractable(false);
        PhotonRoomManager.Instance.JoinRandomRoom();
        _matchingScreenUI.SetStatus("매칭을 찾고 있습니다...");
        ShowMatchingScreen();
    }

    private void HandleLeaveClicked()
    {
        PhotonRoomManager.Instance.LeaveRoom();
    }

    #endregion

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        _matchButtonUI.SetInteractable(true);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        _matchButtonUI.SetInteractable(false);
        CancelCountdown();
        ShowMainScreen();
    }

    public override void OnJoinedRoom()
    {
        _isGameStarting = false;
        UpdateMatchingUI();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        _matchingScreenUI.SetStatus("빈 방 없음 → 새 방 생성 중...");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateMatchingUI();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.InRoom) return;

        if (PhotonRoomManager.Instance.GetCurrentRoomType() == PhotonRoomTypes.Random && _isGameStarting)
        {
            CancelCountdown();
            _matchingScreenUI.SetStatus("플레이어가 나갔습니다. 매칭이 취소됩니다.");
            _matchingScreenUI.SetLeaveButtonInteractable(false);
            StartCoroutine(DelayedLeaveRoom());
            return;
        }

        UpdateMatchingUI();
    }

    public override void OnLeftRoom()
    {
        CancelCountdown();
        _isGameStarting = false;
        _matchButtonUI.SetInteractable(PhotonNetwork.IsConnectedAndReady);
        ShowMainScreen();
    }

    #endregion

    #region Team Assignment & Countdown

    private void HandleAllTeamsAssigned()
    {
        if (_countdownCoroutine != null) return;
        _isGameStarting = true;
        _countdownCoroutine = StartCoroutine(CountdownAndLoadScene());
    }

    private IEnumerator CountdownAndLoadScene()
    {
        float remaining = _countdownSeconds;

        while (remaining > 0f)
        {
            _matchingScreenUI.SetStatus($"{Mathf.CeilToInt(remaining)}초 후에 게임을 시작합니다.");
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        _matchingScreenUI.SetStatus("게임을 시작합니다...");

        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel(_inGameSceneName);
    }

    private void CancelCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
        _isGameStarting = false;
    }

    #endregion

    #region Helpers

    private void UpdateMatchingUI()
    {
        if (!PhotonNetwork.InRoom) return;

        var room = PhotonNetwork.CurrentRoom;
        _matchingScreenUI.SetPlayerCount(room.PlayerCount, room.MaxPlayers);
        _matchingScreenUI.SetStatus("매칭을 찾고 있습니다...");
    }

    private IEnumerator DelayedLeaveRoom()
    {
        yield return new WaitForSeconds(1.5f);
        if (PhotonNetwork.InRoom)
            PhotonRoomManager.Instance.LeaveRoom();
    }

    private void ShowMainScreen()
    {
        _mainScreen.SetActive(true);
        _matchingScreen.SetActive(false);
    }

    private void ShowMatchingScreen()
    {
        _mainScreen.SetActive(false);
        _matchingScreen.SetActive(true);
    }

    #endregion
}
