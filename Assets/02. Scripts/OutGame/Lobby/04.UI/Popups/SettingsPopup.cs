using UnityEngine;
using UnityEngine.UI;

public class SettingsPopup : LobbyPopupBase
{
    [Header("탭 버튼")]
    [SerializeField] private Button _soundTabButton;
    [SerializeField] private Button _screenTabButton;
    [SerializeField] private Button _controlTabButton;

    [Header("탭 앞 이미지 (버튼 비활성 시 색 보정)")]
    [SerializeField] private Image _soundTabFrontImage;
    [SerializeField] private Image _screenTabFrontImage;
    [SerializeField] private Image _controlTabFrontImage;

    [SerializeField] private Color _nonInteractableImageMultiplier = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("탭별 패널")]
    [SerializeField] private GameObject _soundSettingsRoot;
    [SerializeField] private GameObject _screenSettingsRoot;
    [SerializeField] private GameObject _controlSettingsRoot;

    private Color[] _tabFrontImageBaseColors;

    private void OnEnable()
    {
        CacheTabFrontImageColors();

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

        RestoreTabFrontImageColors();
    }

    private void CacheTabFrontImageColors()
    {
        Image[] images = { _soundTabFrontImage, _screenTabFrontImage, _controlTabFrontImage };
        _tabFrontImageBaseColors = new Color[images.Length];
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null)
                _tabFrontImageBaseColors[i] = images[i].color;
            else
                _tabFrontImageBaseColors[i] = Color.white;
        }
    }

    private void RestoreTabFrontImageColors()
    {
        Image[] images = { _soundTabFrontImage, _screenTabFrontImage, _controlTabFrontImage };
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null)
                images[i].color = _tabFrontImageBaseColors[i];
        }
    }

    private void SelectSound() => SetTab(0);

    private void SelectScreen() => SetTab(1);

    private void SelectControl() => SetTab(2);

    private void SetTab(int index)
    {
        Button[] buttons = { _soundTabButton, _screenTabButton, _controlTabButton };
        GameObject[] panels = { _soundSettingsRoot, _screenSettingsRoot, _controlSettingsRoot };
        Image[] frontImages = { _soundTabFrontImage, _screenTabFrontImage, _controlTabFrontImage };

        for (int i = 0; i < buttons.Length; i++)
        {
            bool interactable = i != index;
            if (buttons[i] != null)
                buttons[i].interactable = interactable;

            if (frontImages[i] != null && _tabFrontImageBaseColors != null && i < _tabFrontImageBaseColors.Length)
            {
                Color baseColor = _tabFrontImageBaseColors[i];
                frontImages[i].color = interactable ? baseColor : baseColor * _nonInteractableImageMultiplier;
            }
        }

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
                panels[i].SetActive(i == index);
        }
    }
}
