using UnityEngine;

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
