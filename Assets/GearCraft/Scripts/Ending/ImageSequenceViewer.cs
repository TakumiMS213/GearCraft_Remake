using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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

    [Header("Gallery Unlock Notice")]
    [SerializeField] private GameObject galleryUnlockNotice;
    private bool showingGalleryUnlockNotice;
    private float noticeReadyTime;
    private int lastAdvanceFrame = -1;

    [Header("Advance Controls")]
    [SerializeField] private Button advanceButton;
    [SerializeField] private Button galleryNoticeButton;
    [SerializeField] private TMP_Text advanceHint;
    [SerializeField] private TMP_Text galleryNoticeHint;

    private bool IsInputBlocked => isTransitioning ||
        (scenetransitionManager != null && scenetransitionManager.IsLoading);

    void Start()
    {
        if (scenetransitionManager == null)
            scenetransitionManager = FindFirstObjectByType<SceneTransitionManager>();
        ConfigureAdvanceButton(advanceButton);
        ConfigureAdvanceButton(galleryNoticeButton);
        if (galleryUnlockNotice != null) galleryUnlockNotice.SetActive(false);
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
        RefreshAdvanceControls();
    }

    void Update()
    {
        RefreshAdvanceControls();
    }

    public void Advance()
    {
        if (!isActiveAndEnabled || IsInputBlocked || lastAdvanceFrame == Time.frameCount) return;
        if (showingGalleryUnlockNotice && Time.unscaledTime < noticeReadyTime) return;
        lastAdvanceFrame = Time.frameCount;

        if (showingGalleryUnlockNotice)
        {
            ReturnToTitle();
        }
        else if (currentTween != null && currentTween.IsActive())
        {
            // Complete the current fade only; the next input advances the sequence.
            currentTween.Complete();
            currentTween = null;
        }
        else
        {
            ShowNextImage();
        }
        RefreshAdvanceControls();
    }

    private void ConfigureAdvanceButton(Button button)
    {
        if (button == null) return;
        button.onClick.RemoveListener(Advance);
        button.onClick.AddListener(Advance);
        button.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = button,
            selectOnDown = button,
            selectOnLeft = button,
            selectOnRight = button,
        };
    }

    private void RefreshAdvanceControls()
    {
        bool blocked = IsInputBlocked;
        if (advanceButton != null)
        {
            bool visible = !showingGalleryUnlockNotice;
            if (advanceButton.gameObject.activeSelf != visible)
                advanceButton.gameObject.SetActive(visible);
            advanceButton.interactable = visible && !blocked;
        }
        if (galleryNoticeButton != null)
            galleryNoticeButton.interactable = showingGalleryUnlockNotice && !blocked && Time.unscaledTime >= noticeReadyTime;

        if (advanceHint != null)
        {
            bool finalImage = images != null && images.Length > 0 && currentIndex == images.Length - 1;
            bool pendingNotice = galleryUnlockNotice != null && endingManager != null && endingManager.HasPendingGalleryUnlockNotice;
            bool fading = currentTween != null && currentTween.IsActive();
            advanceHint.text = fading ? "クリック / Enterで表示を完了"
                : !finalImage ? "クリック / Enterで次へ"
                : pendingNotice ? "クリック / Enterで続ける" : "クリック / Enterでタイトルへ";
        }
        if (galleryNoticeHint != null)
            galleryNoticeHint.text = "クリック / Enterでタイトルへ";

        Button activeButton = showingGalleryUnlockNotice ? galleryNoticeButton : advanceButton;
        if (!blocked && activeButton != null && activeButton.gameObject.activeInHierarchy && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != activeButton.gameObject)
            EventSystem.current.SetSelectedGameObject(activeButton.gameObject);
    }

    private void OnDestroy()
    {
        currentTween?.Kill();
        currentTween = null;
        if (advanceButton != null) advanceButton.onClick.RemoveListener(Advance);
        if (galleryNoticeButton != null) galleryNoticeButton.onClick.RemoveListener(Advance);
        if (EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (advanceButton != null && selected == advanceButton.gameObject ||
                galleryNoticeButton != null && selected == galleryNoticeButton.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void ShowNextImage()
    {
        if (IsInputBlocked || showingGalleryUnlockNotice) return;
        // 次へ
        currentIndex++;

        // 範囲外なら終了（ループさせたいならここを書き換える）
        if (images == null || currentIndex >= images.Length)
        {
            if (galleryUnlockNotice != null && endingManager != null && endingManager.ConsumeGalleryUnlockNotice())
            {
                ClearAllImages();
                endingManager.HideAllEndingImages();
                galleryUnlockNotice.SetActive(true);
                showingGalleryUnlockNotice = true;
                noticeReadyTime = Time.unscaledTime + 0.5f;
                RefreshAdvanceControls();
                return;
            }
            ReturnToTitle();
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

    private void ReturnToTitle()
    {
        if (IsInputBlocked) return;
        isTransitioning = true;
        RefreshAdvanceControls();
        // A new run starts from the same state as returning through the pause menu.
        // Gallery discoveries and tutorial mastery are stored separately and retained.
        StoryPlaybackRequest.ClearRequest();
        StatusManager.Instance?.ResetRunProgress();
        if (StageCounter.Instance != null) StageCounter.Instance.StageCount = 1;
        if (MaterialManager.Instance != null) MaterialManager.Instance.ClearAllMaterials();
        if (scenetransitionManager != null) scenetransitionManager.LoadScene("Title");
        else SceneTransitionManager.LoadSceneWithTransition("Title");
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
