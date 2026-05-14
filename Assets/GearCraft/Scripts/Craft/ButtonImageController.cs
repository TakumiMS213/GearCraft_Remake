using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ButtonImageController : MonoBehaviour
{
    [System.Serializable]
    public class ActionSet
    {
        public Button button;               // 対象のボタン
        public List<Image> imagesToShow;    // 表示したいImage
        public List<Image> imagesToHide;    // 非表示にしたいImage
        public List<TMP_Text> textToShow;
        public List<TMP_Text> textToHide;

    }

    public List<ActionSet> actionSets;

    void Start()
    {
        // ボタンに対応する処理をセットする
        foreach (var set in actionSets)
        {
            if (set.button != null)
            {
                set.button.onClick.AddListener(() => HandleButtonClick(set));
            }
        }
    }

    void HandleButtonClick(ActionSet set)
    {
        // 表示するImageの処理
        foreach (var image in set.imagesToShow)
        {
            if (image != null)
                image.gameObject.SetActive(true);
        }
        foreach(var text in set.textToShow)
        {
            if (text != null)
                text.gameObject.SetActive(true);
        }

        // 非表示にするImageの処理
        foreach (var image in set.imagesToHide)
        {
            if (image != null)
                image.gameObject.SetActive(false);
        }
        foreach(var text in set.textToHide)
        {
            if (text != null)
                text.gameObject.SetActive(false);
        }
    }
}
