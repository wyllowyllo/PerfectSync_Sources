using UnityEngine;

/// <summary>
/// <see cref="LobbyManager"/>의 매칭/메인 화면 이벤트에 맞춰 마스크를 켜고 끕니다.
/// 기본은 꺼진 상태이며, 매칭 화면으로 전환될 때만 켜집니다.
/// </summary>
public class LobbyUIMaskToggle : UIMaskToggle
{
    private void Awake()
    {
        DisableMask();
    }

    private void Start()
    {
        if (LobbyManager.Instance == null)
            return;

        LobbyManager.Instance.ShowMatchingScreenRequested += OnShowMatchingScreenRequested;
        LobbyManager.Instance.ShowMainScreenRequested += OnShowMainScreenRequested;
    }

    private void OnDisable()
    {
        if (LobbyManager.Instance == null)
            return;

        LobbyManager.Instance.ShowMatchingScreenRequested -= OnShowMatchingScreenRequested;
        LobbyManager.Instance.ShowMainScreenRequested -= OnShowMainScreenRequested;
    }

    private void OnShowMatchingScreenRequested()
    {
        EnableMask();
    }

    private void OnShowMainScreenRequested()
    {
        DisableMask();
    }
}
