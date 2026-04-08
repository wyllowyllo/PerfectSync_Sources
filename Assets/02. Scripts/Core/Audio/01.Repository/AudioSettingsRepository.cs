using UnityEngine;

public static class AudioSettingsRepository
{
    private const string KeyMaster = "Audio.MasterVolume";
    private const string KeyBgm = "Audio.BgmVolume";
    private const string KeySfx = "Audio.SfxVolume";

    public static AudioVolumeSettings Load()
    {
        return new AudioVolumeSettings(
            PlayerPrefs.GetFloat(KeyMaster, AudioVolumeSettings.VolumeDefault),
            PlayerPrefs.GetFloat(KeyBgm, AudioVolumeSettings.VolumeDefault),
            PlayerPrefs.GetFloat(KeySfx, AudioVolumeSettings.VolumeDefault));
    }

    public static void Save(in AudioVolumeSettings settings)
    {
        PlayerPrefs.SetFloat(KeyMaster, settings.Master);
        PlayerPrefs.SetFloat(KeyBgm, settings.Bgm);
        PlayerPrefs.SetFloat(KeySfx, settings.Sfx);
        PlayerPrefs.Save();
    }
}
