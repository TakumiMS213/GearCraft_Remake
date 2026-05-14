using UnityEngine;

public class SoundButton : MonoBehaviour
{
    [Header("Button Sound")]
    public AudioClip clickSound;       // 再生する音
    public float volume = 1f;          // 音量
    private AudioSource audioSource;

    private void Awake()
    {
        // AudioSourceを取得、なければ追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    // OnClick()から呼ぶ
    public void PlayClickSound()
    {
        if (clickSound != null)
        {
            audioSource.PlayOneShot(clickSound, volume);
        }
    }
}
