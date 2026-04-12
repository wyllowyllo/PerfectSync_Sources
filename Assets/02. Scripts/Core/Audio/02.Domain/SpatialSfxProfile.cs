using UnityEngine;

[CreateAssetMenu(fileName = "NewSpatialSfxProfile", menuName = "Audio/Spatial SFX Profile")]
public class SpatialSfxProfile : ScriptableObject
{
    [Header("Clip Settings")]
    [SerializeField] private AudioClip[] _clips;
    [SerializeField, Range(0f, 2f)] private float _volumeScale = 1f;
    [SerializeField, Range(0.5f, 2f)] private float _pitchMin = 1f;
    [SerializeField, Range(0.5f, 2f)] private float _pitchMax = 1f;
    [SerializeField, Range(0f, 1f)] private float _cooldown = 0.05f;

    [Header("Spatial Settings")]
    [SerializeField] private float _minDistance = 1f;
    [SerializeField] private float _maxDistance = 30f;
    [SerializeField] private AudioRolloffMode _rolloffMode = AudioRolloffMode.Linear;

    public float VolumeScale => _volumeScale;
    public float Cooldown => _cooldown;

    public AudioClip GetRandomClip()
    {
        if (_clips == null || _clips.Length == 0)
            return null;

        return _clips[Random.Range(0, _clips.Length)];
    }

    public float GetRandomPitch()
    {
        return Random.Range(_pitchMin, _pitchMax);
    }

    public void ConfigureSource(AudioSource source)
    {
        source.spatialBlend = 1f;
        source.minDistance = _minDistance;
        source.maxDistance = _maxDistance;
        source.rolloffMode = _rolloffMode;
        source.spread = 0f;
        source.dopplerLevel = 0f;
    }
}
