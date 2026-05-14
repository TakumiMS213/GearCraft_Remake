using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UISwitcher : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject panel2;
    public AudioSource ClickSound;

    // 表示ボタンに割り当てる
    public void ShowPanel()
    {
        if (panel != null)
        {
            panel.SetActive(true);
            ClickSound.Play();
        }
    }
    public void ShowPanel2()
    {
        if (panel2 != null)
        {
            panel2.SetActive(true);
            ClickSound.Play();
        }
    }

    // 非表示ボタンに割り当てる
    public void HidePanel()
    {
        if (panel != null)
        {
            panel.SetActive(false);
            ClickSound.Play();
        }
    }
    public void HidePanel2()
    {
        if (panel2 != null)
        {
            panel2.SetActive(false);
            ClickSound.Play();
        }
    }
}
