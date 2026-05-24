using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipSystem : MonoBehaviour
{
    private const string PositiveEffectColor = "#80FF80";
    private const string NegativeEffectColor = "#FF7070";

    public static TooltipSystem Instance { get; private set; }

    [Header("UI")]
    public RectTransform tooltipPanel;
    public TMP_Text tooltipText;
    public TMP_FontAsset tooltipFont;
    public TMP_Text titleText;
    public RectTransform costListParent;
    public float titleFontSize = 32f;
    public float bodyFontSize = 28f;
    public float costFontSize = 26f;
    public float costIconSize = 36f;

    [Header("Material Icons")]
    public Sprite scrapIcon;
    public Sprite gearIcon;
    public Sprite upgradeCoreIcon;
    public Sprite moduleCoreLv1Icon;
    public Sprite moduleCoreLv2Icon;
    public Sprite moduleCoreLv3Icon;

    [Header("Position")]
    public Vector2 offset = new Vector2(28f, -12f);
    public Vector2 panelSize = new Vector2(620f, 380f);
    public Vector2 minimumPanelSize = new Vector2(620f, 380f);
    public float edgePadding = 18f;

    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform currentAnchor;

    public static TooltipSystem FindInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        TooltipSystem[] systems = Resources.FindObjectsOfTypeAll<TooltipSystem>();
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null && systems[i].gameObject.scene.IsValid())
            {
                Instance = systems[i];
                Instance.ResolveCanvas();
                return Instance;
            }
        }

        return null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveCanvas();
        EnsureReferences();
        Hide();
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        ResolveCanvas();
    }

    private void Update()
    {
        ResolveCanvas();

        if (tooltipPanel == null || !tooltipPanel.gameObject.activeSelf)
        {
            return;
        }

        RectTransform root = canvasRect != null ? canvasRect : tooltipPanel.parent as RectTransform;
        if (root == null)
        {
            return;
        }

        if (currentAnchor != null && currentAnchor.gameObject.activeInHierarchy)
        {
            tooltipPanel.anchoredPosition = ClampToCanvas(GetAnchorPosition(currentAnchor));
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            root,
            Input.mousePosition,
            canvas != null ? canvas.worldCamera : null,
            out Vector2 localPosition);
        tooltipPanel.anchoredPosition = ClampToCanvas(ToTopLeftAnchoredPosition(localPosition) + offset);
    }

    public void Show(string text)
    {
        Show(text, null);
    }

    public void Show(string text, RectTransform anchor)
    {
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        EnsureReferences();
        ResolveCanvas();
        if (tooltipPanel == null || tooltipText == null)
        {
            return;
        }

        tooltipText.text = text;
        tooltipPanel.sizeDelta = ResolvePanelSize();
        SetStructuredElementsActive(false);
        currentAnchor = anchor;
        Transform targetParent = canvas != null ? canvas.transform : transform;
        if (tooltipPanel.parent != targetParent)
        {
            tooltipPanel.SetParent(targetParent, false);
        }

        SetLayerRecursively(tooltipPanel.gameObject, targetParent.gameObject.layer);
        tooltipPanel.anchoredPosition = currentAnchor != null
            ? ClampToCanvas(GetAnchorPosition(currentAnchor))
            : tooltipPanel.anchoredPosition;
        tooltipPanel.gameObject.SetActive(true);
        tooltipPanel.SetAsLastSibling();
    }

    public void ShowPart(UpgradePartSO part, RectTransform anchor)
    {
        if (part == null)
        {
            Hide();
            return;
        }

        EnsureReferences();
        ResolveCanvas();
        if (tooltipPanel == null || tooltipText == null || titleText == null)
        {
            return;
        }

        tooltipPanel.sizeDelta = ResolvePanelSize();
        SetStructuredElementsActive(true);
        titleText.text = $"【{(string.IsNullOrEmpty(part.partName) ? "強化パーツ" : part.partName)}】";
        titleText.color = PartRarityColors.Get(part.rarity);
        tooltipText.text = BuildPartBodyText(part);
        RebuildCostList(part);

        currentAnchor = anchor;
        Transform targetParent = canvas != null ? canvas.transform : transform;
        if (tooltipPanel.parent != targetParent)
        {
            tooltipPanel.SetParent(targetParent, false);
        }

        SetLayerRecursively(tooltipPanel.gameObject, targetParent.gameObject.layer);
        tooltipPanel.anchoredPosition = currentAnchor != null
            ? ClampToCanvas(GetAnchorPosition(currentAnchor))
            : tooltipPanel.anchoredPosition;
        tooltipPanel.gameObject.SetActive(true);
        tooltipPanel.SetAsLastSibling();
    }

    public void Hide()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.gameObject.SetActive(false);
        }

        currentAnchor = null;
    }

    private void EnsureReferences()
    {
        ResolveCanvas();

        if (tooltipPanel == null)
        {
            tooltipPanel = CreatePanel();
        }

        if (tooltipText == null && tooltipPanel != null)
        {
            tooltipText = CreateText(tooltipPanel, tooltipFont);
        }

        if (titleText == null && tooltipPanel != null)
        {
            titleText = CreateTitleText(tooltipPanel, tooltipFont);
        }

        if (costListParent == null && tooltipPanel != null)
        {
            costListParent = CreateCostList(tooltipPanel);
        }

        if (tooltipText != null && tooltipFont != null)
        {
            tooltipText.font = tooltipFont;
        }

        if (titleText != null && tooltipFont != null)
        {
            titleText.font = tooltipFont;
        }

        ApplyTextSizes();
        ApplyStructuredLayout();
    }

    private RectTransform CreatePanel()
    {
        GameObject panelObject = new GameObject("TooltipPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Transform parent = canvas != null ? canvas.transform : transform;
        panelObject.transform.SetParent(parent, false);
        panelObject.layer = parent.gameObject.layer;

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.sizeDelta = ResolvePanelSize();

        Image image = panelObject.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.05f, 0.045f, 0.035f, 0.96f);
            image.raycastTarget = false;
        }

        panelObject.SetActive(false);
        return rect;
    }

    private static TMP_Text CreateText(RectTransform parent, TMP_FontAsset fontAsset)
    {
        GameObject textObject = new GameObject("TooltipText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(18f, 16f);
        rect.offsetMax = new Vector2(-18f, -16f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        text.fontSize = 28f;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;

        return text;
    }

    private static TMP_Text CreateTitleText(RectTransform parent, TMP_FontAsset fontAsset)
    {
        GameObject textObject = new GameObject("TooltipTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(18f, -46f);
        rect.offsetMax = new Vector2(-18f, -12f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        text.fontSize = 32f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;
        return text;
    }

    private static RectTransform CreateCostList(RectTransform parent)
    {
        GameObject listObject = new GameObject("TooltipCostList", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        listObject.transform.SetParent(parent, false);
        listObject.layer = parent.gameObject.layer;

        RectTransform rect = listObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(18f, 18f);
        rect.offsetMax = new Vector2(-18f, 68f);

        HorizontalLayoutGroup layout = listObject.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 18f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        return rect;
    }

    private void SetStructuredElementsActive(bool active)
    {
        ApplyStructuredLayout();

        if (titleText != null)
        {
            titleText.gameObject.SetActive(active);
        }

        if (costListParent != null)
        {
            costListParent.gameObject.SetActive(active);
        }

        if (tooltipText != null)
        {
            RectTransform rect = tooltipText.GetComponent<RectTransform>();
            if (active)
            {
                rect.offsetMin = new Vector2(18f, 86f);
                rect.offsetMax = new Vector2(-18f, -84f);
            }
            else
            {
                rect.offsetMin = new Vector2(18f, 16f);
                rect.offsetMax = new Vector2(-18f, -16f);
            }
        }
    }

    private void RebuildCostList(UpgradePartSO part)
    {
        if (costListParent == null)
        {
            return;
        }

        for (int i = costListParent.childCount - 1; i >= 0; i--)
        {
            DestroyObject(costListParent.GetChild(i).gameObject);
        }

        if (part.costs == null || part.costs.Length == 0)
        {
            CreateCostText("無料");
            return;
        }

        for (int i = 0; i < part.costs.Length; i++)
        {
            CraftCost cost = part.costs[i];
            if (cost == null)
            {
                continue;
            }

            CreateCostEntry(GetMaterialIcon(cost.type), $"×{cost.amount}");
        }
    }

    private void CreateCostEntry(Sprite iconSprite, string amountText)
    {
        GameObject entry = new GameObject("CostEntry", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        entry.transform.SetParent(costListParent, false);
        entry.layer = costListParent.gameObject.layer;

        HorizontalLayoutGroup layout = entry.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 4f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(entry.transform, false);
        iconObject.layer = entry.layer;
        LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
        float iconSize = Mathf.Max(1f, costIconSize);
        iconLayout.preferredWidth = iconSize;
        iconLayout.preferredHeight = iconSize;
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = iconSprite;
        icon.enabled = iconSprite != null;
        icon.raycastTarget = false;

        TMP_Text amount = CreateCostText(amountText);
        amount.transform.SetParent(entry.transform, false);
        amount.gameObject.layer = entry.layer;
    }

    private TMP_Text CreateCostText(string textValue)
    {
        GameObject textObject = new GameObject("CostText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(costListParent, false);
        textObject.layer = costListParent.gameObject.layer;
        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 68f;
        layout.preferredHeight = Mathf.Max(1f, costIconSize);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (tooltipFont != null)
        {
            text.font = tooltipFont;
        }

        text.text = textValue;
        text.fontSize = Mathf.Max(1f, costFontSize);
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    private static void DestroyObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void ApplyTextSizes()
    {
        if (titleText != null)
        {
            titleText.fontSize = Mathf.Max(1f, titleFontSize);
        }

        if (tooltipText != null)
        {
            tooltipText.fontSize = Mathf.Max(1f, bodyFontSize);
            tooltipText.textWrappingMode = TextWrappingModes.Normal;
            tooltipText.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private void ApplyStructuredLayout()
    {
        if (titleText != null)
        {
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            if (titleRect != null)
            {
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.offsetMin = new Vector2(18f, -58f);
                titleRect.offsetMax = new Vector2(-18f, -12f);
            }
        }

        if (costListParent != null)
        {
            costListParent.anchorMin = new Vector2(0f, 0f);
            costListParent.anchorMax = new Vector2(1f, 0f);
            costListParent.pivot = new Vector2(0.5f, 0f);
            costListParent.offsetMin = new Vector2(18f, 18f);
            costListParent.offsetMax = new Vector2(-18f, 78f);
        }
    }

    private Vector2 ResolvePanelSize()
    {
        return new Vector2(
            Mathf.Max(panelSize.x, minimumPanelSize.x),
            Mathf.Max(panelSize.y, minimumPanelSize.y));
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }

    private void ResolveCanvas()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvasRect == null && canvas != null)
        {
            canvasRect = canvas.GetComponent<RectTransform>();
        }
    }

    private Vector2 ClampToCanvas(Vector2 position)
    {
        if (canvasRect == null || tooltipPanel == null)
        {
            return position;
        }

        Rect rect = canvasRect.rect;
        Vector2 size = tooltipPanel.sizeDelta;
        float minX = edgePadding;
        float maxX = rect.width - size.x - edgePadding;
        float minY = -rect.height + size.y + edgePadding;
        float maxY = -edgePadding;
        if (maxX < minX)
        {
            maxX = minX;
        }

        if (maxY < minY)
        {
            minY = maxY;
        }

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
        return position;
    }

    private Vector2 GetAnchorPosition(RectTransform anchor)
    {
        if (anchor == null || canvasRect == null)
        {
            return Vector2.zero;
        }

        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 topRight = corners[2];

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, topRight),
            canvas != null ? canvas.worldCamera : null,
            out Vector2 localPosition);

        return ToTopLeftAnchoredPosition(localPosition) + offset;
    }

    private Vector2 ToTopLeftAnchoredPosition(Vector2 canvasLocalPosition)
    {
        if (canvasRect == null)
        {
            return canvasLocalPosition;
        }

        Rect rect = canvasRect.rect;
        return new Vector2(
            canvasLocalPosition.x - rect.xMin,
            canvasLocalPosition.y - rect.yMax);
    }

    private string BuildPartBodyText(UpgradePartSO part)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"レアリティ: {FormatRarity(part.rarity)}");

        if (part.rangedOnly)
        {
            builder.AppendLine("制限: 遠距離武器のみ");
        }

        builder.AppendLine("効果:");
        if (part.effects == null || part.effects.Length == 0)
        {
            builder.AppendLine("- なし");
        }
        else
        {
            for (int i = 0; i < part.effects.Length; i++)
            {
                UpgradeEffect effect = part.effects[i];
                if (effect != null)
                {
                    builder.AppendLine(UpgradeEffectTextFormatter.FormatEffectLine(effect, PositiveEffectColor, NegativeEffectColor));
                }
            }
        }

        return builder.ToString();
    }

    private Sprite GetMaterialIcon(MaterialManager.MaterialType type)
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

    private static string FormatRarity(PartRarity rarity)
    {
        switch (rarity)
        {
            case PartRarity.Common:
                return "コモン";
            case PartRarity.Uncommon:
                return "アンコモン";
            case PartRarity.Rare:
                return "レア";
            case PartRarity.Epic:
                return "エピック";
            default:
                return rarity.ToString();
        }
    }

}
