using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class EndingManager : MonoBehaviour
{
    [Header("Ending Images")]
    [SerializeField] private GameObject endingBad;
    [SerializeField] private GameObject endingNormal;
    [SerializeField] private GameObject endingTrue;

    [System.Serializable]
    public class SequenceItem
    {
        public GameObject obj;
        [Tooltip("If true, when this item is shown the ImageSequenceViewer should hide other images.")]
        public bool hideOthersOnShow = false;
    }

    [Header("Ending Sequences (Optional)")]
    [Tooltip("If set, these sequences will be used by ImageSequenceViewer to show per-ending image sequences. Each entry can control whether other images are hidden when it is shown.")]
    [SerializeField] private SequenceItem[] endingBadSequence;
    [SerializeField] private SequenceItem[] endingNormalSequence;
    [SerializeField] private SequenceItem[] endingTrueSequence;

    [Header("Fade Settings")]
    [SerializeField] private Image fadePanel;      // ← 黒背景のImageをセット
    [SerializeField] private float fadeDuration = 2f;

    public static int PendingEndingNum { get; private set; }

    private void Awake()
    {
        // 全画像を非表示にする
        SetObjectActive(endingBad, false);
        SetObjectActive(endingNormal, false);
        SetObjectActive(endingTrue, false);

        // フェードパネルは最初は真っ黒
        if (fadePanel != null)
            SetAlpha(fadePanel, 1f);
    }

    private async void Start()
    {
        // すぐにエンディング画像のフェードを開始
        if (PendingEndingNum != 0)
        {
            ShowEndingAsync(PendingEndingNum).Forget();
        }

        // フェードパネルを消す
        if (fadePanel != null)
        {
            await fadePanel
                .DOFade(0f, fadeDuration)
                .SetEase(Ease.Linear)
                .AsyncWaitForCompletion();
        }
    }


    /// <summary>
    /// シーンを跨いでエンディングを再生する
    /// </summary>
    public static void LoadEndingScene(int endNum)
    {
        PendingEndingNum = endNum;
        SceneTransitionManager.LoadSceneWithTransition("Ending");
    }

    /// <summary>
    /// 現シーン内でEnding画像をフェード表示
    /// </summary>
    private async UniTask ShowEndingAsync(int endNum)
    {
        GameObject target = endNum switch
        {
            1 => endingBad,
            2 => endingNormal,
            3 => endingTrue,
            _ => null
        };

        if (!target) return;

        // 他の画像を消す
        SetObjectActive(endingBad, false);
        SetObjectActive(endingNormal, false);
        SetObjectActive(endingTrue, false);

        // ▼ エンディング画像フェードイン
        Image targetImage = target.GetComponent<Image>();
        if (targetImage != null)
        {
            target.SetActive(true);
            await targetImage
                .DOFade(1f, fadeDuration)
                .SetEase(Ease.Linear)
                .AsyncWaitForCompletion();
        }
        else
        {
            target.SetActive(true);
        }
    }

    /// <summary>
    /// 次のシーンに移行するときに呼ぶ：フェードアウトしてシーン移動
    /// </summary>
    public async UniTask FadeOutAndLoadScene(string sceneName)
    {
        // エンディング画像フェードアウト
        SetGameObjectAlpha(endingBad, 0f);
        SetGameObjectAlpha(endingNormal, 0f);
        SetGameObjectAlpha(endingTrue, 0f);

        await UniTask.Delay((int)(fadeDuration * 1000));

        // 画面全体フェードアウト
        if (fadePanel != null)
        {
            await fadePanel
                .DOFade(1f, fadeDuration)
                .SetEase(Ease.Linear)
                .AsyncWaitForCompletion();
        }

        SceneTransitionManager.LoadSceneWithTransition(sceneName);
    }

    private void SetAlpha(Image img, float alpha)
    {
        if (img == null) return;
        var c = img.color;
        c.a = alpha;
        img.color = c;
    }

    /// <summary>
    /// GameObject の active 状態を設定する。Image コンポーネントを持つ場合は Alpha も設定。
    /// </summary>
    private void SetObjectActive(GameObject go, bool active)
    {
        if (go == null) return;
        if (!active)
        {
            // 非表示時は Image の透明度も 0 にする
            Image img = go.GetComponent<Image>();
            if (img != null)
                SetAlpha(img, 0);
        }
        go.SetActive(active);
    }

    /// <summary>
    /// GameObject の Image コンポーネントの透明度を設定する（GameObject 用ヘルパー）。
    /// </summary>
    private void SetGameObjectAlpha(GameObject go, float alpha)
    {
        if (go == null) return;
        Image img = go.GetComponent<Image>();
        if (img != null)
            SetAlpha(img, alpha);
    }

    /// <summary>
    /// エンディング画像（endingBad, endingNormal, endingTrue）を全て透明にして見えなくする。
    /// ImageSequenceViewer がシーケンスを使用する場合に呼び出し。
    /// </summary>
    public void HideAllEndingImages()
    {
        SetObjectActive(endingBad, false);
        SetObjectActive(endingNormal, false);
        SetObjectActive(endingTrue, false);
    }

    /// <summary>
    /// エンディング番号に応じた GameObject シーケンスを返す（未設定なら null を返す）
    /// </summary>
    /// <summary>
    /// 指定したエンディングの GameObject 配列を返す（シーケンス未設定なら null）。
    /// </summary>
    public GameObject[] GetSequenceForEnding(int endNum)
    {
        SequenceItem[] seq = endNum switch
        {
            1 => endingBadSequence,
            2 => endingNormalSequence,
            3 => endingTrueSequence,
            _ => null
        };

        if (seq == null || seq.Length == 0) return null;
        GameObject[] objs = new GameObject[seq.Length];
        for (int i = 0; i < seq.Length; i++) objs[i] = seq[i]?.obj;
        return objs;
    }

    /// <summary>
    /// 指定したエンディングの各要素について、他の画像を消すかどうかのフラグ配列を返す。未設定なら null を返す。
    /// </summary>
    public bool[] GetHideFlagsForEnding(int endNum)
    {
        SequenceItem[] seq = endNum switch
        {
            1 => endingBadSequence,
            2 => endingNormalSequence,
            3 => endingTrueSequence,
            _ => null
        };

        if (seq == null || seq.Length == 0) return null;
        bool[] flags = new bool[seq.Length];
        for (int i = 0; i < seq.Length; i++) flags[i] = seq[i] != null && seq[i].hideOthersOnShow;
        return flags;
    }
}
