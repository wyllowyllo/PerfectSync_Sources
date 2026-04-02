using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserInfoUI : MonoBehaviour
{
    [Header("닉네임")]
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private Button _editNicknameButton;
    [SerializeField] private string _emptyNicknameDisplay = "";

    [Header("User ID")]
    [SerializeField] private TMP_Text _userIdText;
    [SerializeField] private Button _copyUserIdButton;
    [SerializeField] private string _userIdPrefix = "UserID : ";
    [SerializeField] private string _emptyUserIdDisplay = "";

    private string _userId;

    private void Start()
    {
        if (_editNicknameButton != null)
            _editNicknameButton.onClick.AddListener(OnEditNicknameClicked);
        if (_copyUserIdButton != null)
            _copyUserIdButton.onClick.AddListener(OnCopyUserIdClicked);

        SubscribeDataEvents();
        RefreshNicknameDisplay();
        TryApplyExistingUserId();
    }

    private void OnDisable()
    {
        if (_editNicknameButton != null)
            _editNicknameButton.onClick.RemoveListener(OnEditNicknameClicked);
        if (_copyUserIdButton != null)
            _copyUserIdButton.onClick.RemoveListener(OnCopyUserIdClicked);

        UnsubscribeDataEvents();
    }

    private void SubscribeDataEvents()
    {
        if (PhotonServerManager.Instance != null)
            PhotonServerManager.Instance.OnLocalUserIdReady += HandleUserIdReady;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.NicknameFieldSet += HandleNicknameFieldSet;

        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined += HandleLobbyRoomJoined;
    }

    private void UnsubscribeDataEvents()
    {
        if (PhotonServerManager.Instance != null)
            PhotonServerManager.Instance.OnLocalUserIdReady -= HandleUserIdReady;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.NicknameFieldSet -= HandleNicknameFieldSet;

        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined -= HandleLobbyRoomJoined;
    }

    private void TryApplyExistingUserId()
    {
        if (PhotonServerManager.Instance != null && PhotonNetwork.IsConnectedAndReady)
            HandleUserIdReady(GetCurrentPhotonUserIdString());
        else
            RefreshUserIdDisplay();
    }

    private void HandleUserIdReady(string userId)
    {
        _userId = userId ?? string.Empty;
        RefreshUserIdDisplay();
    }

    private void HandleNicknameFieldSet(string nickname)
    {
        RefreshNicknameDisplay(nickname);
    }

    private void HandleLobbyRoomJoined()
    {
        RefreshNicknameDisplay();
    }

    private static string GetCurrentPhotonUserIdString()
    {
        if (PhotonNetwork.LocalPlayer != null && !string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId))
            return PhotonNetwork.LocalPlayer.UserId;
        if (PhotonNetwork.AuthValues != null && !string.IsNullOrEmpty(PhotonNetwork.AuthValues.UserId))
            return PhotonNetwork.AuthValues.UserId;
        return string.Empty;
    }

    private void RefreshNicknameDisplay(string nickname = null)
    {
        if (_nicknameText == null)
            return;

        string display = nickname;
        if (string.IsNullOrEmpty(display))
            display = PhotonNetwork.NickName ?? string.Empty;

        _nicknameText.text = string.IsNullOrEmpty(display) ? _emptyNicknameDisplay : display;
    }

    private void RefreshUserIdDisplay()
    {
        if (_userIdText == null)
            return;

        _userIdText.text = string.IsNullOrEmpty(_userId)
            ? _emptyUserIdDisplay
            : _userIdPrefix + _userId;
    }

    private void OnEditNicknameClicked()
    {
        LobbyUiEvents.RequestNicknameChangePopup();
    }

    private void OnCopyUserIdClicked()
    {
        if (string.IsNullOrEmpty(_userId))
            return;

        GUIUtility.systemCopyBuffer = _userId;
        LobbyUiEvents.RequestTransientToast("복사 완료");
    }
}
