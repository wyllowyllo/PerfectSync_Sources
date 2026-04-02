using System.Text.RegularExpressions;

/// <summary>
/// 플레이어 닉네임 입력 규칙. Photon <see cref="Photon.Pun.PhotonNetwork.NickName"/>에 넣기 전에 사용합니다.
/// </summary>
public static class NicknameValidator
{
    public const int MinLength = 2;
    public const int MaxLength = 16;

    private static readonly Regex AllowedPattern = new Regex(
        @"^[\p{L}\p{N} _-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 공백 앞뒤 제거 후 규칙을 검사합니다. 통과 시 <paramref name="trimmed"/>에 사용할 문자열이 들어갑니다.
    /// </summary>
    public static bool TryValidate(string raw, out string trimmed, out string errorMessage)
    {
        trimmed = string.IsNullOrEmpty(raw) ? string.Empty : raw.Trim();
        errorMessage = string.Empty;

        if (string.IsNullOrEmpty(trimmed))
        {
            errorMessage = "닉네임을 입력해 주세요.";
            return false;
        }

        if (trimmed.Length < MinLength)
        {
            errorMessage = $"닉네임은 {MinLength}자 이상이어야 합니다.";
            return false;
        }

        if (trimmed.Length > MaxLength)
        {
            errorMessage = $"닉네임은 {MaxLength}자 이하여야 합니다.";
            return false;
        }

        if (!AllowedPattern.IsMatch(trimmed))
        {
            errorMessage = "한글, 영문, 숫자, 띄어쓰기, 밑줄(_), 하이픈(-)만 사용할 수 있습니다.";
            return false;
        }

        return true;
    }

    /// <summary>UI 미리보기용: 현재 입력이 통과하면 참입니다.</summary>
    public static bool IsValidPreview(string raw)
    {
        return TryValidate(raw, out _, out _);
    }
}
