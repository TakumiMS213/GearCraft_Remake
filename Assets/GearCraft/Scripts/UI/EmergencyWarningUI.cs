using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class EmergencyWarningUI : MonoBehaviour
{
    [Header("References")]
    public Canvas canvas;
    public CanvasGroup canvasGroup;
    public RectTransform warningRoot;
    public Image warningImage;
    public Sprite emergencySprite;

    [Header("Layout")]
    public Vector2 size = new Vector2(320f, 160f);
    public Vector2 anchoredPosition = new Vector2(-72f, 0f);

    [Header("Blink")]
    public float visibleDuration = 1.8f;
    public float blinkInterval = 0.18f;
    public float minAlpha = 0.25f;

    private Tween blinkTween;
    private int showVersion;
    private bool initialized;

    private void Awake()
    {
        EnsureInitialized();
        HideImmediate();
    }

    private void OnDisable()
    {
        StopBlink();
    }

    private void OnDestroy()
    {
        StopBlink();
    }

    public void Show()
    {
        ShowAsync().Forget();
    }

    private async UniTaskVoid ShowAsync()
    {
        EnsureInitialized();

        int version = ++showVersion;
        if (canvas != null)
        {
            canvas.enabled = true;
        }

        if (warningImage != null)
        {
            warningImage.enabled = true;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        StopBlink();
        if (canvasGroup != null)
        {
            blinkTween = canvasGroup.DOFade(minAlpha, blinkInterval)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        await UniTask.Delay((int)(visibleDuration * 1000f), ignoreTimeScale: true);
        if (version == showVersion)
        {
            HideImmediate();
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (warningRoot == null)
        {
            warningRoot = warningImage != null ? warningImage.rectTransform : transform as RectTransform;
        }

        if (warningImage == null)
        {
            warningImage = GetComponentInChildren<Image>(true);
        }

        if (warningImage != null && emergencySprite != null)
        {
            warningImage.sprite = emergencySprite;
            warningImage.preserveAspect = true;
        }

        ApplyLayout();
        initialized = true;
    }

    private void ApplyLayout()
    {
        if (warningRoot == null)
        {
            return;
        }

        warningRoot.anchorMin = new Vector2(1f, 0.5f);
        warningRoot.anchorMax = new Vector2(1f, 0.5f);
        warningRoot.pivot = new Vector2(1f, 0.5f);
        warningRoot.anchoredPosition = anchoredPosition;
        warningRoot.sizeDelta = size;
    }

    private void HideImmediate()
    {
        StopBlink();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        if (warningImage != null)
        {
            warningImage.enabled = false;
        }

        if (canvas != null)
        {
            canvas.enabled = false;
        }
    }

    private void StopBlink()
    {
        if (blinkTween != null)
        {
            blinkTween.Kill();
            blinkTween = null;
        }
    }
}
