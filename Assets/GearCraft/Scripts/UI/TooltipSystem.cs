using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipSystem : MonoBehaviour
{
    public static TooltipSystem Instance { get; private set; }

    [Header("UI")]
    public RectTransform tooltipPanel;
    public TMP_Text tooltipText;

    [Header("Position")]
    public Vector2 offset = new Vector2(28f, -12f);
    public Vector2 panelSize = new Vector2(500f, 260f);
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
        tooltipPanel.anchoredPosition = ClampToCanvas(localPosition + offset);
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
        tooltipPanel.sizeDelta = panelSize;
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
            tooltipText = CreateText(tooltipPanel);
        }
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
        rect.sizeDelta = panelSize;

        Image image = panelObject.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.05f, 0.045f, 0.035f, 0.96f);
            image.raycastTarget = false;
        }

        panelObject.SetActive(false);
        return rect;
    }

    private static TMP_Text CreateText(RectTransform parent)
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
        text.fontSize = 22f;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;

        return text;
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
        float minX = rect.xMin + edgePadding;
        float maxX = rect.xMax - size.x - edgePadding;
        float minY = rect.yMin + size.y + edgePadding;
        float maxY = rect.yMax - edgePadding;
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

        return localPosition + offset;
    }
}
