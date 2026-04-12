using UnityEngine;

[CreateAssetMenu(fileName = "NewSfxProfile", menuName = "Audio/SFX Profile")]
public class SfxProfile : ScriptableObject
{
    [SerializeField] private AudioClip[] _clips;
    [SerializeField, Range(0f, 2f)] private float _volumeScale = 1f;
    [SerializeField, Range(0.5f, 2f)] private float _pitchMin = 1f;
    [SerializeField, Range(0.5f, 2f)] private float _pitchMax = 1f;
    [SerializeField, Range(0f, 1f)] private float _cooldown = 0.05f;

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
}
