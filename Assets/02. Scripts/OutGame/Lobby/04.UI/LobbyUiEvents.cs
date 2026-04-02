using System;

/// <summary>
/// 로비 UI 간 직접 참조 없이 알림·팝업 요청을 전달하는 정적 채널입니다.
/// </summary>
public static class LobbyUiEvents
{
    public static event Action<string> TransientToastRequested;

    public static void RequestTransientToast(string message) =>
        TransientToastRequested?.Invoke(message);

    public static event Action NicknameChangePopupRequested;

    public static void RequestNicknameChangePopup() =>
        NicknameChangePopupRequested?.Invoke();
}
