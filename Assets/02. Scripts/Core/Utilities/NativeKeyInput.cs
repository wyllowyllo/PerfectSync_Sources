using System;

namespace Core.Utilities
{
    public static class NativeKeyInput
    {
        // ── 에디터 구현체가 주입하는 델리게이트 ──
        public static Action UpdateAction;
        public static Func<int, int, float> GetAxisFunc;
        public static Func<int, bool> IsKeyDownFunc;

        // ── 가상 키 코드 상수 ──
        // Host
        public const int VK_W = 0x57, VK_A = 0x41, VK_S = 0x53, VK_D = 0x44;
        public const int VK_SPACE = 0x20;
        // Guest
        public const int VK_UP = 0x26, VK_DOWN = 0x28, VK_LEFT = 0x25, VK_RIGHT = 0x27;
        public const int VK_RETURN = 0x0D;

        // ── 공개 API ──
        public static bool IsActive => GetAxisFunc != null;

        public static void Update() => UpdateAction?.Invoke();

        public static float GetAxis(int positiveKey, int negativeKey)
            => GetAxisFunc?.Invoke(positiveKey, negativeKey) ?? 0f;

        public static bool IsKeyDown(int vKey)
            => IsKeyDownFunc?.Invoke(vKey) ?? false;
    }
}
