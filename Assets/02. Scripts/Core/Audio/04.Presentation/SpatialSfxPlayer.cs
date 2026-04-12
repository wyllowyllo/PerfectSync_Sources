using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SpatialSfxPlayer : MonoBehaviour
{
    [SerializeField] private SpatialSfxProfile _profile;

    private AudioSource _source;
    private float _lastPlayTime;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
    }

    private void Start()
    {
        if (_profile != null)
            _profile.ConfigureSource(_source);
    }

    public void Play()
    {
        if (_profile == null)
            return;

        if (Time.time - _lastPlayTime < _profile.Cooldown)
            return;

        AudioClip clip = _profile.GetRandomClip();
        if (clip == null)
            return;

        _lastPlayTime = Time.time;
        _source.pitch = _profile.GetRandomPitch();

        float volume = GetEffectiveVolume();
        _source.PlayOneShot(clip, volume);
    }

    private float GetEffectiveVolume()
    {
        if (AudioManager.Instance == null)
            return _profile.VolumeScale;

        return AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume * _profile.VolumeScale;
    }

}
