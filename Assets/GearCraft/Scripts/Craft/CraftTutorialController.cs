using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GearCraft.Scripts.Craft
{
    public sealed class CraftTutorialController : MonoBehaviour
    {
        private const int CraftTab = 0;
        private const int WeaponCustomTab = 1;
        private const string CraftAutoPlayedKey = "GearCraft.CraftTutorial.AutoPlayed.Craft.v3";
        private const string WeaponCustomAutoPlayedKey = "GearCraft.CraftTutorial.AutoPlayed.WeaponCustom.v2";

        private readonly TutorialStep[] craftSteps =
        {
            new TutorialStep("Craft", "ここでは新しい武器や機能をクラフトできます。", "CraftTabButton"),
            new TutorialStep("Recipe", "左側のリストから作りたい項目を選びます。", "CraftRecipeScrollView"),
            new TutorialStep("Materials", "必要素材と現在の所持数を確認します。", "MaterialDisplayArea"),
            new TutorialStep("Craft Button", "素材が足りていれば、このボタンで作成できます。", "craft_button"),
            new TutorialStep("Help", "もう一度見たいときは、この「？」ボタンを押してください。", "question"),
        };

        private readonly TutorialStep[] weaponCustomSteps =
        {
            new TutorialStep("Weapon Custom", "ここでは武器に強化パーツを装着できます。", "UpgradeTabButton"),
            new TutorialStep("Parts Shop", "左側のパーツショップから強化パーツを選びます。", "ShopArea"),
            new TutorialStep("Grid", "パーツをグリッドへ配置すると効果が発動します。", "GridArea"),
            new TutorialStep("Effects", "現在発動している効果はここで確認できます。", "EffectSummary"),
            new TutorialStep("Help", "もう一度見たいときは、この「？」ボタンを押してください。", "question"),
        };

        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform dimTop;
        private RectTransform dimBottom;
        private RectTransform dimLeft;
        private RectTransform dimRight;
        private RectTransform borderTop;
        private RectTransform borderBottom;
        private RectTransform borderLeft;
        private RectTransform borderRight;
        private RectTransform messagePanel;
        private TMP_Text titleText;
        private TMP_Text bodyText;
        private TMP_Text pageText;
        private Button nextButton;
        private Button closeButton;
        private TMP_FontAsset fontAsset;
        private GameObject tutorialRoot;
        private GameObject confirmRoot;
        private TMP_Text confirmText;
        private int currentTab;
        private int currentStepIndex;
        private bool craftAutoPlayedThisScene;
        private bool weaponCustomAutoPlayedThisScene;
        private int pendingAutoPlayedTab = -1;
        private Coroutine autoStartCoroutine;
        private TutorialStep[] currentSteps;

        private void Awake()
        {
            EnsureLayout();
            HideTutorial();
            HideConfirm();
        }

        private void LateUpdate()
        {
            if (tutorialRoot != null && tutorialRoot.activeSelf)
            {
                UpdateHighlight();
            }
        }

        public void OnTabOpened(int tabIndex)
        {
            currentTab = tabIndex;
            EnsureLayout();

            if (tabIndex == CraftTab)
            {
                return;
            }

            TryAutoStartTutorial(tabIndex);
        }

        public void OnCraftSceneEntered()
        {
            EnsureLayout();
            TryAutoStartTutorial(CraftTab);
        }

        private void TryAutoStartTutorial(int tabIndex)
        {
            if (IsTutorialActive() || HasAutoPlayedThisScene(tabIndex))
            {
                return;
            }

            if (autoStartCoroutine != null)
            {
                StopCoroutine(autoStartCoroutine);
            }

            autoStartCoroutine = StartCoroutine(StartTutorialWhenReady(tabIndex));
        }

        private IEnumerator StartTutorialWhenReady(int tabIndex)
        {
            const int MaxWaitFrames = 20;
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                yield return null;

                TutorialStep[] steps = tabIndex == WeaponCustomTab ? weaponCustomSteps : craftSteps;
                if (steps.Length == 0 || FindActiveRectTransform(steps[0].TargetName) != null)
                {
                    StartTutorial(tabIndex, true);
                    autoStartCoroutine = null;
                    yield break;
                }
            }

            StartTutorial(tabIndex, true);
            autoStartCoroutine = null;
        }

        private void ShowConfirm()
        {
            EnsureLayout();
            HideTutorial();
            if (confirmRoot != null)
            {
                confirmRoot.SetActive(true);
            }

            if (confirmText != null)
            {
                confirmText.text = "チュートリアルを開始しますか？";
            }
        }

        private void StartTutorial(int tabIndex, bool markAutoPlayed = false)
        {
            currentTab = tabIndex;
            currentSteps = tabIndex == WeaponCustomTab ? weaponCustomSteps : craftSteps;
            currentStepIndex = 0;

            if (markAutoPlayed)
            {
                MarkAutoStartedThisScene(tabIndex);
                pendingAutoPlayedTab = tabIndex;
            }
            else
            {
                pendingAutoPlayedTab = -1;
            }

            if (tutorialRoot != null)
            {
                tutorialRoot.SetActive(true);
            }

            HideConfirm();
            ShowCurrentStep();
        }

        private void ShowCurrentStep()
        {
            if (currentSteps == null || currentStepIndex < 0 || currentStepIndex >= currentSteps.Length)
            {
                CompleteTutorial();
                return;
            }

            TutorialStep step = currentSteps[currentStepIndex];
            if (titleText != null)
            {
                titleText.text = step.Title;
            }

            if (bodyText != null)
            {
                bodyText.text = step.Body;
            }

            if (pageText != null)
            {
                pageText.text = $"{currentStepIndex + 1}/{currentSteps.Length}";
            }

            TMP_Text nextText = nextButton != null ? nextButton.GetComponentInChildren<TMP_Text>() : null;
            if (nextText != null)
            {
                nextText.text = currentStepIndex == currentSteps.Length - 1 ? "OK" : "NEXT";
            }

            UpdateHighlight();
        }

        private void NextStep()
        {
            currentStepIndex++;
            ShowCurrentStep();
        }

        private void CompleteTutorial()
        {
            if (pendingAutoPlayedTab >= 0)
            {
                PersistAutoPlayed(pendingAutoPlayedTab);
                pendingAutoPlayedTab = -1;
            }

            HideTutorial();
        }

        private void HideTutorial()
        {
            if (tutorialRoot != null)
            {
                tutorialRoot.SetActive(false);
            }
        }

        private void HideConfirm()
        {
            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }
        }

        private bool IsTutorialActive()
        {
            return tutorialRoot != null && tutorialRoot.activeSelf;
        }

        private void UpdateHighlight()
        {
            TutorialStep step = currentSteps != null &&
                currentStepIndex >= 0 &&
                currentStepIndex < currentSteps.Length
                    ? currentSteps[currentStepIndex]
                    : null;

            RectTransform target = ResolveTarget(step);
            if (target == null || canvasRect == null)
            {
                ApplyFullDim();
                return;
            }

            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Vector2 min = ScreenToCanvas(target, corners[0]);
            Vector2 max = ScreenToCanvas(target, corners[2]);
            float padding = 12f;
            min -= Vector2.one * padding;
            max += Vector2.one * padding;

            Rect canvasBounds = canvasRect.rect;
            min.x = Mathf.Clamp(min.x, canvasBounds.xMin, canvasBounds.xMax);
            min.y = Mathf.Clamp(min.y, canvasBounds.yMin, canvasBounds.yMax);
            max.x = Mathf.Clamp(max.x, canvasBounds.xMin, canvasBounds.xMax);
            max.y = Mathf.Clamp(max.y, canvasBounds.yMin, canvasBounds.yMax);

            SetPanel(dimTop, canvasBounds.xMin, max.y, canvasBounds.width, canvasBounds.yMax - max.y);
            SetPanel(dimBottom, canvasBounds.xMin, canvasBounds.yMin, canvasBounds.width, min.y - canvasBounds.yMin);
            SetPanel(dimLeft, canvasBounds.xMin, min.y, min.x - canvasBounds.xMin, max.y - min.y);
            SetPanel(dimRight, max.x, min.y, canvasBounds.xMax - max.x, max.y - min.y);

            const float border = 4f;
            SetPanel(borderTop, min.x, max.y - border, max.x - min.x, border);
            SetPanel(borderBottom, min.x, min.y, max.x - min.x, border);
            SetPanel(borderLeft, min.x, min.y, border, max.y - min.y);
            SetPanel(borderRight, max.x - border, min.y, border, max.y - min.y);
            PlaceMessagePanel(min, max, canvasBounds);
        }

        private Vector2 ScreenToCanvas(RectTransform source, Vector3 worldPosition)
        {
            Canvas sourceCanvas = source != null ? source.GetComponentInParent<Canvas>() : null;
            Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? sourceCanvas.worldCamera
                : null;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);
            return localPoint;
        }

        private void ApplyFullDim()
        {
            if (canvasRect == null)
            {
                return;
            }

            Rect rect = canvasRect.rect;
            SetPanel(dimTop, rect.xMin, rect.yMin, rect.width, rect.height);
            SetPanel(dimBottom, 0f, 0f, 0f, 0f);
            SetPanel(dimLeft, 0f, 0f, 0f, 0f);
            SetPanel(dimRight, 0f, 0f, 0f, 0f);
            SetPanel(borderTop, 0f, 0f, 0f, 0f);
            SetPanel(borderBottom, 0f, 0f, 0f, 0f);
            SetPanel(borderLeft, 0f, 0f, 0f, 0f);
            SetPanel(borderRight, 0f, 0f, 0f, 0f);
            PlaceMessagePanelAtBottom();
        }

        private void PlaceMessagePanel(Vector2 highlightMin, Vector2 highlightMax, Rect canvasBounds)
        {
            if (messagePanel == null)
            {
                return;
            }

            float margin = 36f;
            float panelHeight = messagePanel.sizeDelta.y;
            float bottomTop = canvasBounds.yMin + margin + panelHeight;
            bool overlapsBottom = highlightMin.y < bottomTop;

            if (overlapsBottom)
            {
                messagePanel.anchorMin = new Vector2(0.5f, 1f);
                messagePanel.anchorMax = new Vector2(0.5f, 1f);
                messagePanel.pivot = new Vector2(0.5f, 1f);
                messagePanel.anchoredPosition = new Vector2(0f, -margin);
                return;
            }

            PlaceMessagePanelAtBottom();
        }

        private void PlaceMessagePanelAtBottom()
        {
            if (messagePanel == null)
            {
                return;
            }

            messagePanel.anchorMin = new Vector2(0.5f, 0f);
            messagePanel.anchorMax = new Vector2(0.5f, 0f);
            messagePanel.pivot = new Vector2(0.5f, 0f);
            messagePanel.anchoredPosition = new Vector2(0f, 36f);
        }

        private static void SetPanel(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null)
            {
                return;
            }

            width = Mathf.Max(0f, width);
            height = Mathf.Max(0f, height);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform FindRectTransform(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            RectTransform inactiveMatch = null;
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform rect = rects[i];
                if (rect == null || rect.gameObject.name != objectName)
                {
                    continue;
                }

                if (rect.gameObject.activeInHierarchy)
                {
                    return rect;
                }

                inactiveMatch ??= rect;
            }

            return inactiveMatch;
        }

        private RectTransform ResolveTarget(TutorialStep step)
        {
            if (step == null)
            {
                return null;
            }

            if (step.TargetName == "question")
            {
                return FindQuestionRectTransform(currentTab);
            }

            return FindRectTransform(step.TargetName);
        }

        private static RectTransform FindQuestionRectTransform(int tabIndex)
        {
            RectTransform[] questions = FindRectTransforms("question");
            RectTransform inactiveMatch = null;
            for (int i = 0; i < questions.Length; i++)
            {
                RectTransform question = questions[i];
                if (question == null)
                {
                    continue;
                }

                bool isCraftQuestion = IsChildOfNamedParent(question, "CraftPanel");
                if ((tabIndex == CraftTab) != isCraftQuestion)
                {
                    continue;
                }

                if (question.gameObject.activeInHierarchy)
                {
                    return question;
                }

                inactiveMatch ??= question;
            }

            return inactiveMatch;
        }

        private static RectTransform FindActiveRectTransform(string objectName)
        {
            RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform rect = rects[i];
                if (rect != null && rect.gameObject.name == objectName && rect.gameObject.activeInHierarchy)
                {
                    return rect;
                }
            }

            return null;
        }

        private static RectTransform[] FindRectTransforms(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return Array.Empty<RectTransform>();
            }

            RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i] != null && rects[i].gameObject.name == objectName)
                {
                    count++;
                }
            }

            RectTransform[] matches = new RectTransform[count];
            int index = 0;
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i] != null && rects[i].gameObject.name == objectName)
                {
                    matches[index] = rects[i];
                    index++;
                }
            }

            return matches;
        }

        private static bool IsChildOfNamedParent(Transform child, string parentName)
        {
            Transform current = child;
            while (current != null)
            {
                if (current.gameObject.name == parentName)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private bool HasAutoPlayedThisScene(int tabIndex)
        {
            if (tabIndex == WeaponCustomTab)
            {
                return weaponCustomAutoPlayedThisScene || PlayerPrefs.GetInt(WeaponCustomAutoPlayedKey, 0) == 1;
            }

            return craftAutoPlayedThisScene || PlayerPrefs.GetInt(CraftAutoPlayedKey, 0) == 1;
        }

        private void MarkAutoStartedThisScene(int tabIndex)
        {
            if (tabIndex == WeaponCustomTab)
            {
                weaponCustomAutoPlayedThisScene = true;
                return;
            }

            craftAutoPlayedThisScene = true;
        }

        private void PersistAutoPlayed(int tabIndex)
        {
            if (tabIndex == WeaponCustomTab)
            {
                PlayerPrefs.SetInt(WeaponCustomAutoPlayedKey, 1);
                PlayerPrefs.Save();
                return;
            }

            PlayerPrefs.SetInt(CraftAutoPlayedKey, 1);
            PlayerPrefs.Save();
        }

        private void EnsureLayout()
        {
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("CraftTutorialCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            fontAsset = ResolveDotGothicFont();
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            canvasRect = canvasObject.GetComponent<RectTransform>();

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RegisterExistingQuestionButton();
            CreateTutorialRoot(canvasObject.transform);
            CreateConfirmRoot(canvasObject.transform);
        }

        private void RegisterExistingQuestionButton()
        {
            RectTransform[] questions = FindRectTransforms("question");
            for (int i = 0; i < questions.Length; i++)
            {
                RectTransform question = questions[i];
                Button button = question != null ? question.GetComponent<Button>() : null;
                if (button == null)
                {
                    continue;
                }

                int resolvedTabIndex = IsChildOfNamedParent(question, "CraftPanel") ? CraftTab : WeaponCustomTab;
                button.onClick.AddListener(() => ShowConfirm(resolvedTabIndex));
            }
        }

        private void ShowConfirm(int tabIndex)
        {
            currentTab = tabIndex;
            ShowConfirm();
        }

        private void CreateTutorialRoot(Transform parent)
        {
            tutorialRoot = new GameObject("CraftTutorialRoot", typeof(RectTransform));
            tutorialRoot.transform.SetParent(parent, false);
            Stretch(tutorialRoot.GetComponent<RectTransform>());

            Color dimColor = new Color(0f, 0f, 0f, 0.72f);
            dimTop = CreateImage("DimTop", tutorialRoot.transform, dimColor).rectTransform;
            dimBottom = CreateImage("DimBottom", tutorialRoot.transform, dimColor).rectTransform;
            dimLeft = CreateImage("DimLeft", tutorialRoot.transform, dimColor).rectTransform;
            dimRight = CreateImage("DimRight", tutorialRoot.transform, dimColor).rectTransform;

            Color borderColor = new Color(1f, 0.82f, 0.22f, 1f);
            borderTop = CreateImage("HighlightTop", tutorialRoot.transform, borderColor).rectTransform;
            borderBottom = CreateImage("HighlightBottom", tutorialRoot.transform, borderColor).rectTransform;
            borderLeft = CreateImage("HighlightLeft", tutorialRoot.transform, borderColor).rectTransform;
            borderRight = CreateImage("HighlightRight", tutorialRoot.transform, borderColor).rectTransform;

            messagePanel = CreateImage("TutorialMessagePanel", tutorialRoot.transform, new Color(0.04f, 0.04f, 0.045f, 0.96f)).rectTransform;
            messagePanel.anchorMin = new Vector2(0.5f, 0f);
            messagePanel.anchorMax = new Vector2(0.5f, 0f);
            messagePanel.pivot = new Vector2(0.5f, 0f);
            messagePanel.anchoredPosition = new Vector2(0f, 36f);
            messagePanel.sizeDelta = new Vector2(960f, 220f);

            titleText = CreateText("Title", messagePanel, 34f, FontStyles.Bold, new Vector2(34f, -24f), new Vector2(-34f, -70f));
            bodyText = CreateText("Body", messagePanel, 29f, FontStyles.Normal, new Vector2(34f, -82f), new Vector2(-34f, -132f));
            pageText = CreateText("Page", messagePanel, 22f, FontStyles.Normal, new Vector2(34f, -176f), new Vector2(-820f, -206f));

            nextButton = CreateButton("NextButton", messagePanel, "NEXT", new Vector2(-34f, 24f), new Vector2(144f, 52f));
            closeButton = CreateButton("CloseButton", messagePanel, "CLOSE", new Vector2(-192f, 24f), new Vector2(144f, 52f));
            nextButton.onClick.AddListener(NextStep);
            closeButton.onClick.AddListener(CompleteTutorial);
        }

        private void CreateConfirmRoot(Transform parent)
        {
            confirmRoot = new GameObject("CraftTutorialConfirmRoot", typeof(RectTransform), typeof(Image));
            confirmRoot.transform.SetParent(parent, false);
            Stretch(confirmRoot.GetComponent<RectTransform>());
            confirmRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.64f);

            RectTransform panel = CreateImage("ConfirmPanel", confirmRoot.transform, new Color(0.04f, 0.04f, 0.045f, 0.98f)).rectTransform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(620f, 260f);

            confirmText = CreateText("ConfirmText", panel, 32f, FontStyles.Bold, new Vector2(40f, -42f), new Vector2(-40f, -116f));
            confirmText.alignment = TextAlignmentOptions.Center;

            Button yesButton = CreateButton("YesButton", panel, "YES", new Vector2(-164f, 36f), new Vector2(144f, 58f));
            Button noButton = CreateButton("NoButton", panel, "NO", new Vector2(164f, 36f), new Vector2(144f, 58f));
            CenterButton(yesButton, new Vector2(-90f, -72f));
            CenterButton(noButton, new Vector2(90f, -72f));
            yesButton.onClick.AddListener(() => StartTutorial(currentTab));
            noButton.onClick.AddListener(HideConfirm);
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string objectName, Transform parent, float fontSize, FontStyles fontStyle, Vector2 topLeft, Vector2 bottomRight)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(topLeft.x, bottomRight.y);
            rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);

            TMP_Text text = gameObject.GetComponent<TMP_Text>();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            ApplyFont(text);
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            return text;
        }

        private static TMP_FontAsset ResolveDotGothicFont()
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_FontAsset font = texts[i] != null ? texts[i].font : null;
                if (font != null && font.name.Contains("DotGothic", StringComparison.OrdinalIgnoreCase))
                {
                    return font;
                }
            }

            return Resources.Load<TMP_FontAsset>("DotGothic16-Regular SDF");
        }

        private static void ApplyFont(TMP_Text text)
        {
            CraftTutorialController controller = FindFirstObjectByType<CraftTutorialController>();
            if (text != null && controller != null && controller.fontAsset != null)
            {
                text.font = controller.fontAsset;
            }
        }

        private static Button CreateButton(string objectName, Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = gameObject.GetComponent<Image>();
            image.color = new Color(0.86f, 0.64f, 0.18f, 1f);

            Button button = gameObject.GetComponent<Button>();
            TMP_Text text = CreateText("Text", gameObject.transform, 24f, FontStyles.Bold, Vector2.zero, Vector2.zero);
            RectTransform textRect = text.GetComponent<RectTransform>();
            Stretch(textRect);
            text.text = label;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static void CenterButton(Button button, Vector2 anchoredPosition)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        [Serializable]
        private sealed class TutorialStep
        {
            public readonly string Title;
            public readonly string Body;
            public readonly string TargetName;

            public TutorialStep(string title, string body, string targetName)
            {
                Title = title;
                Body = body;
                TargetName = targetName;
            }
        }
    }
}
