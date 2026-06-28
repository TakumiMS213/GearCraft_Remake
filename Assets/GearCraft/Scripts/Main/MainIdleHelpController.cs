using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GearCraft.Scripts.Main
{
    public sealed class MainIdleHelpController : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float moveHelpDelay = 3f;
        [SerializeField] private float attackHelpDelay = 3f;
        [SerializeField] private float boostHelpDelay = 5f;

        [Header("Left Bottom Help")]
        [SerializeField] private RectTransform leftBottomRoot;
        [SerializeField] private Sprite moveIcon;
        [SerializeField] private Sprite attackIcon;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string moveText = "移動";
        [SerializeField] private string attackText = "攻撃";

        [Header("Existing BOOST Help")]
        [SerializeField] private GameObject boostKeyObject;
        [SerializeField] private GameObject boostTextObject;

        private GameObject movePrompt;
        private GameObject attackPrompt;
        private float moveIdleTime;
        private float attackIdleTime;
        private float boostIdleTime;

        private void Awake()
        {
            EnsureLeftBottomPrompts();
            ResolveBoostObjects();
            SetPromptActive(movePrompt, false);
            SetPromptActive(attackPrompt, false);
            SetBoostPromptActive(false);
        }

        private void Update()
        {
            UpdateMoveHelp();
            UpdateAttackHelp();
            UpdateBoostHelp();
        }

        private void UpdateMoveHelp()
        {
            bool moved = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f ||
                         Input.GetKeyDown(KeyCode.Space) ||
                         Input.GetKeyDown(KeyCode.A) ||
                         Input.GetKeyDown(KeyCode.D) ||
                         Input.GetKeyDown(KeyCode.LeftArrow) ||
                         Input.GetKeyDown(KeyCode.RightArrow);

            moveIdleTime = moved ? 0f : moveIdleTime + Time.deltaTime;
            SetPromptActive(movePrompt, moveIdleTime >= moveHelpDelay);
        }

        private void UpdateAttackHelp()
        {
            bool attacked = Input.GetMouseButton(0) || Input.GetMouseButtonDown(0);
            attackIdleTime = attacked ? 0f : attackIdleTime + Time.deltaTime;
            SetPromptActive(attackPrompt, attackIdleTime >= attackHelpDelay);
        }

        private void UpdateBoostHelp()
        {
            bool boosted = Input.GetKeyDown(KeyCode.Q);
            boostIdleTime = boosted ? 0f : boostIdleTime + Time.deltaTime;
            SetBoostPromptActive(boostIdleTime >= boostHelpDelay);
        }

        private void EnsureLeftBottomPrompts()
        {
            if (leftBottomRoot == null)
            {
                leftBottomRoot = CreateDefaultLeftBottomRoot();
            }

            movePrompt = CreatePrompt("MoveHelp", moveIcon, moveText);
            attackPrompt = CreatePrompt("AttackHelp", attackIcon, attackText);
        }

        private RectTransform CreateDefaultLeftBottomRoot()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("IdleHelpCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 2500;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            GameObject rootObject = new GameObject("IdleHelpLeftBottom", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rootObject.transform.SetParent(canvas.transform, false);

            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(44f, 44f);
            rect.sizeDelta = new Vector2(360f, 160f);

            VerticalLayoutGroup layout = rootObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.spacing = 12f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = rootObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rect;
        }

        private GameObject CreatePrompt(string objectName, Sprite icon, string label)
        {
            Transform existing = leftBottomRoot != null ? leftBottomRoot.Find(objectName) : null;
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject row = new GameObject(objectName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(CanvasGroup));
            row.transform.SetParent(leftBottomRoot, false);

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(360f, 64f);

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 14f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = row.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObject.transform.SetParent(row.transform, false);
            Image image = iconObject.GetComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
            iconLayout.preferredWidth = 76f;
            iconLayout.preferredHeight = 48f;

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(row.transform, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = label;
            text.font = font != null ? font : text.font;
            text.fontSize = 42f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            LayoutElement textLayout = textObject.GetComponent<LayoutElement>();
            textLayout.preferredWidth = 220f;
            textLayout.preferredHeight = 58f;

            return row;
        }

        private void ResolveBoostObjects()
        {
            if (boostKeyObject == null)
            {
                GameObject found = GameObject.Find("Q");
                if (found != null)
                {
                    boostKeyObject = found;
                }
            }

            if (boostTextObject == null)
            {
                GameObject found = GameObject.Find("boost");
                if (found != null)
                {
                    boostTextObject = found;
                }
            }
        }

        private static void SetPromptActive(GameObject prompt, bool active)
        {
            if (prompt != null && prompt.activeSelf != active)
            {
                prompt.SetActive(active);
            }
        }

        private void SetBoostPromptActive(bool active)
        {
            SetPromptActive(boostKeyObject, active);
            SetPromptActive(boostTextObject, active);
        }
    }
}
