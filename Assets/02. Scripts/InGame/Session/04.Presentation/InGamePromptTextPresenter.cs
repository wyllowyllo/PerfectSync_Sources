using System;
using UnityEngine;

public class InGamePromptTextPresenter : MonoBehaviour
{
    [SerializeField] private GameObject _promptObject;

    public event Action OnPromptShown;
    public event Action OnPromptHidden;

    private void Start()
    {
        if (InGameManager.Instance != null)
            InGameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDestroy()
    {
        if (InGameManager.Instance != null)
            InGameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
            Hide();
    }

    public void Show()
    {
        if (_promptObject == null || _promptObject.activeSelf) return;

        _promptObject.SetActive(true);
        OnPromptShown?.Invoke();
    }

    public void Hide()
    {
        if (_promptObject == null || !_promptObject.activeSelf) return;

        _promptObject.SetActive(false);
        OnPromptHidden?.Invoke();
    }
}
