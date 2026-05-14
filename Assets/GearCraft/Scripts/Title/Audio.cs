using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine;

public class Audio : MonoBehaviour
{
    //Audioミキサーを入れるとこです
    public AudioMixer audioMixer;

    //それぞれのスライダーを入れるとこです。。
    public Slider BGMSlider;
    public Slider SESlider;

    private void Start()
    {
        //ミキサーのvolumeにスライダーのvolumeを入れてます。

        //BGM
        audioMixer.GetFloat("BGM", out float bgmVolume);
        BGMSlider.value = bgmVolume;
        //SE
        audioMixer.GetFloat("SE", out float seVolume);
        SESlider.value = seVolume;
    }
    
    public void SetBGM(float volume)
    {
        audioMixer.SetFloat("BGM", volume);
        Debug.Log("bgm");
    }

    public void SetSE(float volume)
    {
        audioMixer.SetFloat("SE", volume);
        Debug.Log("se");
    }
}