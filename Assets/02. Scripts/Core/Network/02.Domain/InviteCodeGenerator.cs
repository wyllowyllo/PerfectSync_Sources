using UnityEngine;

public static class InviteCodeGenerator
{
    private const int CodeLength = 6;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// 6자리 랜덤 초대 코드를 생성합니다.
    /// 혼동 방지를 위해 0/O, 1/I/L 을 제외합니다.
    /// </summary>
    public static string Generate()
    {
        var chars = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
            chars[i] = Alphabet[Random.Range(0, Alphabet.Length)];

        return new string(chars);
    }
}
