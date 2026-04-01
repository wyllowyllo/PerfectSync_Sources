using System;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class QuitPopup : LobbyPopupBase
{
    public static event Action CancelRequested;

    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;

    private void OnEnable()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        if (_cancelButton != null)
            _cancelButton.onClick.AddListener(OnCancelClicked);
    }

    private void OnDisable()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (_cancelButton != null)
            _cancelButton.onClick.RemoveListener(OnCancelClicked);
    }

    private void OnConfirmClicked()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnCancelClicked()
    {
        CancelRequested?.Invoke();
    }
}
