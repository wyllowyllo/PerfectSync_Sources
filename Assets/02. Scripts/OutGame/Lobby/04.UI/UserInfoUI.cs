using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserInfoUI : MonoBehaviour
{
    private const string EmptyDisplay = "";

    [Header("닉네임")]
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private Button _editNicknameButton;

    [Header("초대 코드")]
    [SerializeField] private TMP_Text _inviteCodeText;
    [SerializeField] private Button _copyInviteCodeButton;
    [SerializeField] private string _inviteCodePrefix = "초대 코드 : ";

    private string _inviteCode;

    private void Start()
    {
        if (_editNicknameButton != null)
            _editNicknameButton.onClick.AddListener(OnEditNicknameClicked);
        if (_copyInviteCodeButton != null)
            _copyInviteCodeButton.onClick.AddListener(OnCopyInviteCodeClicked);

        SubscribeDataEvents();
        RefreshNicknameDisplay();
        RefreshInviteCodeDisplay();
    }

    private void OnDisable()
    {
        if (_editNicknameButton != null)
            _editNicknameButton.onClick.RemoveListener(OnEditNicknameClicked);
        if (_copyInviteCodeButton != null)
            _copyInviteCodeButton.onClick.RemoveListener(OnCopyInviteCodeClicked);

        UnsubscribeDataEvents();
    }

    private void SubscribeDataEvents()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.NicknameFieldSet += HandleNicknameFieldSet;
            LobbyManager.Instance.InviteCodeChanged += HandleInviteCodeChanged;
        }

        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined += HandleLobbyRoomJoined;
    }

    private void UnsubscribeDataEvents()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.NicknameFieldSet -= HandleNicknameFieldSet;
            LobbyManager.Instance.InviteCodeChanged -= HandleInviteCodeChanged;
        }

        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined -= HandleLobbyRoomJoined;
    }

    private void HandleInviteCodeChanged(string code)
    {
        _inviteCode = code ?? string.Empty;
        RefreshInviteCodeDisplay();
    }

    private void HandleNicknameFieldSet(string nickname)
    {
        RefreshNicknameDisplay(nickname);
    }

    private void HandleLobbyRoomJoined()
    {
        RefreshNicknameDisplay();
    }

    private void RefreshNicknameDisplay(string nickname = null)
    {
        if (_nicknameText == null)
            return;

        string display = nickname;
        if (string.IsNullOrEmpty(display))
            display = PhotonNetwork.NickName ?? string.Empty;

        _nicknameText.text = string.IsNullOrEmpty(display) ? EmptyDisplay : display;
    }

    private void RefreshInviteCodeDisplay()
    {
        if (_inviteCodeText == null)
            return;

        _inviteCodeText.text = string.IsNullOrEmpty(_inviteCode)
            ? EmptyDisplay
            : _inviteCodePrefix + _inviteCode;
    }

    private void OnEditNicknameClicked()
    {
        LobbyUiEvents.RequestNicknameChangePopup();
    }

    private void OnCopyInviteCodeClicked()
    {
        if (string.IsNullOrEmpty(_inviteCode))
            return;

        GUIUtility.systemCopyBuffer = _inviteCode;
        LobbyUiEvents.RequestTransientToast("초대 코드 복사 완료");
    }
}
