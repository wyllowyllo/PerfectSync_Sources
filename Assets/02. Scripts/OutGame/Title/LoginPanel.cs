using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginPanel : MonoBehaviour
{
    [Header("입력 필드")]
    [SerializeField] private TMP_InputField _emailInput;
    [SerializeField] private TMP_InputField _passwordInput;

    [Header("버튼")]
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _registerButton;

    [Header("메시지")]
    [SerializeField] private TMP_Text _messageText;

    [Header("로딩")]
    [SerializeField] private GameObject _loadingIndicator;

    [Header("씬 전환")]
    [SerializeField] private string _lobbySceneName = "Lobby";

    private bool _isProcessing;

    private void OnEnable()
    {
        SetLoadingVisible(false);
        ShowMessage("로그인 정보를 입력해주세요.");
    }

    private void Start()
    {
        if (_loginButton != null)
            _loginButton.onClick.AddListener(OnLoginClicked);
        if (_registerButton != null)
            _registerButton.onClick.AddListener(OnRegisterClicked);
    }

    private void OnDisable()
    {
        if (_loginButton != null)
            _loginButton.onClick.RemoveListener(OnLoginClicked);
        if (_registerButton != null)
            _registerButton.onClick.RemoveListener(OnRegisterClicked);
    }

    private void OnLoginClicked()
    {
        if (_isProcessing) return;

        string email = _emailInput.text?.Trim();
        string password = _passwordInput.text;

        SetProcessing(true);
        SetLoadingVisible(true);
        ShowMessage(string.Empty);

        AuthService.Instance.RequestLogin(email, password, OnAuthComplete);
    }

    private void OnRegisterClicked()
    {
        if (_isProcessing) return;

        string email = _emailInput.text?.Trim();
        string password = _passwordInput.text;

        var (isValid, errorMessage) = PasswordValidator.Validate(password);
        if (!isValid)
        {
            ShowMessage(errorMessage);
            return;
        }

        SetProcessing(true);
        SetLoadingVisible(true);
        ShowMessage(string.Empty);

        AuthService.Instance.RequestRegister(email, password, OnAuthComplete);
    }

    private void OnAuthComplete(AuthResult result)
    {
        SetProcessing(false);

        if (result.Success)
        {
            SetLoadingVisible(true);
            TransitionToLobby();
        }
        else
        {
            SetLoadingVisible(false);
            ShowMessage(result.ErrorMessage);
        }
    }

    private void TransitionToLobby()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadSceneLocal(_lobbySceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(_lobbySceneName);
    }

    private void SetProcessing(bool processing)
    {
        _isProcessing = processing;
        _loginButton.interactable = !processing;
        _registerButton.interactable = !processing;
    }

    private void ShowMessage(string message)
    {
        if (_messageText != null)
            _messageText.text = message;
    }

    private void ClearMessage()
    {
        if (_messageText != null)
            _messageText.text = string.Empty;
    }

    private void SetLoadingVisible(bool visible)
    {
        if (_loadingIndicator != null)
            _loadingIndicator.SetActive(visible);
    }
}
