using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class CraftTransitionManager : MonoBehaviour
{
    [Header("Transition Images")]
    public RectTransform image1; // 左側
    public RectTransform image2; // 右側
    public Image image3;         // 中央完成画像
    public Image flashImage;     // 白く光るエフェクト用（画面全体 or Image3上に白Imageを配置）
    
    [Header("Settings")]
    public float moveDuration = 0.5f;
    public float delayBeforeImage3 = 0.0f; // シームレスにするので0推奨
    public float image3DisplayTime = 1.5f;
    public float image3FadeOutTime = 0.4f;
    public Vector2 centerPos = Vector2.zero;
    
    [Header("Shake Settings")]
    public float shakeDistance = 5f;  // 上下移動距離
    public float shakeDuration = 0.1f; // 各方向への移動時間
    
    private Vector3 image1StartPos;
    private Vector3 image2StartPos;
    private Vector3 image3StartPos; // Image3の初期位置を保存
    private Color flashBaseColor;

    public Button craftButton;
    
    private void Awake()
    {
        image1StartPos = image1.anchoredPosition;
        image2StartPos = image2.anchoredPosition;
        image3StartPos = image3.rectTransform.anchoredPosition; // Image3の初期位置保存
        image3.gameObject.SetActive(false);
        
        if (flashImage != null)
        {
            flashBaseColor = flashImage.color;
            flashImage.color = new Color(flashBaseColor.r, flashBaseColor.g, flashBaseColor.b, 0f); // 透明
        }
    }
    
    public void StartTransition()
    {
        RaiseTransitionObjects();

        // 初期化
        image1.anchoredPosition = image1StartPos;
        image2.anchoredPosition = image2StartPos;
        image1.gameObject.SetActive(true);
        image2.gameObject.SetActive(true);
        image3.gameObject.SetActive(false);
        
        // Image3の位置も初期化
        image3.rectTransform.anchoredPosition = image3StartPos;
        
        if (flashImage != null)
            flashImage.color = new Color(flashBaseColor.r, flashBaseColor.g, flashBaseColor.b, 0f);
        
        Sequence seq = DOTween.Sequence();
        
        // 1. 左右から中央へ移動
        seq.Append(image1.DOAnchorPos(centerPos, moveDuration).SetEase(Ease.OutCubic));
        seq.Join(image2.DOAnchorPos(centerPos, moveDuration).SetEase(Ease.OutCubic));
        
        // 2. 合体直後に白く光らせてImage3表示
        seq.AppendCallback(() =>
        {
            image1.gameObject.SetActive(false);
            image2.gameObject.SetActive(false);
            image3.gameObject.SetActive(true);
            
            if (flashImage != null)
            {
                flashImage.color = new Color(1f, 1f, 1f, 0f);
                flashImage.DOFade(1f, 0.1f).SetEase(Ease.OutQuad)  // パッと光る
                    .OnComplete(() =>
                    {
                        flashImage.DOFade(0f, 0.3f).SetEase(Ease.InQuad); // すぐ消える
                    });
            }
        });
        
        // 3. Image3の上下揺れアニメーション
        // 上に移動
        seq.Append(image3.rectTransform.DOAnchorPosY(image3StartPos.y + shakeDistance, shakeDuration).SetEase(Ease.OutQuad));
        // 下に移動
        seq.Append(image3.rectTransform.DOAnchorPosY(image3StartPos.y - shakeDistance, shakeDuration).SetEase(Ease.OutQuad));
        // 中央に戻る
        seq.Append(image3.rectTransform.DOAnchorPosY(image3StartPos.y, shakeDuration).SetEase(Ease.OutQuad));
        
        // 4. 表示時間（揺れが終わった後の静止時間）
        seq.AppendInterval(image3DisplayTime);
        
        // 5. フェードアウト
        seq.Append(image3.DOFade(0f, image3FadeOutTime).SetEase(Ease.InQuad));
        seq.AppendCallback(() =>
        {
            image3.gameObject.SetActive(false);
            image3.color = new Color(image3.color.r, image3.color.g, image3.color.b, 1f); // α戻す
        });
        
        craftButton.gameObject.SetActive(true);
        seq.OnComplete(() => Debug.Log("Transition Complete"));
    }

    private void RaiseTransitionObjects()
    {
        if (image1 != null)
        {
            image1.SetAsLastSibling();
        }

        if (image2 != null)
        {
            image2.SetAsLastSibling();
        }

        if (image3 != null)
        {
            image3.rectTransform.SetAsLastSibling();
        }

        if (flashImage != null)
        {
            flashImage.rectTransform.SetAsLastSibling();
        }
    }
}
