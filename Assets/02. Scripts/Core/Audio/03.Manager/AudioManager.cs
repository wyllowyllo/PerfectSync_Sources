using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class AudioManager : SingletonMonoBehaviour<AudioManager>
{
    public static event Action VolumesChanged;

    private const string BgmChildName = "BGM";
    private const string SfxChildName = "SFX";

    private AudioSource _bgmSource;
    private AudioSource _sfxSource;

    private bool _hasAddressableBgm;
    private AddressableLoadResult<AudioClip> _addressableBgm;

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

    private void Start()
    {
        StartCoroutine(CoLoadLobbyBgmFromAddressables());
    }

    private IEnumerator CoLoadLobbyBgmFromAddressables()
    {
        Task<AddressableLoadResult<AudioClip>> task = AudioAssetRepository.LoadClipAsync(AudioBgmAddresses.Lobby);
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            Debug.LogException(task.Exception);
            yield break;
        }

        AddressableLoadResult<AudioClip> result = task.Result;
        ReleaseAddressableBgmIfLoaded();
        _addressableBgm = result;
        _hasAddressableBgm = true;
        PlayBgm(result.Asset);
    }

    private void ReleaseAddressableBgmIfLoaded()
    {
        if (!_hasAddressableBgm)
            return;

        _addressableBgm.Release();
        _hasAddressableBgm = false;
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

    private void CreateChildAudioSources()
    {
        _bgmSource = CreateChildAudioSource(BgmChildName, loop: true);
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
                ReleaseAddressableBgmIfLoaded();
                PlayBgm(clip);
                break;
            case AudioType.Sfx:
                PlaySfx(clip);
                break;
        }
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

    private void PlayBgm(AudioClip clip)
    {
        if (_bgmSource == null)
            return;

        _bgmSource.Stop();
        _bgmSource.clip = clip;
        ApplyBgmVolumeToSource();
        _bgmSource.Play();
    }

    protected override void OnDestroy()
    {
        ReleaseAddressableBgmIfLoaded();
        base.OnDestroy();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (_sfxSource == null)
            return;

        ApplySfxVolumeToSource();
        _sfxSource.PlayOneShot(clip);
    }

    private void ApplyAllVolumes()
    {
        ApplyBgmVolumeToSource();
        ApplySfxVolumeToSource();
        VolumesChanged?.Invoke();
    }

    private void ApplyBgmVolumeToSource()
    {
        if (_bgmSource != null)
            _bgmSource.volume = EffectiveBgmVolume;
    }

    private void ApplySfxVolumeToSource()
    {
        if (_sfxSource != null)
            _sfxSource.volume = EffectiveSfxVolume;
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
