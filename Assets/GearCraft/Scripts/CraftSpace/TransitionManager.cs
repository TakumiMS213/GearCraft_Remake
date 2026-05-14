using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TransitionManager : MonoBehaviour
{
    [SerializeField] private Image imageDisplay;        // 表示対象のImageコンポーネント
    [SerializeField] private Sprite[] imageSprites;     // 画像5種類
    [SerializeField] private RectTransform imageRect;   // imageDisplayのRectTransform

    [SerializeField] private Vector2 offScreenPos = new Vector2(1920f, 0f); // 右画面外の初期位置
    [SerializeField] private Vector2 centerPos = new Vector2(0f, 0f);       // 画面中央

    public static object Instance { get; internal set; }
    public AudioClip TransitionSound;
    private AudioSource audioSource;
    public bool isMain = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        // AudioSource初期設定
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    /// <summary>
    /// 指定インデックスの画像でトランジションを開始
    /// </summary>
    /// <param name="index">表示する画像のインデックス (0〜4)</param>
    public void PlayTransition(int index)
    {
        if (index < 0 || index >= imageSprites.Length)
        {
            Debug.LogError("画像インデックスが不正です");
            return;
        }

        // 初期状態をセット
        imageDisplay.sprite = imageSprites[index];
        imageDisplay.enabled = true;
        imageRect.anchoredPosition = offScreenPos;
        imageDisplay.color = new Color(1, 1, 1, 1); // α = 1 にして表示

        // 移動アニメーション
        if(isMain && (index == 0 || index == 2)){audioSource.PlayOneShot(TransitionSound);}
        imageRect.DOAnchorPos(centerPos, 0.5f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                // 1秒後に非表示処理
                DOVirtual.DelayedCall(1f, () =>
                {
                    // フェードアウト（0.3秒で透明に）
                    imageDisplay.DOFade(0f, 0.3f).OnComplete(() =>
                    {
                        imageDisplay.enabled = false;
                        //位置をリセット
                        imageRect.anchoredPosition = offScreenPos;
                        imageDisplay.color = new Color(1, 1, 1, 1); // α = 1 に戻す
                    });
                });
            });
    }
}
