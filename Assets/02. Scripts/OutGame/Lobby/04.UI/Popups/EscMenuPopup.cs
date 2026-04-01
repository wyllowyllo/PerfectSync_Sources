using System;
using UnityEngine;
using UnityEngine.UI;

public class EscMenuPopup : LobbyPopupBase
{
    public static event Action CloseAllPopupsAndEscMenuRequested;
    public static event Action OpenSettingsFromEscMenuRequested;
    public static event Action OpenQuitFromEscMenuRequested;

    public static event Action CoordinatorClosedAllPopups;

    [Header("버튼")]
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _optionButton;
    [SerializeField] private Button _quitButton;

    private void OnEnable()
    {
        if (_backButton != null)
            _backButton.onClick.AddListener(OnBackClicked);
        if (_optionButton != null)
            _optionButton.onClick.AddListener(OnOptionClicked);
        if (_quitButton != null)
            _quitButton.onClick.AddListener(OnQuitClicked);
    }
    private void OnDisable()
    {
        if (_backButton != null)
            _backButton.onClick.RemoveListener(OnBackClicked);
        if (_optionButton != null)
            _optionButton.onClick.RemoveListener(OnOptionClicked);
        if (_quitButton != null)
            _quitButton.onClick.RemoveListener(OnQuitClicked);
    }

    private void OnBackClicked()
    {
        CloseAllPopupsAndEscMenuRequested?.Invoke();
    }

    private void OnOptionClicked()
    {
        gameObject.SetActive(false);
        OpenSettingsFromEscMenuRequested?.Invoke();
    }

    private void OnQuitClicked()
    {
        gameObject.SetActive(false);
        OpenQuitFromEscMenuRequested?.Invoke();
    }
}
