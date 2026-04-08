using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class AuthService : MonoBehaviour
{
    private static AuthService s_instance;
    public static AuthService Instance => s_instance;

    public bool IsLoggedIn { get; private set; }
    public string CurrentUserEmail { get; private set; }

    public event Action<string> OnLoginSuccess;

    private void Awake()
    {
        if (s_instance == null)
        {
            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (s_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void RequestLogin(string email, string password, Action<AuthResult> callback)
    {
        StartCoroutine(CoWaitFirebaseAndExecute(() => FirebaseAuthRepository.Login(email, password), callback));
    }

    public void RequestRegister(string email, string password, Action<AuthResult> callback)
    {
        StartCoroutine(CoWaitFirebaseAndExecute(() => FirebaseAuthRepository.Register(email, password), callback));
    }

    public void Logout()
    {
        FirebaseAuthRepository.Logout();
        IsLoggedIn = false;
        CurrentUserEmail = null;
    }

    private IEnumerator CoWaitFirebaseAndExecute(Func<Task<AuthResult>> authTask, Action<AuthResult> callback)
    {
        while (!FirebaseInitializer.Instance.IsInitialized)
            yield return null;

        var task = authTask();

        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            Debug.LogError($"[AuthService] 인증 태스크 실패: {task.Exception}");
            callback?.Invoke(new AuthResult { Success = false, ErrorMessage = "인증 처리 중 오류가 발생했습니다." });
            yield break;
        }

        AuthResult result = task.Result;

        if (result.Success)
        {
            IsLoggedIn = true;
            CurrentUserEmail = result.Email;
            OnLoginSuccess?.Invoke(result.Email);
        }

        callback?.Invoke(result);
    }
}
