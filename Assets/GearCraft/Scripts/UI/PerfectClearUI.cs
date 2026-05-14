using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// ゲートに敵が1体も到達しなかった場合に「PERFECT」表示。
/// フェードイン→スケールアニメ→フェードアウト。
/// </summary>
public class PerfectClearUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text perfectText;
    public CanvasGroup canvasGroup;       // フェード用

    [Header("アニメーション")]
    public float displayDuration = 2f;
    public float fadeInTime = 0.3f;
    public float fadeOutTime = 0.5f;
    public float scaleFrom = 0.5f;
    public float scaleTo = 1.2f;

    private void Awake()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        PlayAnimation();
    }

    private void PlayAnimation()
    {
        if (perfectText == null || canvasGroup == null) return;

        // リセット
        canvasGroup.alpha = 0f;
        perfectText.transform.localScale = Vector3.one * scaleFrom;
        perfectText.text = "PERFECT";
        perfectText.color = new Color(1f, 0.9f, 0.2f, 1f); // ゴールド

        Sequence seq = DOTween.Sequence();

        // フェードイン＋スケールアップ
        seq.Append(canvasGroup.DOFade(1f, fadeInTime));
        seq.Join(perfectText.transform.DOScale(scaleTo, fadeInTime).SetEase(Ease.OutBack));

        // 少し待つ
        seq.AppendInterval(displayDuration);

        // フェードアウト
        seq.Append(canvasGroup.DOFade(0f, fadeOutTime));
        seq.OnComplete(() => gameObject.SetActive(false));
    }
}
