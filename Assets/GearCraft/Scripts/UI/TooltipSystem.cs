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
    public Vector2 offset = new Vector2(20f, -20f);

    private Canvas canvas;
    private RectTransform canvasRect;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        EnsureReferences();
        Hide();
    }

    private void Update()
    {
        if (tooltipPanel == null || !tooltipPanel.gameObject.activeSelf)
        {
            return;
        }

        RectTransform root = canvasRect != null ? canvasRect : tooltipPanel.parent as RectTransform;
        if (root == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            root,
            Input.mousePosition,
            canvas != null ? canvas.worldCamera : null,
            out Vector2 localPosition);

        tooltipPanel.anchoredPosition = localPosition + offset;
    }

    public void Show(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        EnsureReferences();
        if (tooltipPanel == null || tooltipText == null)
        {
            return;
        }

        tooltipText.text = text;
        tooltipPanel.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.gameObject.SetActive(false);
        }
    }

    private void EnsureReferences()
    {
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
        panelObject.transform.SetParent(transform, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(360f, 160f);

        Image image = panelObject.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.05f, 0.04f, 0.03f, 0.92f);
            image.raycastTarget = false;
        }

        panelObject.SetActive(false);
        return rect;
    }

    private static TMP_Text CreateText(RectTransform parent)
    {
        GameObject textObject = new GameObject("TooltipText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 10f);
        rect.offsetMax = new Vector2(-12f, -10f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 18f;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;

        return text;
    }
}
