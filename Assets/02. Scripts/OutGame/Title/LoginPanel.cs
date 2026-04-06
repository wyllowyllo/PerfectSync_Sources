using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginPanel : MonoBehaviour
{
    [Header("입력 필드")]
    [SerializeField] private TMP_InputField _userIdInput;
    [SerializeField] private TMP_InputField _passwordInput;

    [Header("버튼")]
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _registerButton;

    [Header("메시지")]
    [SerializeField] private TMP_Text _messageText;

    [Header("씬 전환")]
    [SerializeField] private string _lobbySceneName = "Lobby";

    private bool _isProcessing;

    private void OnEnable()
    {
        _loginButton.onClick.AddListener(OnLoginClicked);
        _registerButton.onClick.AddListener(OnRegisterClicked);
        ClearMessage();
    }

    private void OnDisable()
    {
        _loginButton.onClick.RemoveListener(OnLoginClicked);
        _registerButton.onClick.RemoveListener(OnRegisterClicked);
    }

    private void OnLoginClicked()
    {
        if (_isProcessing) return;

        string userId = _userIdInput.text?.Trim();
        string password = _passwordInput.text;

        if (!ValidateInput(userId, password))
            return;

        SetProcessing(true);
        ShowMessage("로그인 중...");

        AuthService.Instance.RequestLogin(userId, password, OnAuthComplete);
    }

    private void OnRegisterClicked()
    {
        if (_isProcessing) return;

        string userId = _userIdInput.text?.Trim();
        string password = _passwordInput.text;

        if (!ValidateInput(userId, password))
            return;

        SetProcessing(true);
        ShowMessage("회원가입 중...");

        AuthService.Instance.RequestRegister(userId, password, OnAuthComplete);
    }

    private void OnAuthComplete(AuthResult result)
    {
        SetProcessing(false);

        if (result.Success)
        {
            ShowMessage("성공! 로비로 이동합니다...");
            TransitionToLobby();
        }
        else
        {
            ShowMessage(result.ErrorMessage);
        }
    }

    private bool ValidateInput(string userId, string password)
    {
        if (string.IsNullOrEmpty(userId))
        {
            ShowMessage("ID를 입력해주세요.");
            return false;
        }

        if (userId.Length < 3)
        {
            ShowMessage("ID는 3자 이상이어야 합니다.");
            return false;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowMessage("비밀번호를 입력해주세요.");
            return false;
        }

        if (password.Length < 6)
        {
            ShowMessage("비밀번호는 6자 이상이어야 합니다.");
            return false;
        }

        return true;
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
}
