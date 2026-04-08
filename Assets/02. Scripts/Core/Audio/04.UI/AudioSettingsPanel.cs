using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsPanel : MonoBehaviour
{
    private const int DisplayScale = 100;

    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;

    [SerializeField] private TMP_Text _masterValueText;
    [SerializeField] private TMP_Text _bgmValueText;
    [SerializeField] private TMP_Text _sfxValueText;

    private void OnEnable()
    {
        ConfigureSliders();

        if (_masterSlider != null)
            _masterSlider.onValueChanged.AddListener(OnMasterSliderChanged);
        if (_bgmSlider != null)
            _bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
        if (_sfxSlider != null)
            _sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);

        AudioManager.VolumesChanged += OnAudioVolumesChanged;
        RefreshFromAudioManager();
    }

    private void OnDisable()
    {
        if (_masterSlider != null)
            _masterSlider.onValueChanged.RemoveListener(OnMasterSliderChanged);
        if (_bgmSlider != null)
            _bgmSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
        if (_sfxSlider != null)
            _sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);

        AudioManager.VolumesChanged -= OnAudioVolumesChanged;
    }

    private void ConfigureSliders()
    {
        ConfigureSlider(_masterSlider);
        ConfigureSlider(_bgmSlider);
        ConfigureSlider(_sfxSlider);
    }

    private static void ConfigureSlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = DisplayScale;
        slider.wholeNumbers = true;
    }

    private void OnAudioVolumesChanged()
    {
        RefreshFromAudioManager();
    }

    private void RefreshFromAudioManager()
    {
        if (AudioManager.Instance == null)
            return;

        float m = AudioManager.Instance.MasterVolume * DisplayScale;
        float b = AudioManager.Instance.BgmVolume * DisplayScale;
        float s = AudioManager.Instance.SfxVolume * DisplayScale;

        if (_masterSlider != null)
            _masterSlider.SetValueWithoutNotify(m);
        if (_bgmSlider != null)
            _bgmSlider.SetValueWithoutNotify(b);
        if (_sfxSlider != null)
            _sfxSlider.SetValueWithoutNotify(s);

        SetLabel(_masterValueText, _masterSlider != null ? Mathf.RoundToInt(_masterSlider.value) : Mathf.RoundToInt(m));
        SetLabel(_bgmValueText, _bgmSlider != null ? Mathf.RoundToInt(_bgmSlider.value) : Mathf.RoundToInt(b));
        SetLabel(_sfxValueText, _sfxSlider != null ? Mathf.RoundToInt(_sfxSlider.value) : Mathf.RoundToInt(s));
    }

    private static void SetLabel(TMP_Text label, int value)
    {
        if (label == null)
            return;

        int clamped = Mathf.Clamp(value, 0, DisplayScale);
        label.text = $"{clamped}/{DisplayScale}";
    }

    private void OnMasterSliderChanged(float value)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetMasterVolume(value / DisplayScale);
    }

    private void OnBgmSliderChanged(float value)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetBgmVolume(value / DisplayScale);
    }

    private void OnSfxSliderChanged(float value)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.SetSfxVolume(value / DisplayScale);
    }
}
