using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryItemView : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text countText;
    public TMP_Text descriptionText;
    public Sprite frameSprite;
    public bool preserveSceneLayout = true;

    public void SetData(Sprite icon, string itemName, int count, string description, Sprite frame)
    {
        frameSprite = frame;
        EnsureReferences();

        if (iconImage != null)
        {
            iconImage.enabled = icon != null;
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
        }

        if (nameText != null)
        {
            nameText.text = itemName;
        }

        if (countText != null)
        {
            countText.text = $"x{count}";
        }

        if (descriptionText != null)
        {
            descriptionText.text = description;
        }
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        RectTransform root = transform as RectTransform;
        if (root == null)
        {
            return;
        }

        Image background = root.GetComponent<Image>();
        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
        }
        background.sprite = frameSprite;
        background.type = frameSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = frameSprite != null ? Color.white : new Color(0.08f, 0.07f, 0.055f, 0.92f);
        background.raycastTarget = false;

        LayoutElement layout = root.GetComponent<LayoutElement>();
        bool addedLayout = layout == null;
        if (layout == null)
        {
            layout = gameObject.AddComponent<LayoutElement>();
        }
        if (addedLayout || !preserveSceneLayout)
        {
            layout.preferredWidth = 240f;
            layout.preferredHeight = 285f;
        }
        layout.ignoreLayout = preserveSceneLayout;

        iconImage = EnsureImage(root, "Icon", new Vector2(0.2f, 0.65f), new Vector2(0.8f, 0.93f), Vector2.zero, Vector2.zero, !preserveSceneLayout);
        nameText = EnsureText(root, "NameText", new Vector2(0.1f, 0.54f), new Vector2(0.9f, 0.64f), Vector2.zero, Vector2.zero, 20f, TextAlignmentOptions.Center, !preserveSceneLayout);
        countText = EnsureText(root, "CountText", new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.53f), Vector2.zero, Vector2.zero, 19f, TextAlignmentOptions.Center, !preserveSceneLayout);
        descriptionText = EnsureText(root, "DescriptionText", new Vector2(0.11f, 0.12f), new Vector2(0.89f, 0.4f), Vector2.zero, Vector2.zero, 15f, TextAlignmentOptions.TopLeft, !preserveSceneLayout);
    }

    private static Image EnsureImage(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool applyLayoutToExisting)
    {
        RectTransform rect = EnsureRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, applyLayoutToExisting);
        Image image = rect.GetComponent<Image>();
        if (image == null)
        {
            image = rect.gameObject.AddComponent<Image>();
        }
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static TMP_Text EnsureText(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float size, TextAlignmentOptions alignment, bool applyLayoutToExisting)
    {
        RectTransform rect = EnsureRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, applyLayoutToExisting);
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

        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = new Color(1f, 0.96f, 0.82f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform EnsureRect(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool applyLayoutToExisting)
    {
        Transform child = parent.Find(name);
        RectTransform rect = child as RectTransform;
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
