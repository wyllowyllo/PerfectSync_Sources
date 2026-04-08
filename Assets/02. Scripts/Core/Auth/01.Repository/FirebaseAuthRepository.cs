using System.Threading.Tasks;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using Firebase.Auth;
using Firebase;
#endif

public static class FirebaseAuthRepository
{
#if !UNITY_WEBGL || UNITY_EDITOR
    public static async Task<AuthResult> Register(string email, string password)
    {
        try
        {
            var result = await FirebaseInitializer.Instance.Auth
                .CreateUserWithEmailAndPasswordAsync(email, password);

            Debug.Log($"[FirebaseAuth] 회원가입 성공: {email}");
            return new AuthResult { Success = true, Email = email };
        }
        catch (FirebaseException e)
        {
            Debug.LogError($"[FirebaseAuth] 회원가입 실패: {e.Message}");
            return new AuthResult { Success = false, ErrorMessage = GetErrorMessage(e) };
        }
    }

    public static async Task<AuthResult> Login(string email, string password)
    {
        try
        {
            var result = await FirebaseInitializer.Instance.Auth
                .SignInWithEmailAndPasswordAsync(email, password);

            Debug.Log($"[FirebaseAuth] 로그인 성공: {email}");
            return new AuthResult { Success = true, Email = email };
        }
        catch (FirebaseException e)
        {
            Debug.LogError($"[FirebaseAuth] 로그인 실패: {e.Message}");
            return new AuthResult { Success = false, ErrorMessage = GetErrorMessage(e) };
        }
    }

    public static void Logout()
    {
        FirebaseInitializer.Instance.Auth.SignOut();
        Debug.Log("[FirebaseAuth] 로그아웃 완료");
    }

    /// <summary>현재 로그인된 유저의 이메일.</summary>
    public static string GetCurrentUserEmail()
    {
        var user = FirebaseInitializer.Instance.Auth.CurrentUser;
        if (user == null || string.IsNullOrEmpty(user.Email))
            return null;

        return user.Email;
    }

    private static string GetErrorMessage(FirebaseException e)
    {
        return e.ErrorCode switch
        {
            (int)AuthError.EmailAlreadyInUse => "이미 사용 중인 이메일입니다.",
            (int)AuthError.InvalidEmail      => "유효하지 않은 이메일 형식입니다.",
            (int)AuthError.WeakPassword      => $"비밀번호가 조건을 충족하지 않습니다.\n{e.Message}",
            (int)AuthError.WrongPassword     => "이메일 또는 비밀번호를 확인해주세요.",
            (int)AuthError.UserNotFound      => "이메일 또는 비밀번호를 확인해주세요.",
            (int)AuthError.Failure           => "이메일 또는 비밀번호를 확인해주세요.",
            _ => $"인증 오류가 발생했습니다. ({e.Message})"
        };
    }
#else
    public static Task<AuthResult> Register(string email, string password)
    {
        Debug.LogWarning("[FirebaseAuth] WebGL: Firebase 사용 불가");
        return Task.FromResult(new AuthResult { Success = false, ErrorMessage = "WebGL에서는 사용할 수 없습니다." });
    }

    public static Task<AuthResult> Login(string email, string password)
    {
        Debug.LogWarning("[FirebaseAuth] WebGL: Firebase 사용 불가");
        return Task.FromResult(new AuthResult { Success = false, ErrorMessage = "WebGL에서는 사용할 수 없습니다." });
    }

    public static void Logout()
    {
        Debug.LogWarning("[FirebaseAuth] WebGL: Firebase 사용 불가");
    }

    public static string GetCurrentUserEmail() => null;
#endif
}
