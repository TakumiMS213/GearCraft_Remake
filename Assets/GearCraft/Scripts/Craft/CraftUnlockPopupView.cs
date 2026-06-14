using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GearCraft.Scripts.Craft
{
    public sealed class CraftUnlockPopupView : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset dotGothicFont;
        [SerializeField] private float popDuration = 0.22f;
        [SerializeField] private float closeDuration = 0.16f;

        private RectTransform root;
        private RectTransform panel;
        private CanvasGroup canvasGroup;
        private Image iconImage;
        private TMP_Text messageText;
        private TMP_Text summaryText;
        private Button clickCatcher;
        private bool closeRequested;

        public IEnumerator ShowAsync(CraftRecipeSO recipe, Transform canvasParent)
        {
            if (recipe == null || canvasParent == null)
            {
                yield break;
            }

            EnsureLayout(canvasParent);
            ApplyRecipe(recipe);

            closeRequested = false;
            root.gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            panel.localScale = Vector3.one * 0.72f;

            yield return DOTween.Sequence()
                .Join(canvasGroup.DOFade(1f, popDuration))
                .Join(panel.DOScale(Vector3.one, popDuration).SetEase(Ease.OutBack))
                .WaitForCompletion();

            yield return new WaitUntil(() => closeRequested);

            yield return DOTween.Sequence()
                .Join(canvasGroup.DOFade(0f, closeDuration))
                .Join(panel.DOScale(Vector3.one * 0.72f, closeDuration).SetEase(Ease.InBack))
                .WaitForCompletion();

            root.gameObject.SetActive(false);
        }

        private void EnsureLayout(Transform canvasParent)
        {
            if (root != null)
            {
                return;
            }

            ResolveFont();

            GameObject rootObject = new GameObject("CraftUnlockPopupRoot", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
            rootObject.transform.SetParent(canvasParent, false);
            root = rootObject.GetComponent<RectTransform>();
            Stretch(root);

            Image dim = rootObject.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.64f);

            canvasGroup = rootObject.GetComponent<CanvasGroup>();
            clickCatcher = rootObject.GetComponent<Button>();
            clickCatcher.transition = Selectable.Transition.None;
            clickCatcher.onClick.AddListener(() => closeRequested = true);

            panel = CreateImage("UnlockPanel", root, new Color(0.04f, 0.04f, 0.045f, 0.98f)).rectTransform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(760f, 420f);
            panel.anchoredPosition = Vector2.zero;

            iconImage = CreateImage("UnlockIcon", panel, Color.white);
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(132f, 132f);
            iconRect.anchoredPosition = new Vector2(0f, -56f);
            iconImage.preserveAspect = true;

            messageText = CreateText("UnlockMessage", panel, 36f, FontStyles.Bold);
            SetOffsets(messageText.rectTransform, 48f, -206f, -48f, -276f);
            messageText.alignment = TextAlignmentOptions.Center;

            summaryText = CreateText("UnlockSummary", panel, 27f, FontStyles.Normal);
            SetOffsets(summaryText.rectTransform, 62f, -286f, -62f, -374f);
            summaryText.alignment = TextAlignmentOptions.Center;

            rootObject.SetActive(false);
        }

        private void ApplyRecipe(CraftRecipeSO recipe)
        {
            if (iconImage != null)
            {
                iconImage.sprite = recipe.NotificationIcon;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (messageText != null)
            {
                messageText.text = $"{recipe.DisplayName}のCraftが解放されました！";
            }

            if (summaryText != null)
            {
                summaryText.text = recipe.Summary;
            }
        }

        private void ResolveFont()
        {
            if (dotGothicFont != null)
            {
                return;
            }

            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_FontAsset font = texts[i] != null ? texts[i].font : null;
                if (font != null && font.name.Contains("DotGothic"))
                {
                    dotGothicFont = font;
                    return;
                }
            }

            dotGothicFont = Resources.Load<TMP_FontAsset>("DotGothic16-Regular SDF");
        }

        private TMP_Text CreateText(string objectName, Transform parent, float fontSize, FontStyles fontStyle)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.font = dotGothicFont != null ? dotGothicFont : text.font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.enableWordWrapping = true;
            return text;
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetOffsets(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}
