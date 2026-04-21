using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class AudioManager : SingletonMonoBehaviour<AudioManager>
{
    public static event Action VolumesChanged;

    private const string BgmChildNameA = "BGM_A";
    private const string BgmChildNameB = "BGM_B";
    private const string SfxChildName = "SFX";

    private AudioSource _bgmSourceA;
    private AudioSource _bgmSourceB;
    private AudioSource _activeBgmSource;
    private AudioSource _sfxSource;

    private bool _hasCurrentAddressable;
    private AddressableLoadResult<AudioClip> _currentAddressable;
    private bool _hasPreviousAddressable;
    private AddressableLoadResult<AudioClip> _previousAddressable;

    private string _currentBgmAddress;
    private bool _isBgmFading;
    private Coroutine _bgmRoutine;

    private float _masterVolume = AudioVolumeSettings.VolumeDefault;
    private float _bgmVolume = AudioVolumeSettings.VolumeDefault;
    private float _sfxVolume = AudioVolumeSettings.VolumeDefault;

    public float MasterVolume => _masterVolume;
    public float BgmVolume => _bgmVolume;
    public float SfxVolume => _sfxVolume;
    private float EffectiveBgmVolume => _masterVolume * _bgmVolume;
    private float EffectiveSfxVolume => _masterVolume * _sfxVolume;

    protected override void Awake()
    {
        base.Awake();
        CreateChildAudioSources();
        ApplyVolumeSettings(AudioSettingsRepository.Load());
        ApplyAllVolumes();
    }

    private void CreateChildAudioSources()
    {
        _bgmSourceA = CreateChildAudioSource(BgmChildNameA, loop: true);
        _bgmSourceB = CreateChildAudioSource(BgmChildNameB, loop: true);
        _bgmSourceA.volume = 0f;
        _bgmSourceB.volume = 0f;
        _activeBgmSource = _bgmSourceA;

        _sfxSource = CreateChildAudioSource(SfxChildName, loop: false);
    }

    private AudioSource CreateChildAudioSource(string childName, bool loop)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        return source;
    }

    public void Play(AudioType type, AudioClip clip)
    {
        if (clip == null)
            return;

        switch (type)
        {
            case AudioType.Bgm:
                PlayBgmImmediate(clip);
                break;
            case AudioType.Sfx:
                PlaySfx(clip);
                break;
        }
    }

    public void PlayBgmByAddress(string address, float fadeDuration = 0f)
    {
        if (string.IsNullOrEmpty(address))
            return;

        if (_currentBgmAddress == address)
            return;

        _currentBgmAddress = address;

        if (_bgmRoutine != null)
            StopCoroutine(_bgmRoutine);

        _isBgmFading = false;
        _bgmRoutine = StartCoroutine(CoLoadAndPlay(address, fadeDuration));
    }

    public void SetMasterVolume(float value)
    {
        _masterVolume = Clamp01(value);
        ApplyAllVolumes();
        PersistVolumeSettings();
    }

    public void SetBgmVolume(float value)
    {
        _bgmVolume = Clamp01(value);
        ApplyAllVolumes();
        PersistVolumeSettings();
    }

    public void SetSfxVolume(float value)
    {
        _sfxVolume = Clamp01(value);
        ApplyAllVolumes();
        PersistVolumeSettings();
    }

    private IEnumerator CoLoadAndPlay(string address, float fadeDuration)
    {
        Task<AddressableLoadResult<AudioClip>> task = AudioAssetRepository.LoadClipAsync(address);
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            Debug.LogException(task.Exception);
            yield break;
        }

        AddressableLoadResult<AudioClip> result = task.Result;

        // 이전 페이드가 끝나기도 전에 또 전환된 경우: 오래된 previous 즉시 해제.
        if (_hasPreviousAddressable)
        {
            _previousAddressable.Release();
            _hasPreviousAddressable = false;
        }

        // current → previous (페이드가 끝난 뒤에 해제).
        if (_hasCurrentAddressable)
        {
            _previousAddressable = _currentAddressable;
            _hasPreviousAddressable = true;
        }

        _currentAddressable = result;
        _hasCurrentAddressable = true;

        yield return CoCrossfade(result.Asset, fadeDuration);

        if (_hasPreviousAddressable)
        {
            _previousAddressable.Release();
            _hasPreviousAddressable = false;
        }

        _bgmRoutine = null;
    }

    private IEnumerator CoCrossfade(AudioClip next, float duration)
    {
        AudioSource fadeOut = _activeBgmSource;
        AudioSource fadeIn = (_activeBgmSource == _bgmSourceA) ? _bgmSourceB : _bgmSourceA;

        fadeIn.Stop();
        fadeIn.clip = next;
        fadeIn.time = 0f;
        fadeIn.volume = 0f;
        fadeIn.Play();

        if (duration <= 0f)
        {
            fadeOut.Stop();
            fadeOut.clip = null;
            fadeOut.volume = 0f;
            fadeIn.volume = EffectiveBgmVolume;
            _activeBgmSource = fadeIn;
            yield break;
        }

        _isBgmFading = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float effective = EffectiveBgmVolume;
            fadeOut.volume = effective * (1f - t);
            fadeIn.volume = effective * t;
            yield return null;
        }
        _isBgmFading = false;

        fadeOut.Stop();
        fadeOut.clip = null;
        fadeOut.volume = 0f;
        fadeIn.volume = EffectiveBgmVolume;
        _activeBgmSource = fadeIn;
    }

    private void PlayBgmImmediate(AudioClip clip)
    {
        // 외부 clip 경로: Addressables 핸들 전부 해제, 주소 가드 해제.
        ReleaseAllAddressables();
        _currentBgmAddress = null;

        if (_bgmRoutine != null)
        {
            StopCoroutine(_bgmRoutine);
            _bgmRoutine = null;
        }
        _isBgmFading = false;

        AudioSource source = _activeBgmSource != null ? _activeBgmSource : _bgmSourceA;
        AudioSource other = (source == _bgmSourceA) ? _bgmSourceB : _bgmSourceA;
        if (other != null)
        {
            other.Stop();
            other.clip = null;
            other.volume = 0f;
        }

        source.Stop();
        source.clip = clip;
        source.volume = EffectiveBgmVolume;
        source.Play();
        _activeBgmSource = source;
    }

    protected override void OnDestroy()
    {
        ReleaseAllAddressables();
        base.OnDestroy();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (_sfxSource == null)
            return;

        ApplySfxVolumeToSource();
        _sfxSource.PlayOneShot(clip);
    }

    private void ApplyVolumeSettings(AudioVolumeSettings settings)
    {
        _masterVolume = settings.Master;
        _bgmVolume = settings.Bgm;
        _sfxVolume = settings.Sfx;
    }

    private void PersistVolumeSettings()
    {
        AudioSettingsRepository.Save(new AudioVolumeSettings(_masterVolume, _bgmVolume, _sfxVolume));
    }

    private void ApplyAllVolumes()
    {
        ApplyBgmVolumeToSource();
        ApplySfxVolumeToSource();
        VolumesChanged?.Invoke();
    }

    private void ApplyBgmVolumeToSource()
    {
        // 페이드 중에는 CoCrossfade 루프가 매 프레임 재계산.
        if (_isBgmFading)
            return;

        float effective = EffectiveBgmVolume;
        if (_bgmSourceA != null)
            _bgmSourceA.volume = (_bgmSourceA == _activeBgmSource) ? effective : 0f;
        if (_bgmSourceB != null)
            _bgmSourceB.volume = (_bgmSourceB == _activeBgmSource) ? effective : 0f;
    }

    private void ApplySfxVolumeToSource()
    {
        if (_sfxSource != null)
            _sfxSource.volume = EffectiveSfxVolume;
    }

    private void ReleaseAllAddressables()
    {
        if (_hasCurrentAddressable)
        {
            _currentAddressable.Release();
            _hasCurrentAddressable = false;
        }

        if (_hasPreviousAddressable)
        {
            _previousAddressable.Release();
            _hasPreviousAddressable = false;
        }
    }

    private static float Clamp01(float value) => Mathf.Clamp01(value);

#if UNITY_EDITOR
    private void OnValidate()
    {
        _masterVolume = Clamp01(_masterVolume);
        _bgmVolume = Clamp01(_bgmVolume);
        _sfxVolume = Clamp01(_sfxVolume);
        if (Application.isPlaying && Instance == this)
            ApplyAllVolumes();
    }
#endif
}
