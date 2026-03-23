using UnityEngine;

public class LobbyUI : MonoBehaviour
{
    [Header("Screens")]
    [SerializeField] private GameObject _mainScreen;
    [SerializeField] private GameObject _matchingScreen;

    [Header("UI")]
    [SerializeField] private NicknameUI _nicknameUI;
    [SerializeField] private MatchButtonUI _matchButtonUI;
    [SerializeField] private MatchingScreenUI _matchingScreenUI;

    private LobbyManager _subscribedManager;

    private void Awake()
    {
        if (LobbyManager.Instance == null)
            Debug.LogError("[LobbyUI] LobbyManager.Instance가 없습니다. 씬에 LobbyManager가 있어야 합니다.");

        if (_nicknameUI != null)
            _nicknameUI.OnConfirmClicked += HandleNicknameConfirm;
        if (_matchButtonUI != null)
            _matchButtonUI.OnMatchClicked += HandleMatch;
        if (_matchingScreenUI != null)
            _matchingScreenUI.OnLeaveClicked += HandleLeave;
    }

    private void Start()
    {
        var m = LobbyManager.Instance;
        if (m == null) return;

        _subscribedManager = m;
        m.ShowMainScreenRequested += ShowMainScreen;
        m.ShowMatchingScreenRequested += ShowMatchingScreen;
        m.MatchButtonInteractableChanged += SetMatchButtonInteractable;
        m.MatchingStatusChanged += SetMatchingStatus;
        m.PlayerCountChanged += SetPlayerCount;
        m.LeaveButtonInteractableChanged += SetLeaveButtonInteractable;
        m.NicknameFieldSet += SetNicknameField;
    }

    private void OnDestroy()
    {
        if (_nicknameUI != null)
            _nicknameUI.OnConfirmClicked -= HandleNicknameConfirm;
        if (_matchButtonUI != null)
            _matchButtonUI.OnMatchClicked -= HandleMatch;
        if (_matchingScreenUI != null)
            _matchingScreenUI.OnLeaveClicked -= HandleLeave;

        var m = _subscribedManager;
        _subscribedManager = null;
        if (m == null) return;

        m.ShowMainScreenRequested -= ShowMainScreen;
        m.ShowMatchingScreenRequested -= ShowMatchingScreen;
        m.MatchButtonInteractableChanged -= SetMatchButtonInteractable;
        m.MatchingStatusChanged -= SetMatchingStatus;
        m.PlayerCountChanged -= SetPlayerCount;
        m.LeaveButtonInteractableChanged -= SetLeaveButtonInteractable;
        m.NicknameFieldSet -= SetNicknameField;
    }

    private void HandleNicknameConfirm(string nickname)
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnNicknameConfirmed(nickname);
    }

    private void HandleMatch()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RequestMatch(GetNicknameTrimmed());
    }

    private void HandleLeave()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RequestLeaveRoom();
    }

    public void ShowMainScreen()
    {
        if (_mainScreen != null) _mainScreen.SetActive(true);
        if (_matchingScreen != null) _matchingScreen.SetActive(false);
    }

    public void ShowMatchingScreen()
    {
        if (_mainScreen != null) _mainScreen.SetActive(false);
        if (_matchingScreen != null) _matchingScreen.SetActive(true);
    }

    public void SetMatchButtonInteractable(bool value)
    {
        if (_matchButtonUI != null)
            _matchButtonUI.SetInteractable(value);
    }

    public void SetMatchingStatus(string message)
    {
        if (_matchingScreenUI != null)
            _matchingScreenUI.SetStatus(message);
    }

    public void SetPlayerCount(int current, int max)
    {
        if (_matchingScreenUI != null)
            _matchingScreenUI.SetPlayerCount(current, max);
    }

    public void SetLeaveButtonInteractable(bool value)
    {
        if (_matchingScreenUI != null)
            _matchingScreenUI.SetLeaveButtonInteractable(value);
    }

    public void SetNicknameField(string nickname)
    {
        if (_nicknameUI != null)
            _nicknameUI.SetNickname(nickname);
    }

    private string GetNicknameTrimmed()
    {
        return _nicknameUI != null ? _nicknameUI.Nickname.Trim() : string.Empty;
    }
}
