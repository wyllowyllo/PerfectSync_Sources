using System;

public static class UserIdGenerator
{
    private const string Prefix = "PS_";

    public static string CreateSessionUserId()
    {
        long ticks = DateTime.UtcNow.Ticks;
        int salt = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        ulong mixed = (ulong)ticks ^ (ulong)(uint)salt;
        mixed ^= mixed >> 33;
        mixed *= 0x9E3779B97F4A7C15UL;
        mixed ^= mixed >> 29;
        return Prefix + EncodeBase62Fixed5(mixed);
    }

    private static string EncodeBase62Fixed5(ulong value)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var chars = new char[5];
        for (int i = 0; i < 5; i++)
        {
            chars[i] = alphabet[(int)(value % 62)];
            value /= 62;
        }

        return new string(chars);
    }
}
