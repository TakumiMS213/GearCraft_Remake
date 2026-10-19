using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine;

public class Audio : MonoBehaviour
{
    private const string BgmVolumeKey = "GearCraft.Audio.BGM.dB.v1";
    private const string SeVolumeKey = "GearCraft.Audio.SE.dB.v1";
    private const float MinVolume = -40f;
    private const float MaxVolume = 0f;
    private const float SaveDelay = 0.3f;

    public AudioMixer audioMixer;
    public Slider BGMSlider;
    public Slider SESlider;

    private bool hasPendingChanges;
    private float lastVolumeChangeTime;

    private void Start()
    {
        RestoreVolume("BGM", BgmVolumeKey, BGMSlider);
        RestoreVolume("SE", SeVolumeKey, SESlider);
    }

    private void Update()
    {
        // The settings menu can pause gameplay; saving must still advance.
        if (hasPendingChanges && Time.unscaledTime - lastVolumeChangeTime >= SaveDelay)
            SavePendingChanges();
    }

    public void SetBGM(float volume)
    {
        SetVolume("BGM", BgmVolumeKey, volume);
    }

    public void SetSE(float volume)
    {
        SetVolume("SE", SeVolumeKey, volume);
    }

    private void RestoreVolume(string parameter, string key, Slider slider)
    {
        if (audioMixer == null || !audioMixer.GetFloat(parameter, out float volume)) return;

        if (PlayerPrefs.HasKey(key))
        {
            float savedVolume = PlayerPrefs.GetFloat(key, volume);
            if (!float.IsNaN(savedVolume) && !float.IsInfinity(savedVolume) &&
                audioMixer.SetFloat(parameter, Mathf.Clamp(savedVolume, MinVolume, MaxVolume)))
                audioMixer.GetFloat(parameter, out volume);
        }

        // Reading settings does not create a save or invoke the slider callbacks.
        if (slider != null) slider.SetValueWithoutNotify(volume);
    }

    private void SetVolume(string parameter, string key, float volume)
    {
        if (audioMixer == null || float.IsNaN(volume) || float.IsInfinity(volume)) return;

        volume = Mathf.Clamp(volume, MinVolume, MaxVolume);
        if (!audioMixer.SetFloat(parameter, volume)) return;

        PlayerPrefs.SetFloat(key, volume);
        hasPendingChanges = true;
        lastVolumeChangeTime = Time.unscaledTime;
    }

    private void SavePendingChanges()
    {
        if (!hasPendingChanges) return;
        PlayerPrefs.Save();
        hasPendingChanges = false;
    }

    private void OnDisable() => SavePendingChanges();

    private void OnApplicationPause(bool paused)
    {
        if (paused) SavePendingChanges();
    }

    private void OnApplicationQuit() => SavePendingChanges();
}
