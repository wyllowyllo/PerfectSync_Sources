using UnityEngine;

public readonly struct AudioVolumeSettings
{
    public const float VolumeMin = 0f;
    public const float VolumeMax = 1f;
    public const float VolumeDefault = 0.5f;

    public float Master { get; }
    public float Bgm { get; }
    public float Sfx { get; }

    public static AudioVolumeSettings Default =>
        new AudioVolumeSettings(VolumeDefault, VolumeDefault, VolumeDefault);

    public AudioVolumeSettings(float master, float bgm, float sfx)
    {
        Master = Mathf.Clamp(master, VolumeMin, VolumeMax);
        Bgm = Mathf.Clamp(bgm, VolumeMin, VolumeMax);
        Sfx = Mathf.Clamp(sfx, VolumeMin, VolumeMax);
    }
}
