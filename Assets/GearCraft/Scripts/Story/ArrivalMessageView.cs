using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArrivalMessageView : MonoBehaviour
{
    [SerializeField] private CanvasGroup windowGroup;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text messageText;
    [SerializeField, Min(0f)] private float fadeDuration = 0.3f;

    private Tween fadeTween;

    public bool IsConfigured => windowGroup != null && portraitImage != null && messageText != null;
    public bool IsShowing => fadeTween != null && fadeTween.IsActive();
    public float FadeDuration => fadeDuration;

    private void Awake()
    {
        HideImmediate();
    }

    public Tween Show(ArrivalMessagePageData page)
    {
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

    public void HideImmediate(bool keepInputBlocked = false)
    {
        fadeTween?.Kill();
        if (windowGroup == null)
        {
            return;
        }

        windowGroup.alpha = 0f;
        // The scene's full-screen transparent Image can keep blocking menu clicks
        // while the text is hidden during a camera pan.
        windowGroup.gameObject.SetActive(keepInputBlocked);
    }

    private void OnDisable() => fadeTween?.Kill();
}
