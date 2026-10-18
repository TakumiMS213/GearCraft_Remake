using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArrivalMessageView : MonoBehaviour
{
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private bool useDedicatedOverlayCanvas = true;
    [SerializeField] private int dedicatedCanvasSortingOrder = 100;
    [SerializeField] private CanvasGroup windowGroup;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField, Min(0f)] private float fadeDuration = 0.3f;

    private Tween fadeTween;

    public bool IsShowing => fadeTween != null && fadeTween.IsActive();
    public float FadeDuration => fadeDuration;

    private void Awake()
    {
        EnsureLayout();
        HideImmediate();
    }

    public Tween Show(ArrivalMessagePageData page)
    {
        EnsureLayout();
        fadeTween?.Kill();

        if (portraitImage != null)
        {
            portraitImage.sprite = page.Portrait;
            portraitImage.enabled = page.Portrait != null;
        }

        if (messageText != null)
        {
            messageText.text = page.Text;
        }

        if (windowGroup == null)
        {
            return null;
        }

        windowGroup.gameObject.SetActive(true);
        windowGroup.alpha = 0f;
        fadeTween = windowGroup
            .DOFade(1f, fadeDuration)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true);
        return fadeTween;
    }

    public Tween Hide()
    {
        fadeTween?.Kill();

        if (windowGroup == null)
        {
            return null;
        }

        fadeTween = windowGroup
            .DOFade(0f, fadeDuration)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true)
            .OnComplete(() => windowGroup.gameObject.SetActive(false));

        return fadeTween;
    }

    public void CompleteShow()
    {
        fadeTween?.Complete();
    }

    private void HideImmediate()
    {
        if (windowGroup == null)
        {
            return;
        }

        windowGroup.alpha = 0f;
        windowGroup.gameObject.SetActive(false);
    }

    private void EnsureLayout()
    {
        if (windowGroup != null && portraitImage != null && messageText != null)
        {
            return;
        }

        Canvas canvas = useDedicatedOverlayCanvas ? FindOrCreateDedicatedCanvas() : targetCanvas;
        canvas = canvas != null ? canvas : FindFirstObjectByType<Canvas>();
        canvas = canvas != null ? canvas : CreateDedicatedCanvas();

        targetCanvas = canvas;
        CreateMessageWindow(canvas.transform);
    }

    private Canvas FindOrCreateDedicatedCanvas()
    {
        GameObject existing = GameObject.Find("ArrivalMessageCanvas");
        if (existing != null && existing.TryGetComponent(out Canvas existingCanvas))
        {
            ConfigureDedicatedCanvas(existingCanvas);
            return existingCanvas;
        }

        return CreateDedicatedCanvas();
    }

    private Canvas CreateDedicatedCanvas()
    {
        GameObject canvasObject = new GameObject("ArrivalMessageCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        ConfigureDedicatedCanvas(canvas);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private void ConfigureDedicatedCanvas(Canvas canvas)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = dedicatedCanvasSortingOrder;
    }

    private void CreateMessageWindow(Transform parent)
    {
        GameObject window = new GameObject("ArrivalMessageWindow", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        window.transform.SetParent(parent, false);
        window.transform.SetAsLastSibling();

        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0f);
        windowRect.anchorMax = new Vector2(0.5f, 0f);
        windowRect.pivot = new Vector2(0.5f, 0f);
        windowRect.anchoredPosition = new Vector2(0f, 42f);
        windowRect.sizeDelta = new Vector2(980f, 190f);

        Image background = window.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.86f);

        windowGroup = window.GetComponent<CanvasGroup>();
        windowGroup.blocksRaycasts = true;

        portraitImage = CreateImage("Portrait", window.transform, new Vector2(36f, 0f), new Vector2(150f, 150f));
        messageText = CreateText("MessageText", window.transform);
    }

    private Image CreateImage(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.preserveAspect = true;
        return image;
    }

    private TMP_Text CreateText(string objectName, Transform parent)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(220f, 26f);
        rect.offsetMax = new Vector2(-42f, -26f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        text.fontSize = 32f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.lineSpacing = 8f;
        return text;
    }
}
