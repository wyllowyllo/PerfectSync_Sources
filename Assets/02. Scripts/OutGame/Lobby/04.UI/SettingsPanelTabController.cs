using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelTabController : MonoBehaviour
{
    [SerializeField] private Button _soundTabButton;
    [SerializeField] private Button _screenTabButton;
    [SerializeField] private Button _controlTabButton;

    [SerializeField] private GameObject _soundSettingsRoot;
    [SerializeField] private GameObject _screenSettingsRoot;
    [SerializeField] private GameObject _controlSettingsRoot;

    private void OnEnable()
    {
        if (_soundTabButton != null)
            _soundTabButton.onClick.AddListener(SelectSound);
        if (_screenTabButton != null)
            _screenTabButton.onClick.AddListener(SelectScreen);
        if (_controlTabButton != null)
            _controlTabButton.onClick.AddListener(SelectControl);

        SelectSound();
    }

    private void OnDisable()
    {
        if (_soundTabButton != null)
            _soundTabButton.onClick.RemoveListener(SelectSound);
        if (_screenTabButton != null)
            _screenTabButton.onClick.RemoveListener(SelectScreen);
        if (_controlTabButton != null)
            _controlTabButton.onClick.RemoveListener(SelectControl);
    }

    private void SelectSound() => SetTab(0);

    private void SelectScreen() => SetTab(1);

    private void SelectControl() => SetTab(2);

    private void SetTab(int index)
    {
        Button[] buttons = { _soundTabButton, _screenTabButton, _controlTabButton };
        GameObject[] panels = { _soundSettingsRoot, _screenSettingsRoot, _controlSettingsRoot };

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].interactable = i != index;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
                panels[i].SetActive(i == index);
        }
    }
}
