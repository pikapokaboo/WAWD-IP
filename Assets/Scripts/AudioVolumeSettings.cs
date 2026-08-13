// -----------------------------------------------------------------------------
// File: AudioVolumeSettings.cs
// Project: WAWD Integrated Studio Project
// Purpose: Stores and applies persistent audio volume settings.
// -----------------------------------------------------------------------------

using UnityEngine;

public static class AudioVolumeSettings
{
    private const string MasterKey = "Audio.Master";
    private const string BgmKey = "Audio.Bgm";
    private const string EffectsKey = "Audio.Effects";

    public static float Master => PlayerPrefs.GetFloat(MasterKey, 1f);
    public static float Bgm => PlayerPrefs.GetFloat(BgmKey, 1f);
    public static float SoundEffects => PlayerPrefs.GetFloat(EffectsKey, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedMasterVolume() => AudioListener.volume = Master;

    public static void SetMaster(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterKey, value);
        AudioListener.volume = value;
        PlayerPrefs.Save();
    }

    public static void SetBgm(float value)
    {
        PlayerPrefs.SetFloat(BgmKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }

    public static void SetSoundEffects(float value)
    {
        PlayerPrefs.SetFloat(EffectsKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }
}
