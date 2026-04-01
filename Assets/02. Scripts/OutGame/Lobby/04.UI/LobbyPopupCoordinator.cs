using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPopupCoordinator : MonoBehaviour
{
    [Header("팝업 배경 (검은 배경 부모 · 자식에 패널)")]
    [SerializeField] private GameObject _popupBackgroundRoot;

    [Header("Popups")]
    [SerializeField] private SettingsPopup _settingsPopup;
    [SerializeField] private NicknameChangePopup _nicknameChangePopup;
    [SerializeField] private InviteFriendsPopup _inviteFriendsPopup;
    [SerializeField] private QuitPopup _quitPopup;
    [SerializeField] private PartyInvitePopup _partyInvitePopup;
    [SerializeField] private EscMenuPopup _escMenuPopup;

    [Header("일시 알림 (TMP가 붙은 오브젝트를 켜고 끔)")]
    [SerializeField] private TMP_Text _transientToastText;
    [SerializeField] private float _transientToastDurationSeconds = 3f;

    [Header("파티 초대 버튼 비주얼 (동시에 하나만 활성)")]
    [SerializeField] private GameObject _openInviteFriendsButtonInviteImage;
    [SerializeField] private GameObject _openInviteFriendsButtonBackImage;

    [Header("Optional")]
    [SerializeField] private Button _openSettingsButton;
    [SerializeField] private Button _openNicknameChangeButton;
    [SerializeField] private Button _openInviteFriendsButton;

    private void Start()
    {
        if (_openSettingsButton != null)
            _openSettingsButton.onClick.AddListener(OnOpenSettingsButtonClicked);
        if (_openNicknameChangeButton != null)
            _openNicknameChangeButton.onClick.AddListener(OnOpenNicknameChangeButtonClicked);
        if (_openInviteFriendsButton != null)
            _openInviteFriendsButton.onClick.AddListener(OnOpenInviteFriendsButtonClicked);

        EscMenuPopup.CloseAllPopupsAndEscMenuRequested += OnEscMenuCloseAllRequested;
        EscMenuPopup.OpenSettingsFromEscMenuRequested += OnEscMenuOpenSettingsRequested;
        EscMenuPopup.OpenQuitFromEscMenuRequested += OnEscMenuOpenQuitRequested;
        QuitPopup.CancelRequested += OnQuitCancelRequested;
    }

    private void OnDisable()
    {
        if (_openSettingsButton != null)
            _openSettingsButton.onClick.RemoveListener(OnOpenSettingsButtonClicked);
        if (_openNicknameChangeButton != null)
            _openNicknameChangeButton.onClick.RemoveListener(OnOpenNicknameChangeButtonClicked);
        if (_openInviteFriendsButton != null)
            _openInviteFriendsButton.onClick.RemoveListener(OnOpenInviteFriendsButtonClicked);

        EscMenuPopup.CloseAllPopupsAndEscMenuRequested -= OnEscMenuCloseAllRequested;
        EscMenuPopup.OpenSettingsFromEscMenuRequested -= OnEscMenuOpenSettingsRequested;
        EscMenuPopup.OpenQuitFromEscMenuRequested -= OnEscMenuOpenQuitRequested;
        QuitPopup.CancelRequested -= OnQuitCancelRequested;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleEscape();
    }
    
    public void ShowPopup(LobbyPopupKind kind)
    {
        HideAllBackgroundPopups();
        ShowPopupBackground();
        GetBackgroundPopup(kind)?.Show();
    }

    public void ShowSettings()
    {
        ShowPopup(LobbyPopupKind.Settings);
    }

    public void ShowNicknameChange()
    {
        ShowPopup(LobbyPopupKind.NicknameChange);
    }

    public void ShowFollowFriends()
    {
        ShowPopup(LobbyPopupKind.InviteFriends);
    }

    public void ShowQuitPanel()
    {
        ShowPopup(LobbyPopupKind.Quit);
    }

    public void CloseAllPopups()
    {
        HideAllBackgroundPopups();
        HidePopupBackground();
    }

    private LobbyPopupBase GetBackgroundPopup(LobbyPopupKind kind)
    {
        switch (kind)
        {
            case LobbyPopupKind.Settings:
                return _settingsPopup;
            case LobbyPopupKind.NicknameChange:
                return _nicknameChangePopup;
            case LobbyPopupKind.InviteFriends:
                return _inviteFriendsPopup;
            case LobbyPopupKind.Quit:
                return _quitPopup;
            case LobbyPopupKind.PartyInvite:
                return _partyInvitePopup;
            case LobbyPopupKind.EscMenu:
                return _escMenuPopup;
            default:
                return null;
        }
    }

    private void ShowPopupBackground()
    {
        if (_popupBackgroundRoot != null)
            _popupBackgroundRoot.SetActive(true);
    }

    private void HidePopupBackground()
    {
        if (_popupBackgroundRoot != null)
            _popupBackgroundRoot.SetActive(false);
    }

    private void HideAllBackgroundPopups()
    {
        _settingsPopup?.Hide();
        _nicknameChangePopup?.Hide();
        _inviteFriendsPopup?.Hide();
        _quitPopup?.Hide();
        _partyInvitePopup?.Hide();
        _escMenuPopup?.Hide();
    }

    private void HandleEscape()
    {
        if (IsAnyPopupStackShowing())
            CloseAllPopups();
        else
            ShowPopup(LobbyPopupKind.EscMenu);
    }

    private bool IsAnyPopupStackShowing()
    {
        if (_popupBackgroundRoot != null && _popupBackgroundRoot.activeSelf)
            return true;
        else
            return false;
    }

    private void OnOpenSettingsButtonClicked()
    {
        ShowSettings();
    }

    private void OnOpenNicknameChangeButtonClicked()
    {
        ShowNicknameChange();
    }

    private void OnOpenInviteFriendsButtonClicked()
    {
        ShowFollowFriends();
    }

    private void OnEscMenuCloseAllRequested()
    {
        CloseAllPopups();
    }

    private void OnEscMenuOpenSettingsRequested()
    {
        ShowSettings();
    }

    private void OnEscMenuOpenQuitRequested()
    {
        ShowQuitPanel();
    }

    private void OnQuitCancelRequested()
    {
        CloseAllPopups();
    }
}
