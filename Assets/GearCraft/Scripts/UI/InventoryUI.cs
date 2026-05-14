using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// インベントリパネル：所持素材を画像＋個数で表示。
/// PauseManagerから開くか、CraftScene内で常時表示。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("UI")]
    public RectTransform itemListParent;
    public GameObject itemEntryPrefab;     // 素材1行のプレハブ（Icon + Text）

    [Header("素材アイコン")]
    public Sprite scrapIcon;
    public Sprite gearIcon;
    public Sprite upgradeCoreIcon;

    private List<GameObject> entries = new List<GameObject>();

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        foreach (var e in entries) Destroy(e);
        entries.Clear();

        if (MaterialManager.Instance == null || itemListParent == null || itemEntryPrefab == null) return;

        var allMats = MaterialManager.Instance.GetAllMaterials();
        foreach (var (type, count, name) in allMats)
        {
            GameObject entry = Instantiate(itemEntryPrefab, itemListParent);
            entries.Add(entry);

            // アイコン
            Image icon = entry.GetComponentInChildren<Image>();
            if (icon != null)
                icon.sprite = GetIcon(type);

            // テキスト
            TMP_Text text = entry.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = $"{name}  ×{count}";
        }
    }

    private Sprite GetIcon(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap: return scrapIcon;
            case MaterialManager.MaterialType.Gear: return gearIcon;
            case MaterialManager.MaterialType.UpgradeCore: return upgradeCoreIcon;
            default: return null;
        }
    }
}
