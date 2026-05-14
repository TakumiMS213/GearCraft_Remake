using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(AudioSource))]
public class ButtonSoundPlayer : MonoBehaviour
{
    [Header("クリック時に再生する音")]
    public AudioClip clickSound;

    private Button button;
    private AudioSource audioSource;

    void Awake()
    {
        button = GetComponent<Button>();
        audioSource = GetComponent<AudioSource>();

        // AudioSource初期設定
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // ボタンクリック時に再生
        button.onClick.AddListener(PlayClickSound);
    }

    void PlayClickSound()
    {
        if (clickSound != null)
        {
            Debug.Log($"{name}: ボタンクリック音を再生");
            audioSource.PlayOneShot(clickSound);
        }
        else
        {
            Debug.LogWarning($"{name}: clickSound が設定されていません");
        }
    }
}
