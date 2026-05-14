using UnityEngine;
using System.Collections.Generic;

public class AllSpritesToBlack : MonoBehaviour
{
    [Header("黒くしないオブジェクトのリスト")]
    [SerializeField] private List<GameObject> excludeObjects = new List<GameObject>();
    public GameObject BlackImage;

    /// <summary>
    /// シーン内の全スプライトを即座に黒にする（除外リストを考慮）
    /// </summary>
    public void SetAllToBlack()
    {
        // シーン内の全てのSpriteRendererを取得
        SpriteRenderer[] sprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

    foreach (var sr in sprites)
        {
            if (sr == null) continue;

            bool excluded = false;

            // 除外リスト内に含まれている、またはその子孫の場合はスキップ
            foreach (var obj in excludeObjects)
            {
                if (obj != null && sr.transform.IsChildOf(obj.transform))
                {
                    excluded = true;
                    break;
                }
            }

            if (excluded) continue;

            // 即座に黒くする
            sr.color = Color.black;
        }
        // BlackImage を一度だけ表示
        if (BlackImage != null)
        {
            BlackImage.SetActive(true);
        }

        Debug.Log("🖤 全スプライトを黒に変更（除外リスト適用済）");
    }
}
