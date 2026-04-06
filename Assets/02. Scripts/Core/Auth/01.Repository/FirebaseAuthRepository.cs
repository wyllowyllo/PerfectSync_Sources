using System.Threading.Tasks;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using Firebase.Auth;
using Firebase;
#endif

public static class FirebaseAuthRepository
{
    private const string EmailSuffix = "@perfectsync.local";

    private static string ToEmail(string userId) => userId + EmailSuffix;

#if !UNITY_WEBGL || UNITY_EDITOR
    public static async Task<AuthResult> Register(string userId, string password)
    {
        try
        {
            var result = await FirebaseInitializer.Instance.Auth
                .CreateUserWithEmailAndPasswordAsync(ToEmail(userId), password);

            Debug.Log($"[FirebaseAuth] 회원가입 성공: {userId}");
            return new AuthResult { Success = true, UserId = userId };
        }
        catch (FirebaseException e)
        {
            Debug.LogError($"[FirebaseAuth] 회원가입 실패: {e.Message}");
            return new AuthResult { Success = false, ErrorMessage = GetErrorMessage(e) };
        }
    }

    public static async Task<AuthResult> Login(string userId, string password)
    {
        try
        {
            var result = await FirebaseInitializer.Instance.Auth
                .SignInWithEmailAndPasswordAsync(ToEmail(userId), password);

            Debug.Log($"[FirebaseAuth] 로그인 성공: {userId}");
            return new AuthResult { Success = true, UserId = userId };
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

    /// <summary>현재 로그인된 유저의 UserId (이메일에서 suffix 제거).</summary>
    public static string GetCurrentUserId()
    {
        var user = FirebaseInitializer.Instance.Auth.CurrentUser;
        if (user == null || string.IsNullOrEmpty(user.Email))
            return null;

        string email = user.Email;
        if (email.EndsWith(EmailSuffix))
            return email.Substring(0, email.Length - EmailSuffix.Length);

        return email;
    }

    private static string GetErrorMessage(FirebaseException e)
    {
        return e.ErrorCode switch
        {
            (int)AuthError.EmailAlreadyInUse => "이미 사용 중인 ID입니다.",
            (int)AuthError.InvalidEmail => "유효하지 않은 ID 형식입니다.",
            (int)AuthError.WeakPassword => "비밀번호가 너무 약합니다. (6자 이상)",
            (int)AuthError.WrongPassword => "ID 또는 비밀번호를 확인해주세요.",
            (int)AuthError.UserNotFound => "ID 또는 비밀번호를 확인해주세요.",
            _ => "인증 오류가 발생했습니다."
        };
    }
#else
    public static Task<AuthResult> Register(string userId, string password)
    {
        Debug.LogWarning("[FirebaseAuth] WebGL: Firebase 사용 불가");
        return Task.FromResult(new AuthResult { Success = false, ErrorMessage = "WebGL에서는 사용할 수 없습니다." });
    }

    public static Task<AuthResult> Login(string userId, string password)
    {
        Debug.LogWarning("[FirebaseAuth] WebGL: Firebase 사용 불가");
        return Task.FromResult(new AuthResult { Success = false, ErrorMessage = "WebGL에서는 사용할 수 없습니다." });
    }

    public static void Logout()
    {
        Debug.LogWarning("[FirebaseAuth] WebGL: Firebase 사용 불가");
    }

    public static string GetCurrentUserId() => null;
#endif
}
