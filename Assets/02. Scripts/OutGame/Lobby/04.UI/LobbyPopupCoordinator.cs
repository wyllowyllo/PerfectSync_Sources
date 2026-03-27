using UnityEngine;
using UnityEngine.UI;

public class LobbyPopupCoordinator : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject _popupPanelRoot;

    [Header("Panels (children under PopupPanel)")]
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private GameObject _nicknameChangePanel;
    [SerializeField] private GameObject _followFriendsPanel;
    [SerializeField] private GameObject _quitPanel;

    [Header("Optional — leave empty if you wire buttons only in Inspector OnClick")]
    [SerializeField] private Button _openInviteFriendsButton;
    [SerializeField] private Button _openNicknameChangeButton;
    [SerializeField] private Button _openSettingsButton;

    private void OnEnable()
    {
        if (_openSettingsButton != null)
            _openSettingsButton.onClick.AddListener(ShowSettings);
        if (_openNicknameChangeButton != null)
            _openNicknameChangeButton.onClick.AddListener(ShowNicknameChange);
        if (_openInviteFriendsButton != null)
            _openInviteFriendsButton.onClick.AddListener(ShowFollowFriends);
    }

    private void OnDisable()
    {
        if (_openSettingsButton != null)
            _openSettingsButton.onClick.RemoveListener(ShowSettings);
        if (_openNicknameChangeButton != null)
            _openNicknameChangeButton.onClick.RemoveListener(ShowNicknameChange);
        if (_openInviteFriendsButton != null)
            _openInviteFriendsButton.onClick.RemoveListener(ShowFollowFriends);
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
        ShowSinglePopup(_followFriendsPanel);
    }

    public void CloseAllPopups()
    {
        SetActiveIfExists(_settingsPanel, false);
        SetActiveIfExists(_nicknameChangePanel, false);
        SetActiveIfExists(_followFriendsPanel, false);
        SetActiveIfExists(_quitPanel, false);
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
        SetActiveIfExists(_followFriendsPanel, false);
        SetActiveIfExists(_quitPanel, false);

        _popupPanelRoot.SetActive(true);
        target.SetActive(true);
    }

    private static void SetActiveIfExists(GameObject go, bool active)
    {
        if (go != null)
            go.SetActive(active);
    }

    private bool AnyNonQuitPopupVisible()
    {
        return IsActive(_settingsPanel) || IsActive(_nicknameChangePanel) || IsActive(_followFriendsPanel);
    }

    private bool IsQuitPanelVisible()
    {
        return IsActive(_quitPanel);
    }

    private static bool IsActive(GameObject go)
    {
        return go != null && go.activeInHierarchy;
    }
}
