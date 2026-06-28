using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GearCraft.Scripts.Main
{
    public sealed class MainOptionManualController : MonoBehaviour
    {
        [Header("Option")]
        [SerializeField] private Button optionButton;
        [SerializeField] private GameObject optionPanel;
        [SerializeField] private Button optionCloseButton;

        [Header("Manual")]
        [SerializeField] private Button manualButton;
        [SerializeField] private GameObject manualPanel;
        [SerializeField] private Button manualCloseButton;

        [Header("Assets")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite closeSprite;
        [SerializeField] private Sprite manualTitleSprite;
        [SerializeField] private Sprite moveKeySprite;
        [SerializeField] private Sprite moveTextSprite;
        [SerializeField] private Sprite attackKeySprite;
        [SerializeField] private Sprite attackTextSprite;
        [SerializeField] private Sprite boostKeySprite;
        [SerializeField] private Sprite boostTextSprite;
        [SerializeField] private Sprite shiftKeySprite;
        [SerializeField] private Sprite shiftTextSprite;
        [SerializeField] private Sprite spaceKeySprite;
        [SerializeField] private Sprite jumpTextSprite;

        private void Awake()
        {
            ResolveReferences();
            EnsureManualButton();
            EnsureManualPanel();
            RegisterButtons();
            HideOption();
            HideManual();
        }

        public void OnClick()
        {
            ShowOption();
        }

        public void ShowOption()
        {
            if (optionPanel != null)
            {
                optionPanel.SetActive(true);
                optionPanel.transform.SetAsLastSibling();
            }
        }

        public void HideOption()
        {
            if (optionPanel != null)
            {
                optionPanel.SetActive(false);
            }
        }

        public void ShowManual()
        {
            if (manualPanel != null)
            {
                manualPanel.SetActive(true);
                manualPanel.transform.SetAsLastSibling();
            }
        }

        public void HideManual()
        {
            if (manualPanel != null)
            {
                manualPanel.SetActive(false);
            }
        }

        private void ResolveReferences()
        {
            if (optionButton == null)
            {
                optionButton = FindSceneComponent<Button>("OptionButton");
            }

            if (optionPanel == null)
            {
                optionPanel = FindOptionPanel();
            }

            if (optionCloseButton == null && optionPanel != null)
            {
                optionCloseButton = FindChildComponent<Button>(optionPanel.transform, "HideSetting");
            }
        }

        private void RegisterButtons()
        {
            if (optionButton != null)
            {
                optionButton.onClick.RemoveListener(OnClick);
                optionButton.onClick.AddListener(OnClick);
            }

            if (optionCloseButton != null)
            {
                optionCloseButton.onClick.RemoveListener(HideOption);
                optionCloseButton.onClick.AddListener(HideOption);
            }

            if (manualButton != null)
            {
                manualButton.onClick.RemoveListener(ShowManual);
                manualButton.onClick.AddListener(ShowManual);
            }

            if (manualCloseButton != null)
            {
                manualCloseButton.onClick.RemoveListener(HideManual);
                manualCloseButton.onClick.AddListener(HideManual);
            }
        }

        private void EnsureManualButton()
        {
            if (manualButton != null || optionPanel == null)
            {
                return;
            }

            Transform buttonParent = GetOptionContentTransform();
            Transform existing = FindChildTransform(buttonParent, "ManualCheckButton");
            if (existing != null)
            {
                manualButton = existing.GetComponent<Button>();
                return;
            }

            RectTransform optionRect = buttonParent.GetComponent<RectTransform>();
            Button button = CreateTextButton("ManualCheckButton", buttonParent, "操作方法の確認", new Vector2(360f, -160f), new Vector2(560f, 140f));
            RectTransform rect = button.GetComponent<RectTransform>();
            if (optionRect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
            }

            manualButton = button;
        }

        private void EnsureManualPanel()
        {
            if (manualPanel != null)
            {
                return;
            }

            Canvas canvas = optionPanel != null ? optionPanel.GetComponentInParent<Canvas>(true) : FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            Transform existing = canvas.transform.Find("OperationManualPanel");
            if (existing != null)
            {
                manualPanel = existing.gameObject;
                manualCloseButton = FindChildComponent<Button>(existing, "CloseManualButton");
                return;
            }

            GameObject panel = new GameObject("OperationManualPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image background = panel.GetComponent<Image>();
            background.sprite = panelSprite;
            background.color = new Color(1f, 1f, 1f, 0.92f);
            background.raycastTarget = true;

            manualPanel = panel;
            CreateImage("ManualTitle", rect, manualTitleSprite, new Vector2(0f, 330f), new Vector2(420f, 100f));
            CreateManualRow("MoveManual", rect, moveKeySprite, moveTextSprite, new Vector2(-280f, 165f));
            CreateManualRow("AttackManual", rect, attackKeySprite, attackTextSprite, new Vector2(-280f, 35f));
            CreateManualRow("BoostManual", rect, boostKeySprite, boostTextSprite, new Vector2(-280f, -95f));
            CreateManualRow("ShiftManual", rect, shiftKeySprite, shiftTextSprite, new Vector2(-280f, -225f));
            CreateManualRow("JumpManual", rect, spaceKeySprite, jumpTextSprite, new Vector2(-280f, -355f));

            manualCloseButton = CreateIconButton("CloseManualButton", rect, closeSprite, new Vector2(650f, 330f), new Vector2(86f, 86f));
        }

        private void CreateManualRow(string rowName, RectTransform parent, Sprite keySprite, Sprite textSprite, Vector2 anchoredPosition)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = anchoredPosition;
            rowRect.sizeDelta = new Vector2(900f, 110f);

            CreateImage("Key", rowRect, keySprite, new Vector2(0f, 0f), new Vector2(180f, 88f));
            CreateImage("Text", rowRect, textSprite, new Vector2(350f, 0f), new Vector2(520f, 92f));
        }

        private void CreateImage(string objectName, RectTransform parent, Sprite sprite, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private Button CreateIconButton(string objectName, RectTransform parent, Sprite sprite, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(ButtonScaler));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = true;

            return buttonObject.GetComponent<Button>();
        }

        private Button CreateTextButton(string objectName, Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(ButtonScaler));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = buttonSprite;
            image.raycastTarget = true;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = label;
            text.font = font != null ? font : text.font;
            text.fontSize = 42f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            return buttonObject.GetComponent<Button>();
        }

        private Transform GetOptionContentTransform()
        {
            if (optionPanel == null)
            {
                return transform;
            }

            Transform content = optionPanel.transform.Find("SettingPanel");
            return content != null ? content : optionPanel.transform;
        }

        private static GameObject FindOptionPanel()
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            GameObject fallback = null;
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null ||
                    candidate.name != "SettingPanel" ||
                    !candidate.scene.IsValid())
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }

                bool hasCloseButton = FindChildComponent<Button>(candidate.transform, "HideSetting") != null;
                bool hasVisualPanelChild = candidate.transform.Find("SettingPanel") != null;
                if (hasCloseButton || hasVisualPanelChild)
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private static GameObject FindSceneObject(string objectName, bool includeInactive)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate != null &&
                    candidate.name == objectName &&
                    candidate.scene.IsValid() &&
                    (includeInactive || candidate.activeInHierarchy))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform FindChildTransform(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == childName)
                {
                    return children[i];
                }
            }

            return null;
        }

        private static T FindSceneComponent<T>(string objectName) where T : Component
        {
            GameObject gameObject = FindSceneObject(objectName, true);
            return gameObject != null ? gameObject.GetComponent<T>() : null;
        }

        private static T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
            if (parent == null)
            {
                return null;
            }

            T[] components = parent.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].gameObject.name == childName)
                {
                    return components[i];
                }
            }

            return null;
        }
    }
}
