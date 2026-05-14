using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

public class ImageSequenceViewer : MonoBehaviour
{
    [SerializeField] private GameObject[] images; // 表示する画像を順番に入れる
    private int currentIndex = -1;
    public SceneTransitionManager scenetransitionManager;
    public EndingManager endingManager;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 1f; // フェード時間（秒）

    private Tweener currentTween; // 現在実行中のトウィーン
    private bool isTransitioning = false; // シーン遷移中フラグ
    
    [Header("Display Options")]
    [Tooltip("If true, all other images will be hidden when showing a new image.")]
    [SerializeField] private bool hideOthersOnShow = false;
    
    // per-ending sequence flags retrieved from EndingManager (optional)
    private bool[] perImageHideFlags;
    public ScoreManager scoreManager;

    void Start()
    {
        if (scoreManager != null)
        {
            scoreManager.GetScore();
        }
        // EndingManager が指定され、PendingEndingNum に応じたシーケンスがあればそちらを使う
        if (endingManager != null && EndingManager.PendingEndingNum != 0)
        {
            var seq = endingManager.GetSequenceForEnding(EndingManager.PendingEndingNum);
            if (seq != null && seq.Length > 0)
            {
                images = seq;
                perImageHideFlags = endingManager.GetHideFlagsForEnding(EndingManager.PendingEndingNum);
                // EndingManager の画像を非表示にして、シーケンス画像だけを表示可能にする
                endingManager.HideAllEndingImages();
            }
        }

        // 全画像を非表示にしておく
        if (images != null)
        {
            foreach (var img in images)
            {
                if (img != null) img.SetActive(false);
            }
        }
    }

    void Update()
    {
        // 画面タップ or 左クリック
        if (Input.GetMouseButtonDown(0))
        {
            // フェード中なら即座に完了させて次へ
            if (currentTween != null && currentTween.IsActive())
            {
                currentTween.Complete();
                // Complete() の直後は既に ShowNextImage() のコールバックが実行されている可能性があるため
                // ここでは何もしない（コールバック内で処理される）
            }
            else
            {
                // フェード中でなければ次の画像を表示開始
                ShowNextImage();
            }
        }
    }

    private void ShowNextImage()
    {
        // 次へ
        currentIndex++;

        // 範囲外なら終了（ループさせたいならここを書き換える）
        if (currentIndex >= images.Length)
        {
            Debug.Log("最後まで表示しました");
            StatusManager.Instance.HP = 100;
            StatusManager.Instance.STR = 0;
            StatusManager.Instance.ACC = 0;
            StatusManager.Instance.GATE = 1000;
            StatusManager.Instance.selectWeapon = 0;
            StatusManager.Instance.module_scrap = false;
            StatusManager.Instance.module_repair = false;
            StatusManager.Instance.module_barrier = false;
            StatusManager.Instance.punkDrive = false;
            StatusManager.Instance.craftWeaponDamagebuff = 0;
            StatusManager.Instance.killAllEnemies = true;
            StatusManager.Instance.UseCraftSpacebuff = false;
            StageCounter.Instance.StageCount = 0;
            MaterialManager.Instance.ClearAllMaterials();
            if (!isTransitioning)
            {
                isTransitioning = true;
                scenetransitionManager.LoadScene("Title"); // シーン遷移
            }
            return;
        }

        // 次の画像をフェードインで表示
        var targetImage = images[currentIndex];
        if (targetImage == null) return;

        // 必要なら他の画像を消す（EndingManager のフラグがあればそれを優先）
        bool shouldHide = hideOthersOnShow;
        if (perImageHideFlags != null && currentIndex >= 0 && currentIndex < perImageHideFlags.Length)
        {
            shouldHide = perImageHideFlags[currentIndex];
        }
        if (shouldHide)
        {
            HideOtherImages(targetImage);
        }

        // Image コンポーネントを取得（または最初の Image 子要素）
        Image imgComponent = targetImage.GetComponent<Image>();
        if (imgComponent == null)
        {
            imgComponent = targetImage.GetComponentInChildren<Image>();
        }

        if (imgComponent == null)
        {
            Debug.LogWarning($"Image component not found on {targetImage.name}");
            targetImage.SetActive(true);
            return;
        }

        // 画像を表示状態に設定
        targetImage.SetActive(true);

        // 透明度を 0 に初期化
        var color = imgComponent.color;
        color.a = 0f;
        imgComponent.color = color;

        // 前のトウィーンがあればキャンセル
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
        }

        // フェードイン（alpha: 0 → 1）
        currentTween = imgComponent
            .DOFade(1f, fadeDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                // フェード完了時の処理（必要があれば）
                Debug.Log($"Fade completed for {targetImage.name}");
            });
    }

    /// <summary>
    /// 指定したオブジェクト以外の images 配列内のオブジェクトを非表示にする。
    /// 引数を省略すると全て非表示にする。
    /// </summary>
    public void HideOtherImages(GameObject except = null)
    {
        if (images == null) return;
        foreach (var go in images)
        {
            if (go == null) continue;
            if (except != null && go == except) continue;
            go.SetActive(false);
        }
    }

    /// <summary>
    /// images 配列の全ての画像を非表示にするユーティリティ。
    /// </summary>
    public void ClearAllImages()
    {
        if (images == null) return;
        foreach (var go in images)
        {
            if (go == null) continue;
            go.SetActive(false);
        }
    }
}
