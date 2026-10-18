using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class ButtonScaler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale;
    private Tween currentTween;

    [SerializeField] private float scaleFactor = 1.1f;  // 拡大倍率
    [SerializeField] private float duration = 0.2f;     // アニメーション時間

    private void Start()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        currentTween?.Kill(); // 途中のTweenがあれば止める
        currentTween = transform.DOScale(originalScale * scaleFactor, duration).SetEase(Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        currentTween?.Kill();
        currentTween = transform.DOScale(originalScale, duration).SetEase(Ease.OutBack);
    }
}
