using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// ゲートHP低下時に画面端に赤いフラッシュ＋WARNINGテキストを表示
/// </summary>
public class GateWarningUI : MonoBehaviour
{
    [Header("UI")]
    public Image warningOverlay;          // 画面全体を覆う赤いImage（初期は非表示）
    public TMP_Text warningText;          // WARNING テキスト

    [Header("設定")]
    public float warningThreshold = 0.3f; // HP30%以下で警告
    public float flashSpeed = 1.5f;
    public float maxAlpha = 0.3f;         // 警告オーバーレイの最大透明度

    private bool isWarning = false;
    private Tween flashTween;

    void Update()
    {
        if (StatusManager.Instance == null) return;

        float hpRatio = StatusManager.Instance.GATE / 100f;

        if (hpRatio <= warningThreshold && !isWarning)
        {
            StartWarning();
        }
        else if (hpRatio > warningThreshold && isWarning)
        {
            StopWarning();
        }
    }

    private void StartWarning()
    {
        isWarning = true;

        if (warningOverlay != null)
        {
            warningOverlay.gameObject.SetActive(true);
            warningOverlay.color = new Color(1f, 0f, 0f, 0f);
            flashTween = warningOverlay.DOFade(maxAlpha, flashSpeed)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        if (warningText != null)
        {
            warningText.gameObject.SetActive(true);
            warningText.text = "⚠ WARNING ⚠";
            warningText.color = new Color(1f, 0.2f, 0.2f, 1f);
            warningText.DOFade(0.3f, flashSpeed * 0.5f)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }

    private void StopWarning()
    {
        isWarning = false;
        flashTween?.Kill();

        if (warningOverlay != null)
        {
            warningOverlay.DOKill();
            warningOverlay.gameObject.SetActive(false);
        }
        if (warningText != null)
        {
            warningText.DOKill();
            warningText.gameObject.SetActive(false);
        }
    }
}
