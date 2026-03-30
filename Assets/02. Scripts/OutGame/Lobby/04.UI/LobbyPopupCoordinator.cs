using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPopupCoordinator : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject _popupPanelRoot;

    [Header("Panels (children under PopupPanel)")]
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private GameObject _nicknameChangePanel;
    [SerializeField] private GameObject _inviteFriendsPanel;
    [SerializeField] private GameObject _quitPanel;
    [SerializeField] private GameObject _partyInviteReceivedPanel;

    [Header("파티 초대 수신 (PopupPanel 하위)")]
    [SerializeField] private TMP_Text _partyInviteMessageText;
    [SerializeField] private Button _partyInviteConfirmButton;
    [SerializeField] private Button _partyInviteCancelButton;
    [SerializeField] private string _partyInviteMessageFormat = "{0}님이 파티 초대를 보냈습니다.";

    [Header("일시 알림 (TMP가 붙은 오브젝트를 켜고 끔)")]
    [SerializeField] private TMP_Text _transientToastText;
    [SerializeField] private float _transientToastDurationSeconds = 3f;
    [SerializeField] private string _partyInviteDeclinedToast = "상대가 파티 초대를 거절했습니다.";

    [Header("파티 초대 버튼 비주얼 (동시에 하나만 활성)")]
    [SerializeField] private GameObject _openInviteFriendsButtonInviteImage;
    [SerializeField] private GameObject _openInviteFriendsButtonBackImage;

    [Header("Optional")]
    [SerializeField] private Button _openSettingsButton;
    [SerializeField] private Button _openNicknameChangeButton;
    [SerializeField] private Button _openInviteFriendsButton;

    private Coroutine _toastHideRoutine;

    private void Start()
    {
        if (_openSettingsButton != null)
            _openSettingsButton.onClick.AddListener(ShowSettings);
        if (_openNicknameChangeButton != null)
            _openNicknameChangeButton.onClick.AddListener(ShowNicknameChange);
        if (_openInviteFriendsButton != null)
            _openInviteFriendsButton.onClick.AddListener(ShowFollowFriends);

        if (_partyInviteConfirmButton != null)
            _partyInviteConfirmButton.onClick.AddListener(OnPartyInviteConfirmClicked);
        if (_partyInviteCancelButton != null)
            _partyInviteCancelButton.onClick.AddListener(OnPartyInviteCancelClicked);

        if (LobbyPartyService.Instance != null)
        {
            LobbyPartyService.Instance.OnPartyInviteReceived += HandlePartyInviteReceived;
            LobbyPartyService.Instance.OnPartyInviteResponded += HandlePartyInviteResponded;
            LobbyPartyService.Instance.OnPartyPartnerLinked += HandlePartyLinkedRefreshVisuals;
            LobbyPartyService.Instance.OnPartyCleared += HandlePartyClearedRefreshVisuals;
            LobbyPartyService.Instance.OnPendingPartyInviteInvalidated += HandlePendingPartyInviteInvalidated;
        }

        RefreshPartyInviteButtonVisuals();
    }

    private void OnDisable()
    {
        if (_openSettingsButton != null)
            _openSettingsButton.onClick.RemoveListener(ShowSettings);
        if (_openNicknameChangeButton != null)
            _openNicknameChangeButton.onClick.RemoveListener(ShowNicknameChange);
        if (_openInviteFriendsButton != null)
            _openInviteFriendsButton.onClick.RemoveListener(ShowFollowFriends);

        if (_partyInviteConfirmButton != null)
            _partyInviteConfirmButton.onClick.RemoveListener(OnPartyInviteConfirmClicked);
        if (_partyInviteCancelButton != null)
            _partyInviteCancelButton.onClick.RemoveListener(OnPartyInviteCancelClicked);

        if (LobbyPartyService.Instance != null)
        {
            LobbyPartyService.Instance.OnPartyInviteReceived -= HandlePartyInviteReceived;
            LobbyPartyService.Instance.OnPartyInviteResponded -= HandlePartyInviteResponded;
            LobbyPartyService.Instance.OnPartyPartnerLinked -= HandlePartyLinkedRefreshVisuals;
            LobbyPartyService.Instance.OnPartyCleared -= HandlePartyClearedRefreshVisuals;
            LobbyPartyService.Instance.OnPendingPartyInviteInvalidated -= HandlePendingPartyInviteInvalidated;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleEscape();
    }

    private void HandleEscape()
    {
        if (_popupPanelRoot == null)
            return;

        if (IsActive(_partyInviteReceivedPanel))
        {
            OnPartyInviteCancelClicked();
            return;
        }

        if (!_popupPanelRoot.activeSelf)
        {
            ShowQuitPanel();
            return;
        }

        if (IsQuitPanelVisible())
        {
            CloseAllPopups();
            return;
        }

        if (AnyNonQuitPopupVisible())
        {
            CloseAllPopups();
            return;
        }

        ShowQuitPanel();
    }

    public void ShowSettings()
    {
        ShowSinglePopup(_settingsPanel);
    }

    public void ShowNicknameChange()
    {
        ShowSinglePopup(_nicknameChangePanel);
    }

    public void ShowFollowFriends()
    {
        ShowSinglePopup(_inviteFriendsPanel);
    }

    public void CloseAllPopups()
    {
        SetActiveIfExists(_settingsPanel, false);
        SetActiveIfExists(_nicknameChangePanel, false);
        SetActiveIfExists(_inviteFriendsPanel, false);
        SetActiveIfExists(_quitPanel, false);
        SetActiveIfExists(_partyInviteReceivedPanel, false);
        SetActiveIfExists(_popupPanelRoot, false);
    }

    private void ShowQuitPanel()
    {
        ShowSinglePopup(_quitPanel);
    }

    private void ShowSinglePopup(GameObject target)
    {
        if (_popupPanelRoot == null || target == null)
            return;

        SetActiveIfExists(_settingsPanel, false);
        SetActiveIfExists(_nicknameChangePanel, false);
        SetActiveIfExists(_inviteFriendsPanel, false);
        SetActiveIfExists(_quitPanel, false);
        SetActiveIfExists(_partyInviteReceivedPanel, false);

        _popupPanelRoot.SetActive(true);
        target.SetActive(true);
    }

    private void HandlePartyInviteReceived(int inviterActor, string inviterUserId)
    {
        if (_partyInviteReceivedPanel == null)
            return;

        string displayName = inviterUserId;
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
        {
            Player inviter = PhotonNetwork.CurrentRoom.GetPlayer(inviterActor);
            if (inviter != null && !string.IsNullOrEmpty(inviter.NickName))
                displayName = inviter.NickName;
        }

        if (_partyInviteMessageText != null)
            _partyInviteMessageText.text = string.Format(_partyInviteMessageFormat, displayName);

        SetActiveIfExists(_settingsPanel, false);
        SetActiveIfExists(_nicknameChangePanel, false);
        SetActiveIfExists(_inviteFriendsPanel, false);
        SetActiveIfExists(_quitPanel, false);

        if (_popupPanelRoot != null)
            _popupPanelRoot.SetActive(true);
        _partyInviteReceivedPanel.SetActive(true);
    }

    private void OnPartyInviteConfirmClicked()
    {
        LobbyPartyService.Instance?.RespondToPendingPartyInvite(true);
        HidePartyInvitePanelOnly();
    }

    private void OnPartyInviteCancelClicked()
    {
        LobbyPartyService.Instance?.RespondToPendingPartyInvite(false);
        HidePartyInvitePanelOnly();
    }

    private void HidePartyInvitePanelOnly()
    {
        SetActiveIfExists(_partyInviteReceivedPanel, false);
        if (!AnyPopupContentVisible())
            SetActiveIfExists(_popupPanelRoot, false);
    }

    private void HandlePartyInviteResponded(bool accepted)
    {
        if (!accepted)
            ShowTransientToast(_partyInviteDeclinedToast);
    }

    private void HandlePartyLinkedRefreshVisuals(Player _)
    {
        RefreshPartyInviteButtonVisuals();
    }

    private void HandlePartyClearedRefreshVisuals()
    {
        RefreshPartyInviteButtonVisuals();
    }

    private void HandlePendingPartyInviteInvalidated()
    {
        HidePartyInvitePanelOnly();
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

    private bool AnyPopupContentVisible()
    {
        return IsActive(_settingsPanel) || IsActive(_nicknameChangePanel) || IsActive(_inviteFriendsPanel) ||
               IsActive(_partyInviteReceivedPanel);
    }

    private bool AnyNonQuitPopupVisible()
    {
        return AnyPopupContentVisible();
    }

    private bool IsQuitPanelVisible()
    {
        return IsActive(_quitPanel);
    }

    private static void SetActiveIfExists(GameObject go, bool active)
    {
        if (go != null)
            go.SetActive(active);
    }

    private static bool IsActive(GameObject go)
    {
        return go != null && go.activeInHierarchy;
    }
}
