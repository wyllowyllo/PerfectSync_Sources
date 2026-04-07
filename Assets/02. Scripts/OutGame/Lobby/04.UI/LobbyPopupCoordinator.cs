using System.Collections;
using Photon.Realtime;
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
    [SerializeField] private Button _openInviteFriendsButton;

    [Header("Lobby Nickname Objects (Customization 토글)")]
    [SerializeField] private GameObject _myNickname;
    [SerializeField] private GameObject _friendNickname;

    private bool _nicknameObjectsDesiredActive = true;
    private Coroutine _transientToastCoroutine;

    private void Start()
    {
        if (_openSettingsButton != null)
            _openSettingsButton.onClick.AddListener(OnOpenSettingsButtonClicked);
        if (_openInviteFriendsButton != null)
            _openInviteFriendsButton.onClick.AddListener(OnOpenInviteFriendsButtonClicked);

        LobbyUiEvents.NicknameChangePopupRequested += OnNicknameChangePopupRequested;
        LobbyUiEvents.TransientToastRequested += OnLobbyUiTransientToastRequested;
        PartyInvitePopup.TransientToastRequested += OnPartyInviteTransientToastRequested;
        PartyInvitePopup.PartyInviteClosedOnlyRequested += OnPartyInviteClosedOnly;

        if (LobbyPartyService.Instance != null)
        {
            LobbyPartyService.Instance.OnPartyInviteReceived += HandlePartyInviteReceived;
            LobbyPartyService.Instance.OnPartyInviteResponded += HandlePartyInviteResponded;
            LobbyPartyService.Instance.OnPendingPartyInviteInvalidated += HandlePendingInviteInvalidated;
            LobbyPartyService.Instance.OnPartyPartnerLinked += HandlePartyPartnerLinked;
            LobbyPartyService.Instance.OnPartyCleared += HandlePartyCleared;
        }

        EscMenuPopup.CloseAllPopupsAndEscMenuRequested += OnEscMenuCloseAllRequested;
        EscMenuPopup.OpenSettingsFromEscMenuRequested += OnEscMenuOpenSettingsRequested;
        EscMenuPopup.OpenQuitFromEscMenuRequested += OnEscMenuOpenQuitRequested;
        QuitPopup.CancelRequested += OnQuitCancelRequested;

        SlidePanelStateController.SwitchToCustomizingStarted += HideNickname;
        SlidePanelStateController.SwitchToNormalCompleted += ShowNickname;

        RefreshNicknameObjectsActive();
    }

    private void OnDisable()
    {
        if (_openSettingsButton != null)
            _openSettingsButton.onClick.RemoveListener(OnOpenSettingsButtonClicked);
        if (_openInviteFriendsButton != null)
            _openInviteFriendsButton.onClick.RemoveListener(OnOpenInviteFriendsButtonClicked);

        LobbyUiEvents.NicknameChangePopupRequested -= OnNicknameChangePopupRequested;
        LobbyUiEvents.TransientToastRequested -= OnLobbyUiTransientToastRequested;
        PartyInvitePopup.TransientToastRequested -= OnPartyInviteTransientToastRequested;
        PartyInvitePopup.PartyInviteClosedOnlyRequested -= OnPartyInviteClosedOnly;

        if (LobbyPartyService.Instance != null)
        {
            LobbyPartyService.Instance.OnPartyInviteReceived -= HandlePartyInviteReceived;
            LobbyPartyService.Instance.OnPartyInviteResponded -= HandlePartyInviteResponded;
            LobbyPartyService.Instance.OnPendingPartyInviteInvalidated -= HandlePendingInviteInvalidated;
            LobbyPartyService.Instance.OnPartyPartnerLinked -= HandlePartyPartnerLinked;
            LobbyPartyService.Instance.OnPartyCleared -= HandlePartyCleared;
        }

        EscMenuPopup.CloseAllPopupsAndEscMenuRequested -= OnEscMenuCloseAllRequested;
        EscMenuPopup.OpenSettingsFromEscMenuRequested -= OnEscMenuOpenSettingsRequested;
        EscMenuPopup.OpenQuitFromEscMenuRequested -= OnEscMenuOpenQuitRequested;
        QuitPopup.CancelRequested -= OnQuitCancelRequested;

        SlidePanelStateController.SwitchToCustomizingStarted -= HideNickname;
        SlidePanelStateController.SwitchToNormalCompleted -= ShowNickname;
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

    public void ShowNickname()
    {
        _nicknameObjectsDesiredActive = true;
        RefreshNicknameObjectsActive();
    }

    public void HideNickname()
    {
        _nicknameObjectsDesiredActive = false;
        RefreshNicknameObjectsActive();
    }

    private void RefreshNicknameObjectsActive()
    {
        bool hasParty = LobbyPartyService.Instance != null && LobbyPartyService.Instance.LocalPlayerHasParty;
        bool active = _nicknameObjectsDesiredActive;

        if (_myNickname != null)
            _myNickname.SetActive(active);
        if (_friendNickname != null)
            _friendNickname.SetActive(active && hasParty);
    }

    private void HandlePartyPartnerLinked(Player _)
    {
        RefreshNicknameObjectsActive();
    }

    private void HandlePartyCleared()
    {
        RefreshNicknameObjectsActive();
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

    private void OnNicknameChangePopupRequested()
    {
        ShowNicknameChange();
    }

    private void OnLobbyUiTransientToastRequested(string message)
    {
        ShowTransientToast(message);
    }

    private void OnPartyInviteTransientToastRequested(string message)
    {
        ShowTransientToast(message);
    }

    private void HandlePartyInviteReceived(int inviterActor, string inviterUserId)
    {
        _partyInvitePopup.SetInviteMessage(inviterActor, inviterUserId);
        ShowPopup(LobbyPopupKind.PartyInvite);
    }

    private void HandlePartyInviteResponded(bool accepted)
    {
        if (!accepted)
            ShowTransientToast(_partyInvitePopup.PartyInviteDeclinedToast);
    }

    private void HandlePendingInviteInvalidated()
    {
        CloseAllPopups();
    }

    private void OnPartyInviteClosedOnly()
    {
        CloseAllPopups();
    }

    private void ShowTransientToast(string message)
    {
        if (_transientToastText == null)
            return;

        if (_transientToastCoroutine != null)
            StopCoroutine(_transientToastCoroutine);

        _transientToastText.text = message;
        _transientToastText.gameObject.SetActive(true);
        _transientToastCoroutine = StartCoroutine(HideTransientToastAfterDelay());
    }

    private IEnumerator HideTransientToastAfterDelay()
    {
        yield return new WaitForSeconds(_transientToastDurationSeconds);
        if (_transientToastText != null)
            _transientToastText.gameObject.SetActive(false);
        _transientToastCoroutine = null;
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
