using System;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class CraftTransitionManager : MonoBehaviour
{
    public event Action TransitionFinished;
    public bool IsPlaying { get; private set; }

    [Header("Transition Images")]
    public RectTransform image1;
    public RectTransform image2;
    public Image image3;
    public Image flashImage;

    [Header("Settings")]
    public float moveDuration = 0.5f;
    public float delayBeforeImage3 = 0.0f;
    public float image3DisplayTime = 1.5f;
    public float image3FadeOutTime = 0.4f;
    public Vector2 centerPos = Vector2.zero;

    [Header("Shake Settings")]
    public float shakeDistance = 5f;
    public float shakeDuration = 0.1f;

    private Vector2 image1StartPos;
    private Vector2 image2StartPos;
    private Vector2 image3StartPos;
    private Color flashBaseColor;
    private Sequence transitionSequence;
    private Sequence flashSequence;

    // Retained for existing scene references; CraftManager owns button availability.
    public Button craftButton;

    private void Awake()
    {
        if (image1 != null) image1StartPos = image1.anchoredPosition;
        if (image2 != null) image2StartPos = image2.anchoredPosition;
        if (image3 != null) image3StartPos = image3.rectTransform.anchoredPosition;
        if (flashImage != null) flashBaseColor = flashImage.color;
        ResetVisuals();
    }

    private void OnDisable()
    {
        CancelTransition();
    }

    private void OnDestroy()
    {
        CancelTransition();
    }

    public void StartTransition()
    {
        if (IsPlaying || !isActiveAndEnabled) return;
        if (image1 == null || image2 == null || image3 == null) return;

        ResetVisuals();
        RaiseTransitionObjects();
        IsPlaying = true;
        image1.gameObject.SetActive(true);
        image2.gameObject.SetActive(true);

        transitionSequence = DOTween.Sequence();
        transitionSequence.Append(image1.DOAnchorPos(centerPos, moveDuration).SetEase(Ease.OutCubic));
        transitionSequence.Join(image2.DOAnchorPos(centerPos, moveDuration).SetEase(Ease.OutCubic));
        transitionSequence.AppendInterval(Mathf.Max(0f, delayBeforeImage3));
        transitionSequence.AppendCallback(() =>
        {
            image1.gameObject.SetActive(false);
            image2.gameObject.SetActive(false);
            image3.gameObject.SetActive(true);
            if (flashImage != null)
            {
                flashImage.color = new Color(1f, 1f, 1f, 0f);
                flashSequence = DOTween.Sequence()
                    .Append(flashImage.DOFade(1f, 0.1f).SetEase(Ease.OutQuad))
                    .Append(flashImage.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
            }
        });
        transitionSequence.Append(image3.rectTransform.DOAnchorPosY(image3StartPos.y + shakeDistance, shakeDuration).SetEase(Ease.OutQuad));
        transitionSequence.Append(image3.rectTransform.DOAnchorPosY(image3StartPos.y - shakeDistance, shakeDuration).SetEase(Ease.OutQuad));
        transitionSequence.Append(image3.rectTransform.DOAnchorPosY(image3StartPos.y, shakeDuration).SetEase(Ease.OutQuad));
        transitionSequence.AppendInterval(image3DisplayTime);
        transitionSequence.Append(image3.DOFade(0f, image3FadeOutTime).SetEase(Ease.InQuad));
        transitionSequence.OnComplete(() =>
        {
            transitionSequence = null;
            FinishTransition();
        });
    }

    public void CancelTransition()
    {
        if (transitionSequence != null)
        {
            transitionSequence.Kill(false);
            transitionSequence = null;
        }
        FinishTransition();
    }

    private void FinishTransition()
    {
        if (flashSequence != null)
        {
            flashSequence.Kill(false);
            flashSequence = null;
        }
        ResetVisuals();
        bool wasPlaying = IsPlaying;
        IsPlaying = false;
        if (wasPlaying) TransitionFinished?.Invoke();
    }

    private void ResetVisuals()
    {
        if (image1 != null)
        {
            image1.anchoredPosition = image1StartPos;
            image1.gameObject.SetActive(false);
        }
        if (image2 != null)
        {
            image2.anchoredPosition = image2StartPos;
            image2.gameObject.SetActive(false);
        }
        if (image3 != null)
        {
            image3.rectTransform.anchoredPosition = image3StartPos;
            image3.color = new Color(image3.color.r, image3.color.g, image3.color.b, 1f);
            image3.gameObject.SetActive(false);
        }
        if (flashImage != null)
            flashImage.color = new Color(flashBaseColor.r, flashBaseColor.g, flashBaseColor.b, 0f);
    }

    private void RaiseTransitionObjects()
    {
        if (image1 != null) image1.SetAsLastSibling();
        if (image2 != null) image2.SetAsLastSibling();
        if (image3 != null) image3.rectTransform.SetAsLastSibling();
        if (flashImage != null) flashImage.rectTransform.SetAsLastSibling();
    }
}
