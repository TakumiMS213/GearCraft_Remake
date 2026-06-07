using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoryView : MonoBehaviour
{
    [SerializeField] private CanvasGroup pageGroup;
    [SerializeField] private Image storyImage;
    [SerializeField] private TMP_Text storyText;
    [SerializeField, Min(0f)] private float fadeDuration = 0.7f;

    private Tween fadeTween;

    public bool IsShowing => fadeTween != null && fadeTween.IsActive();
    public float FadeDuration => fadeDuration;

    private void Awake()
    {
        if (pageGroup != null)
        {
            pageGroup.alpha = 0f;
        }
    }

    public Tween Show(StoryPageData page)
    {
        fadeTween?.Kill();

        if (storyImage != null)
        {
            storyImage.sprite = page.Image;
            storyImage.enabled = page.Image != null;
        }

        if (storyText != null)
        {
            storyText.text = page.Text;
        }

        if (pageGroup == null)
        {
            return null;
        }

        pageGroup.alpha = 0f;
        fadeTween = pageGroup
            .DOFade(1f, fadeDuration)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true);
        return fadeTween;
    }

    public void CompleteShow()
    {
        fadeTween?.Complete();
    }
}
