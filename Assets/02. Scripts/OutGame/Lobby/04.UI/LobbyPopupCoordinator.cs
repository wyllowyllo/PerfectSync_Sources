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
    [SerializeField] private Button _openNicknameChangeButton;
    [SerializeField] private Button _openInviteFriendsButton;

    public void ShowPopup(LobbyPopupKind kind)
    {
        if (kind == LobbyPopupKind.EscMenu)
        {
            _escMenuPopup?.Show();
            return;
        }

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
        EscMenuPopup.RaiseCoordinatorClosedAllPopups();
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
    }

    private static void SetActiveIfExists(GameObject go, bool active)
    {
        if (go != null)
            go.SetActive(active);
    }

    /*
    private Coroutine _toastHideRoutine;

    private void Start()
    {
        EscMenuPopup.CloseAllPopupsAndEscMenuRequested += HandleEscCloseAllPopupsAndEscMenu;
        EscMenuPopup.OpenSettingsFromEscMenuRequested += HandleEscOpenSettingsFromMenu;
        EscMenuPopup.OpenQuitFromEscMenuRequested += HandleEscOpenQuitFromMenu;
        QuitPopup.CancelRequested += HandleQuitPopupCancelRequested;
        PartyInvitePopup.OpenPartyInviteFlowRequested += HandleOpenPartyInviteFlow;
        PartyInvitePopup.PartyInviteClosedOnlyRequested += HandlePartyInviteClosedOnly;
        PartyInvitePopup.TransientToastRequested += HandlePartyInviteTransientToast;

        if (_openSettingsButton != null)
            _openSettingsButton.onClick.AddListener(ShowSettings);
        if (_openNicknameChangeButton != null)
            _openNicknameChangeButton.onClick.AddListener(ShowNicknameChange);
        if (_openInviteFriendsButton != null)
            _openInviteFriendsButton.onClick.AddListener(ShowFollowFriends);

        if (LobbyPartyService.Instance != null)
        {
            LobbyPartyService.Instance.OnPartyPartnerLinked += HandlePartyLinkedRefreshVisuals;
            LobbyPartyService.Instance.OnPartyCleared += HandlePartyClearedRefreshVisuals;
        }

        RefreshPartyInviteButtonVisuals();
    }

    private void OnDisable()
    {
        EscMenuPopup.CloseAllPopupsAndEscMenuRequested -= HandleEscCloseAllPopupsAndEscMenu;
        EscMenuPopup.OpenSettingsFromEscMenuRequested -= HandleEscOpenSettingsFromMenu;
        EscMenuPopup.OpenQuitFromEscMenuRequested -= HandleEscOpenQuitFromMenu;
        QuitPopup.CancelRequested -= HandleQuitPopupCancelRequested;
        PartyInvitePopup.OpenPartyInviteFlowRequested -= HandleOpenPartyInviteFlow;
        PartyInvitePopup.PartyInviteClosedOnlyRequested -= HandlePartyInviteClosedOnly;
        PartyInvitePopup.TransientToastRequested -= HandlePartyInviteTransientToast;

        if (_openSettingsButton != null)
            _openSettingsButton.onClick.RemoveListener(ShowSettings);
        if (_openNicknameChangeButton != null)
            _openNicknameChangeButton.onClick.RemoveListener(ShowNicknameChange);
        if (_openInviteFriendsButton != null)
            _openInviteFriendsButton.onClick.RemoveListener(ShowFollowFriends);

        if (LobbyPartyService.Instance != null)
        {
            LobbyPartyService.Instance.OnPartyPartnerLinked -= HandlePartyLinkedRefreshVisuals;
            LobbyPartyService.Instance.OnPartyCleared -= HandlePartyClearedRefreshVisuals;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleEscape();
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
        if (_popupBackgroundRoot != null)
            return _popupBackgroundRoot.activeSelf;
        return AnyPopupContentVisible() || (_quitPopup != null && _quitPopup.IsShowing);
    }

    private bool AnyPopupContentVisible()
    {
        return (_settingsPopup != null && _settingsPopup.IsShowing)
               || (_nicknameChangePopup != null && _nicknameChangePopup.IsShowing)
               || (_inviteFriendsPopup != null && _inviteFriendsPopup.IsShowing)
               || (_partyInvitePopup != null && _partyInvitePopup.IsShowing);
    }

    private void HandleEscCloseAllPopupsAndEscMenu()
    {
        CloseAllPopups();
    }

    private void HandleEscOpenSettingsFromMenu()
    {
        ShowSettings();
    }

    private void HandleEscOpenQuitFromMenu()
    {
        ShowQuitPanel();
    }

    private void HandleQuitPopupCancelRequested()
    {
        CloseAllPopups();
    }

    private void HandleOpenPartyInviteFlow()
    {
        ShowPopup(LobbyPopupKind.PartyInvite);
    }

    private void HandlePartyInviteClosedOnly()
    {
        if (!AnyPopupContentVisible())
            HidePopupBackground();
    }

    private void HandlePartyInviteTransientToast(string message)
    {
        ShowTransientToast(message);
    }

    private void HandlePartyLinkedRefreshVisuals(Player _)
    {
        RefreshPartyInviteButtonVisuals();
    }

    private void HandlePartyClearedRefreshVisuals()
    {
        RefreshPartyInviteButtonVisuals();
    }

    private void RefreshPartyInviteButtonVisuals()
    {
        bool inParty = LobbyPartyService.Instance != null && LobbyPartyService.Instance.LocalPlayerHasParty;
        SetActiveIfExists(_openInviteFriendsButtonInviteImage, !inParty);
        SetActiveIfExists(_openInviteFriendsButtonBackImage, inParty);
    }

    public void ShowTransientToast(string message)
    {
        if (_transientToastText == null)
            return;

        if (_toastHideRoutine != null)
        {
            StopCoroutine(_toastHideRoutine);
            _toastHideRoutine = null;
        }

        _transientToastText.text = message ?? string.Empty;
        _transientToastText.gameObject.SetActive(true);
        _toastHideRoutine = StartCoroutine(HideTransientToastAfterDelay());
    }

    private IEnumerator HideTransientToastAfterDelay()
    {
        yield return new WaitForSeconds(_transientToastDurationSeconds);
        if (_transientToastText != null)
            _transientToastText.gameObject.SetActive(false);
        _toastHideRoutine = null;
    }
    */
}
