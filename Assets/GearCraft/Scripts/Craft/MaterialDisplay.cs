using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 素材表示（動的に全素材を表示）＋ 選択中レシピのコスト表示
/// </summary>
public class MaterialDisplay : MonoBehaviour
{
    [Header("動的表示")]
    public RectTransform materialListParent;     // 素材一覧の親
    public GameObject materialEntryPrefab;       // 素材1行分のプレハブ（Image + Text）

    [Header("素材アイコン")]
    public Sprite scrapIcon;
    public Sprite gearIcon;
    public Sprite upgradeCoreIcon;
    public Sprite moduleCoreLv1Icon;
    public Sprite moduleCoreLv2Icon;
    public Sprite moduleCoreLv3Icon;

    [Header("コスト表示（現在数/必要数）")]
    [Tooltip("左側のコスト表示TMP")]
    public TMP_Text costTextLeft;
    [Tooltip("右側のコスト表示TMP")]
    public TMP_Text costTextRight;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_FontAsset dotGothicFont;

    [Header("色設定")]
    public Color enoughColor = Color.white;
    public Color notEnoughColor = new Color(1f, 0.3f, 0.3f, 1f); // 赤

    private List<GameObject> dynamicEntries = new List<GameObject>();
    private CraftRecipeSO currentRecipe;

    void Start()
    {
        UpdateMaterialAmount();
    }

    /// <summary>
    /// 選択中のレシピを設定する（CraftManagerから呼ぶ）
    /// </summary>
    public void SetCurrentRecipe(CraftRecipeSO recipe)
    {
        currentRecipe = recipe;
        UpdateMaterialAmount();
    }

    public void UpdateMaterialAmount()
    {
        // コスト表示を更新
        UpdateCostDisplay();

        // 動的リスト更新
        if (materialListParent != null && materialEntryPrefab != null)
        {
            // 既存エントリをクリア
            foreach (var entry in dynamicEntries)
                DestroyEntry(entry);
            dynamicEntries.Clear();

            var allMats = GetMaterialsForDisplay();
            foreach (var (type, count, name) in allMats)
            {
                GameObject entry = Instantiate(materialEntryPrefab, materialListParent);
                dynamicEntries.Add(entry);

                TMP_Text text = entry.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = $"{name} x{count}";

                Image icon = entry.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null)
                {
                    icon.sprite = GetIcon(type);
                    icon.enabled = icon.sprite != null;
                }
            }
        }
    }

    private void DestroyEntry(GameObject entry)
    {
        if (entry == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(entry);
        }
        else
        {
            DestroyImmediate(entry);
        }
    }

    private Sprite GetIcon(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap:
                return scrapIcon;
            case MaterialManager.MaterialType.Gear:
                return gearIcon;
            case MaterialManager.MaterialType.UpgradeCore:
                return upgradeCoreIcon;
            case MaterialManager.MaterialType.ModuleCore_lv1:
                return moduleCoreLv1Icon;
            case MaterialManager.MaterialType.ModuleCore_lv2:
                return moduleCoreLv2Icon;
            case MaterialManager.MaterialType.ModuleCore_lv3:
                return moduleCoreLv3Icon;
            default:
                return null;
        }
    }

    private List<(MaterialManager.MaterialType type, int count, string name)> GetMaterialsForDisplay()
    {
        if (MaterialManager.Instance != null)
        {
            return MaterialManager.Instance.GetAllMaterials();
        }

        return new List<(MaterialManager.MaterialType, int, string)>
        {
            (MaterialManager.MaterialType.Scrap, 0, "Scrap"),
            (MaterialManager.MaterialType.Gear, 0, "Gear"),
            (MaterialManager.MaterialType.UpgradeCore, 0, "UpCore"),
            (MaterialManager.MaterialType.ModuleCore_lv1, 0, "ModC1"),
            (MaterialManager.MaterialType.ModuleCore_lv2, 0, "ModC2"),
            (MaterialManager.MaterialType.ModuleCore_lv3, 0, "ModC3"),
        };
    }

    /// <summary>
    /// 左右のコスト表示TMPを「現在数/必要数」形式で更新
    /// </summary>
private void UpdateCostDisplay()
    {
        HideLegacyCostTexts();

        if (costText != null)
        {
            costText.text = "";
            costText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 個別のコストテキストを設定
    /// </summary>
private void EnsureCostText()
    {
        if (costText != null)
        {
            return;
        }

        ResolveFont();

        GameObject textObject = new GameObject("RecipeCostText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(16f, -40f);
        rect.offsetMax = new Vector2(-16f, -4f);

        costText = textObject.GetComponent<TMP_Text>();
        costText.font = dotGothicFont != null ? dotGothicFont : costText.font;
        costText.fontSize = 26f;
        costText.alignment = TextAlignmentOptions.Center;
        costText.richText = true;
        costText.enableWordWrapping = false;
    }

    private void HideLegacyCostTexts()
    {
        if (costTextLeft != null)
        {
            costTextLeft.gameObject.SetActive(false);
        }

        if (costTextRight != null)
        {
            costTextRight.gameObject.SetActive(false);
        }
    }

    private void ResolveFont()
    {
        if (dotGothicFont != null)
        {
            return;
        }

        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_FontAsset font = texts[i] != null ? texts[i].font : null;
            if (font != null && font.name.Contains("DotGothic"))
            {
                dotGothicFont = font;
                return;
            }
        }

        dotGothicFont = Resources.Load<TMP_FontAsset>("DotGothic16-Regular SDF");
    }

    private string Colorize(string text, bool enough)
    {
        Color color = enough ? enoughColor : notEnoughColor;
        return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
    }

    private static string FormatCount(int current, int required)
    {
        return $"{current}/{required}";
    }

    private static string FormatMaterialName(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap: return "Scrap";
            case MaterialManager.MaterialType.Gear: return "Gear";
            case MaterialManager.MaterialType.UpgradeCore: return "UpCore";
            case MaterialManager.MaterialType.ModuleCore_lv1: return "ModC1";
            case MaterialManager.MaterialType.ModuleCore_lv2: return "ModC2";
            case MaterialManager.MaterialType.ModuleCore_lv3: return "ModC3";
            default: return type.ToString();
        }
    }
}
