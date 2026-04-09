using System;

public static class LobbyUiEvents
{
    public static event Action<string> TransientToastRequested;

    public static void RequestTransientToast(string message) =>
        TransientToastRequested?.Invoke(message);

    public static event Action NicknameChangePopupRequested;

    public static void RequestNicknameChangePopup() =>
        NicknameChangePopupRequested?.Invoke();
}
