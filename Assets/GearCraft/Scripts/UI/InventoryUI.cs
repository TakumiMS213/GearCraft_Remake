using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main scene inventory display for materials and the current weapon.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Root")]
    public RectTransform contentRoot;
    public Sprite backgroundSprite;
    public Sprite gridFrameSprite;
    public bool preserveSceneLayout = true;

    [Header("Weapon")]
    public Image weaponIconImage;
    public TMP_Text weaponNameText;
    public TMP_Text weaponDescriptionText;
    public TMP_Text weaponStatsText;

    [Header("Materials")]
    public RectTransform itemListParent;
    public GameObject itemEntryPrefab;

    [Header("Material Icons")]
    public Sprite scrapIcon;
    public Sprite gearIcon;
    public Sprite upgradeCoreIcon;
    public Sprite moduleCoreLv1Icon;
    public Sprite moduleCoreLv2Icon;
    public Sprite moduleCoreLv3Icon;

    private readonly List<GameObject> entries = new List<GameObject>();

    private void Awake()
    {
        EnsureLayout();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        EnsureLayout();
        RefreshWeapon();
        RefreshMaterials();
    }

    private void RefreshWeapon()
    {
        StatusManager status = StatusManager.Instance;
        WeaponDataSO weapon = status != null ? status.currentWeapon : null;
        if (weapon == null)
        {
            SetText(weaponNameText, "No Weapon");
            SetText(weaponDescriptionText, "装備中の武器がありません。");
            SetText(weaponStatsText, "");
            if (weaponIconImage != null)
            {
                weaponIconImage.enabled = false;
            }
            return;
        }

        if (weaponIconImage != null)
        {
            weaponIconImage.enabled = weapon.icon != null;
            weaponIconImage.sprite = weapon.icon;
            weaponIconImage.preserveAspect = true;
        }

        SetText(weaponNameText, weapon.weaponName);
        SetText(weaponDescriptionText, GetWeaponDescription(weapon));
        SetText(weaponStatsText, BuildWeaponStatsText(weapon, status));
    }

    private void RefreshMaterials()
    {
        entries.Clear();

        if (MaterialManager.Instance == null || itemListParent == null || itemEntryPrefab == null)
        {
            return;
        }

        List<(MaterialManager.MaterialType type, int count, string name)> allMaterials = MaterialManager.Instance.GetAllMaterials();
        for (int i = 0; i < allMaterials.Count; i++)
        {
            (MaterialManager.MaterialType type, int count, string name) material = allMaterials[i];
            GameObject entry = GetOrCreateMaterialEntry(i, material.type);
            if (!entry.name.StartsWith("InventoryItemPreview_"))
            {
                entry.name = $"InventoryItem_{material.type}";
            }
            entry.SetActive(true);
            entries.Add(entry);

            InventoryItemView itemView = entry.GetComponent<InventoryItemView>();
            if (itemView == null)
            {
                itemView = entry.AddComponent<InventoryItemView>();
            }

            itemView.SetData(
                GetIcon(material.type),
                GetMaterialDisplayName(material.type),
                material.count,
                GetMaterialDescription(material.type),
                gridFrameSprite);
        }

        for (int i = allMaterials.Count; i < itemListParent.childCount; i++)
        {
            itemListParent.GetChild(i).gameObject.SetActive(false);
        }
    }

    private GameObject GetOrCreateMaterialEntry(int index, MaterialManager.MaterialType type)
    {
        if (itemListParent != null && index >= 0 && index < itemListParent.childCount)
        {
            return itemListParent.GetChild(index).gameObject;
        }

        GameObject entry = Instantiate(itemEntryPrefab, itemListParent);
        entry.name = $"InventoryItem_{type}";
        return entry;
    }

    private string BuildWeaponStatsText(WeaponDataSO weapon, StatusManager status)
    {
        float bonusDamage = status != null ? status.bonusDamage : 0f;
        float scaledBonus = weapon.GetScaledFlatDamageBonus(status != null ? status.ACC + bonusDamage : 0f);
        float damage = weapon.baseDamage + scaledBonus;
        float attackCooldown = status != null ? weapon.coolTime * status.attackSpeedMult : weapon.coolTime;
        float attackPerSecond = attackCooldown > 0f ? 1f / attackCooldown : 0f;

        return
            $"ダメージ: {FormatNumber(damage)}\n" +
            $"攻撃間隔: {attackCooldown:0.##}秒\n" +
            $"攻撃速度: {attackPerSecond:0.##}/秒\n" +
            $"射程: {weapon.attackRange:0.##}";
    }

    private static string GetWeaponDescription(WeaponDataSO weapon)
    {
        if (!string.IsNullOrWhiteSpace(weapon.description))
        {
            return weapon.description;
        }

        string typeText = weapon.weaponType == WeaponType.Melee ? "近接武器" : "射撃武器";
        if (weapon.isPiercing)
        {
            return $"{typeText}。敵を貫通して複数対象へ攻撃できます。";
        }

        return $"{typeText}。扱いやすい標準的な武器です。";
    }

    private static string FormatNumber(float value)
    {
        return Mathf.Approximately(value, Mathf.Round(value)) ? Mathf.RoundToInt(value).ToString() : value.ToString("0.##");
    }

    private Sprite GetIcon(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap: return scrapIcon;
            case MaterialManager.MaterialType.Gear: return gearIcon;
            case MaterialManager.MaterialType.UpgradeCore: return upgradeCoreIcon;
            case MaterialManager.MaterialType.ModuleCore_lv1: return moduleCoreLv1Icon != null ? moduleCoreLv1Icon : upgradeCoreIcon;
            case MaterialManager.MaterialType.ModuleCore_lv2: return moduleCoreLv2Icon != null ? moduleCoreLv2Icon : upgradeCoreIcon;
            case MaterialManager.MaterialType.ModuleCore_lv3: return moduleCoreLv3Icon != null ? moduleCoreLv3Icon : upgradeCoreIcon;
            default: return null;
        }
    }

    private static string GetMaterialDisplayName(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap: return "Scrap";
            case MaterialManager.MaterialType.Gear: return "Gear";
            case MaterialManager.MaterialType.UpgradeCore: return "Upgrade Core";
            case MaterialManager.MaterialType.ModuleCore_lv1: return "Module Core I";
            case MaterialManager.MaterialType.ModuleCore_lv2: return "Module Core II";
            case MaterialManager.MaterialType.ModuleCore_lv3: return "Module Core III";
            default: return type.ToString();
        }
    }

    private static string GetMaterialDescription(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap:
                return "武器や設備の基礎素材。多くのクラフトで消費します。";
            case MaterialManager.MaterialType.Gear:
                return "精密な歯車素材。武器製作やショップ更新に使用します。";
            case MaterialManager.MaterialType.UpgradeCore:
                return "強化パーツの生成や購入に使用する中核素材。";
            case MaterialManager.MaterialType.ModuleCore_lv1:
                return "低位モジュールの構築に使用するコア素材。";
            case MaterialManager.MaterialType.ModuleCore_lv2:
                return "中位モジュールの構築に使用するコア素材。";
            case MaterialManager.MaterialType.ModuleCore_lv3:
                return "高位モジュールの構築に使用する希少なコア素材。";
            default:
                return "";
        }
    }

    private void EnsureLayout()
    {
        RectTransform root = contentRoot != null ? contentRoot : transform as RectTransform;
        if (root == null)
        {
            return;
        }

        contentRoot = root;
        EnsureRootBackground(root);

        RectTransform weaponPanel = EnsureRectChild(root, "CurrentWeaponPanel", new Vector2(0.045f, 0.08f), new Vector2(0.29f, 0.92f), Vector2.zero, Vector2.zero, !preserveSceneLayout);
        EnsureImage(weaponPanel.gameObject, new Color(0f, 0f, 0f, 0f), null);

        RectTransform materialPanel = EnsureRectChild(root, "MaterialGridPanel", new Vector2(0.31f, 0.08f), new Vector2(0.955f, 0.92f), Vector2.zero, Vector2.zero, !preserveSceneLayout);
        EnsureImage(materialPanel.gameObject, new Color(0f, 0f, 0f, 0f), null);

        weaponIconImage = EnsureImageChild(weaponPanel, "WeaponIcon", new Vector2(0.04f, 0.6f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero, !preserveSceneLayout);
        weaponNameText = EnsureTextChild(weaponPanel, "WeaponNameText", new Vector2(0.08f, 0.49f), new Vector2(0.92f, 0.58f), 34, TextAlignmentOptions.Center, !preserveSceneLayout);
        weaponDescriptionText = EnsureTextChild(weaponPanel, "WeaponDescriptionText", new Vector2(0.08f, 0.37f), new Vector2(0.92f, 0.49f), 18, TextAlignmentOptions.TopLeft, !preserveSceneLayout);
        weaponStatsText = EnsureTextChild(weaponPanel, "WeaponStatsText", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.34f), 18, TextAlignmentOptions.TopLeft, !preserveSceneLayout);

        itemListParent = EnsureRectChild(materialPanel, "MaterialGrid", new Vector2(0.035f, 0.065f), new Vector2(0.965f, 0.935f), Vector2.zero, Vector2.zero, !preserveSceneLayout);
        EnsureGrid(itemListParent, !preserveSceneLayout);
    }

    private void EnsureRootBackground(RectTransform root)
    {
        Image image = root.GetComponent<Image>();
        if (image == null)
        {
            image = root.gameObject.AddComponent<Image>();
        }
        image.sprite = backgroundSprite;
        image.type = Image.Type.Simple;
        image.color = backgroundSprite != null ? new Color(1f, 1f, 1f, 0.96f) : new Color(0.04f, 0.035f, 0.03f, 0.94f);
        image.raycastTarget = true;
    }

    private static RectTransform EnsureRectChild(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool applyLayoutToExisting)
    {
        Transform child = parent.Find(name);
        RectTransform rect = child != null ? child as RectTransform : null;
        bool created = rect == null;
        if (rect == null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        if (created || applyLayoutToExisting)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        return rect;
    }

    private static Image EnsureImage(GameObject obj, Color color, Sprite sprite)
    {
        Image image = obj.GetComponent<Image>();
        if (image == null)
        {
            image = obj.AddComponent<Image>();
        }
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image EnsureImageChild(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool applyLayoutToExisting)
    {
        RectTransform rect = EnsureRectChild(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, applyLayoutToExisting);
        Image image = EnsureImage(rect.gameObject, Color.white, null);
        image.preserveAspect = true;
        return image;
    }

    private static TMP_Text EnsureTextChild(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float size, TextAlignmentOptions alignment, bool applyLayoutToExisting)
    {
        RectTransform rect = EnsureRectChild(parent, name, anchorMin, anchorMax, Vector2.zero, Vector2.zero, applyLayoutToExisting);
        TMP_Text text = rect.GetComponent<TMP_Text>();
        bool createdText = text == null;
        if (text == null)
        {
            text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        }

        TMP_FontAsset font = ResolveDotGothicFont();
        if (font != null)
        {
            text.font = font;
        }

        if (createdText || applyLayoutToExisting)
        {
            text.fontSize = size;
            text.alignment = alignment;
        }

        text.enableAutoSizing = false;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = new Color(1f, 0.96f, 0.82f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static void EnsureGrid(RectTransform parent, bool applyLayoutToExisting)
    {
        GridLayoutGroup grid = parent.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = parent.gameObject.AddComponent<GridLayoutGroup>();
        }
        else if (!applyLayoutToExisting)
        {
            grid.enabled = false;
            return;
        }

        grid.enabled = true;
        grid.cellSize = new Vector2(240f, 285f);
        grid.spacing = new Vector2(34f, 24f);
        grid.padding = new RectOffset(26, 26, 22, 22);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperLeft;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private static TMP_FontAsset ResolveDotGothicFont()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("DotGothic16-Regular SDF");
        if (font != null)
        {
            return font;
        }

        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            if (fonts[i] != null && fonts[i].name == "DotGothic16-Regular SDF")
            {
                return fonts[i];
            }
        }

        return null;
    }
}
