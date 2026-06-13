using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GearCraft.Scripts.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RestResultReportUI : MonoBehaviour
{
    private const float PaperWidth = 1500f;
    private const float PaperHeight = 860f;

    [Header("Scene UI")]
    public Canvas canvas;
    public RectTransform paper;
    public CanvasGroup canvasGroup;
    public RectTransform rowsParent;
    public TMP_Text clickText;

    [Header("Icons")]
    public Sprite scrapIcon;
    public Sprite gearIcon;
    public Sprite upgradeCoreIcon;
    public Sprite moduleCoreLv1Icon;
    public Sprite moduleCoreLv2Icon;
    public Sprite moduleCoreLv3Icon;

    [Header("Typography")]
    public TMP_FontAsset fontAsset;

    private Tween clickBlinkTween;
    private bool initialized;
    private Vector2 paperCenterPosition;

    public async UniTask ShowAsync(int defeatedEnemies, IReadOnlyDictionary<MaterialManager.MaterialType, int> materialGains, CancellationToken token)
    {
        EnsureInitialized();
        BuildRows(defeatedEnemies, materialGains);

        canvas.gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        paper.anchoredPosition = paperCenterPosition + new Vector2(0f, PaperHeight + 220f);
        clickText.alpha = 0f;

        paper.DOAnchorPos(paperCenterPosition, 0.65f).SetEase(Ease.OutBack);
        await UniTask.Delay(TimeSpan.FromSeconds(0.65f), cancellationToken: token);

        await UniTask.Delay(TimeSpan.FromSeconds(0.15f), cancellationToken: token);
        StartClickTextBlink();

        await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0) || Input.touchCount > 0, cancellationToken: token);
        StopClickTextBlink();
        canvasGroup.DOFade(0f, 0.25f);
        await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: token);

        canvas.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        StopClickTextBlink();
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        ResolveAssets();
        EnsureCanvas();
        EnsurePaper();
        paperCenterPosition = paper != null ? paper.anchoredPosition : Vector2.zero;
        initialized = true;
        canvas.gameObject.SetActive(false);
    }

    private void ResolveAssets()
    {
        if (fontAsset == null)
        {
            TMP_Text existingText = FindFirstObjectByType<TMP_Text>();
            if (existingText != null)
            {
                fontAsset = existingText.font;
            }
        }

        MaterialDropper dropper = MaterialDropper.Instance != null ? MaterialDropper.Instance : FindFirstObjectByType<MaterialDropper>();
        if (dropper != null)
        {
            scrapIcon = ResolveSprite(scrapIcon, dropper.ScrapDropPrefab);
            gearIcon = ResolveSprite(gearIcon, dropper.GearDropPrefab);
            upgradeCoreIcon = ResolveSprite(upgradeCoreIcon, dropper.UpgradeCoreDropPrefab);
            moduleCoreLv1Icon = ResolveSprite(moduleCoreLv1Icon, dropper.ModuleCoreLv1DropPrefab);
            moduleCoreLv2Icon = ResolveSprite(moduleCoreLv2Icon, dropper.ModuleCoreLv2DropPrefab);
            moduleCoreLv3Icon = ResolveSprite(moduleCoreLv3Icon, dropper.ModuleCoreLv3DropPrefab);
        }
    }

    private static Sprite ResolveSprite(Sprite current, GameObject prefab)
    {
        if (current != null || prefab == null)
        {
            return current;
        }

        SpriteRenderer spriteRenderer = prefab.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            return spriteRenderer.sprite;
        }

        Image image = prefab.GetComponentInChildren<Image>();
        return image != null ? image.sprite : null;
    }

    private void EnsureCanvas()
    {
        if (canvas != null)
        {
            if (canvasGroup == null)
            {
                canvasGroup = canvas.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                }
            }

            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 300);
            return;
        }

        GameObject canvasObject = new GameObject("RestResultReportCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
    }

    private void EnsurePaper()
    {
        if (paper != null)
        {
            if (rowsParent == null)
            {
                Transform rows = paper.Find("Rows");
                if (rows != null)
                {
                    rowsParent = rows as RectTransform;
                }
            }

            if (clickText == null)
            {
                Transform click = paper.Find("ClickText");
                if (click != null)
                {
                    clickText = click.GetComponent<TMP_Text>();
                }
            }

            ApplyFontToChildren(paper);
            return;
        }

        GameObject paperObject = new GameObject("ResultPaper", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        paperObject.transform.SetParent(canvas.transform, false);
        paper = paperObject.GetComponent<RectTransform>();
        paper.anchorMin = new Vector2(0.5f, 0.5f);
        paper.anchorMax = new Vector2(0.5f, 0.5f);
        paper.pivot = new Vector2(0.5f, 0.5f);
        paper.sizeDelta = new Vector2(PaperWidth, PaperHeight);

        Image paperImage = paperObject.GetComponent<Image>();
        paperImage.color = new Color(0.92f, 0.86f, 0.68f, 0.98f);
        paperImage.raycastTarget = true;

        TMP_Text title = CreateText("Title", paper, "今回の成果", 112f, new Color(0.16f, 0.11f, 0.06f, 1f), TextAlignmentOptions.Center);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(72f, -188f);
        titleRect.offsetMax = new Vector2(-72f, -32f);

        GameObject rowsObject = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
        rowsObject.transform.SetParent(paper, false);
        rowsParent = rowsObject.GetComponent<RectTransform>();
        rowsParent.anchorMin = new Vector2(0.5f, 0.5f);
        rowsParent.anchorMax = new Vector2(0.5f, 0.5f);
        rowsParent.pivot = new Vector2(0.5f, 0.5f);
        rowsParent.anchoredPosition = new Vector2(0f, -34f);
        rowsParent.sizeDelta = new Vector2(PaperWidth - 300f, 560f);

        VerticalLayoutGroup rowsLayout = rowsObject.GetComponent<VerticalLayoutGroup>();
        rowsLayout.childAlignment = TextAnchor.UpperCenter;
        rowsLayout.spacing = 24f;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;

        clickText = CreateText("ClickText", paper, "クリックで宿舎へ帰還", 56f, new Color(0.18f, 0.12f, 0.08f, 1f), TextAlignmentOptions.Center);
        RectTransform clickRect = clickText.GetComponent<RectTransform>();
        clickRect.anchorMin = new Vector2(0f, 0f);
        clickRect.anchorMax = new Vector2(1f, 0f);
        clickRect.pivot = new Vector2(0.5f, 0f);
        clickRect.offsetMin = new Vector2(72f, 38f);
        clickRect.offsetMax = new Vector2(-72f, 118f);
    }

    private void BuildRows(int defeatedEnemies, IReadOnlyDictionary<MaterialManager.MaterialType, int> materialGains)
    {
        for (int i = rowsParent.childCount - 1; i >= 0; i--)
        {
            Destroy(rowsParent.GetChild(i).gameObject);
        }

        CreateRow(null, "倒した敵", defeatedEnemies, new Color(0.72f, 0.16f, 0.12f, 1f));

        MaterialManager.MaterialType[] types =
        {
            MaterialManager.MaterialType.Scrap,
            MaterialManager.MaterialType.Gear,
            MaterialManager.MaterialType.UpgradeCore,
            MaterialManager.MaterialType.ModuleCore_lv1,
            MaterialManager.MaterialType.ModuleCore_lv2,
            MaterialManager.MaterialType.ModuleCore_lv3,
        };

        for (int i = 0; i < types.Length; i++)
        {
            MaterialManager.MaterialType type = types[i];
            int amount = materialGains != null && materialGains.TryGetValue(type, out int gained) ? gained : 0;
            CreateRow(GetMaterialIcon(type), FormatMaterialName(type), amount, Color.white);
        }
    }

    private void CreateRow(Sprite iconSprite, string label, int amount, Color fallbackIconColor)
    {
        GameObject rowObject = new GameObject(label + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.transform.SetParent(rowsParent, false);
        LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
        rowElement.preferredHeight = 70f;

        HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.spacing = 28f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(rowObject.transform, false);
        LayoutElement iconElement = iconObject.GetComponent<LayoutElement>();
        iconElement.preferredWidth = 68f;
        iconElement.preferredHeight = 68f;
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = iconSprite;
        icon.color = iconSprite == null ? fallbackIconColor : Color.white;
        icon.raycastTarget = false;

        TMP_Text labelText = CreateText("Label", rowObject.transform, label, 56f, new Color(0.16f, 0.11f, 0.06f, 1f), TextAlignmentOptions.MidlineLeft);
        LayoutElement labelElement = labelText.gameObject.AddComponent<LayoutElement>();
        labelElement.preferredWidth = 680f;
        labelElement.preferredHeight = 70f;

        TMP_Text amountText = CreateText("Amount", rowObject.transform, "x" + amount, 60f, new Color(0.16f, 0.11f, 0.06f, 1f), TextAlignmentOptions.MidlineRight);
        LayoutElement amountElement = amountText.gameObject.AddComponent<LayoutElement>();
        amountElement.preferredWidth = 240f;
        amountElement.preferredHeight = 70f;
    }

    private TMP_Text CreateText(string name, Transform parent, string value, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        ApplyPlainTextMaterial(text);
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private void ApplyFontToChildren(RectTransform root)
    {
        if (fontAsset == null || root == null)
        {
            return;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
            {
                texts[i].font = fontAsset;
                ApplyPlainTextMaterial(texts[i]);
            }
        }
    }

    private static void ApplyPlainTextMaterial(TMP_Text text)
    {
        if (text == null || text.fontSharedMaterial == null)
        {
            return;
        }

        Material material = new Material(text.fontSharedMaterial);
        material.name = text.fontSharedMaterial.name + " No Outline";
        if (material.HasProperty(ShaderUtilities.ID_OutlineWidth))
        {
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        }

        if (material.HasProperty(ShaderUtilities.ID_UnderlaySoftness))
        {
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        }

        if (material.HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
        {
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        }

        if (material.HasProperty(ShaderUtilities.ID_UnderlayOffsetY))
        {
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
        }

        text.fontMaterial = material;
    }

    private void StartClickTextBlink()
    {
        if (clickText == null)
        {
            return;
        }

        StopClickTextBlink();
        clickText.alpha = 1f;
        clickBlinkTween = clickText.DOFade(0.28f, 0.65f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopClickTextBlink()
    {
        if (clickBlinkTween != null)
        {
            clickBlinkTween.Kill();
            clickBlinkTween = null;
        }

        if (clickText != null)
        {
            clickText.alpha = 1f;
        }
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

    private static string FormatMaterialName(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap:
                return "Scrap";
            case MaterialManager.MaterialType.Gear:
                return "Gear";
            case MaterialManager.MaterialType.UpgradeCore:
                return "Upgrade Core";
            case MaterialManager.MaterialType.ModuleCore_lv1:
                return "Module Core Lv1";
            case MaterialManager.MaterialType.ModuleCore_lv2:
                return "Module Core Lv2";
            case MaterialManager.MaterialType.ModuleCore_lv3:
                return "Module Core Lv3";
            default:
                return type.ToString();
        }
    }
}
