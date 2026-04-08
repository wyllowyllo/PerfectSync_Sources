using System.Linq;

public static class PasswordValidator
{
    private const int MinLength = 6;
    private const int MaxLength = 15;

    public static (bool isValid, string errorMessage) Validate(string password)
    {
        if (string.IsNullOrEmpty(password))
            return (false, "비밀번호를 입력해주세요.");

        if (password.Length < MinLength || password.Length > MaxLength)
            return (false, $"비밀번호는 {MinLength}~{MaxLength}자여야 합니다.");

        if (!password.Any(char.IsUpper))
            return (false, "비밀번호에 대문자가 포함되어야 합니다.");

        if (!password.Any(char.IsLower))
            return (false, "비밀번호에 소문자가 포함되어야 합니다.");

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            return (false, "비밀번호에 특수문자가 포함되어야 합니다.");

        return (true, null);
    }
}
