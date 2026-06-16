using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using TMPro;
using System.Collections;
using GearCraft.Scripts.Data;

public class SceneTransitionManager : MonoBehaviour
{
    private static bool pendingEnterTransition;
    private static string pendingTip;

    [Header("Fade Settings")]
    public Image fadeImage;
    public Image LoadfadeImage;
    public float fadeDuration = 1f;

    [Header("Loading Settings")]
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private TMP_Text loadingTipText;
    [SerializeField] private LoadingTipData loadingTipData;
    [SerializeField] private float minLoadingSeconds = 1f;
    [SerializeField] private float maxLoadingSeconds = 2f;
    [SerializeField]
    private string[] loadingTips =
    {
        "Tip: 装備の相性を見直すと戦況が変わる。",
        "Tip: 休息前に素材を使い切る判断も大切。",
        "Tip: 歯車は次の一手を作るための余白。",
        "Tip: 危険な時ほどゲートの残りHPを確認しよう。"
    };

    public string sceneName = "CraftSpace";

    private bool isLoading;

    private void Awake()
    {
        ResolveFadeImages();

        if (pendingEnterTransition)
        {
            pendingEnterTransition = false;
            ShowLoadingVisuals(1f, pendingTip);
            FadeLoadingVisuals(0f, fadeDuration);
            return;
        }

        ShowLoadingVisuals(0f, null);
    }

    public void LoadScene()
    {
        LoadScene(sceneName);
    }

    public void LoadScene(string nextSceneName)
    {
        if (isLoading || string.IsNullOrWhiteSpace(nextSceneName))
        {
            return;
        }

        StartCoroutine(LoadSceneRoutine(nextSceneName));
    }

    public static void LoadSceneWithTransition(string nextSceneName)
    {
        SceneTransitionManager transitionManager = FindFirstObjectByType<SceneTransitionManager>();
        if (transitionManager != null)
        {
            transitionManager.LoadScene(nextSceneName);
            return;
        }

        pendingEnterTransition = true;
        pendingTip = null;
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator LoadSceneRoutine(string nextSceneName)
    {
        isLoading = true;

        ResolveFadeImages();
        string tip = SelectTip();
        ShowLoadingVisuals(0f, tip);
        FadeLoadingVisuals(1f, fadeDuration);

        if (fadeDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(fadeDuration);
        }

        float loadingSeconds = Random.Range(
            Mathf.Min(minLoadingSeconds, maxLoadingSeconds),
            Mathf.Max(minLoadingSeconds, maxLoadingSeconds));

        yield return new WaitForSecondsRealtime(loadingSeconds);

        pendingEnterTransition = true;
        pendingTip = tip;
        SceneManager.LoadScene(nextSceneName);
    }

    private void ResolveFadeImages()
    {
        if (fadeImage == null)
        {
            fadeImage = FindFadeImage("fade", "loadfade");
        }

        if (LoadfadeImage == null)
        {
            LoadfadeImage = FindFadeImage("loadfade", null);
        }

        if (loadingTipText == null)
        {
            loadingTipText = FindLoadingTipText();
        }

        if (loadingRoot == null)
        {
            loadingRoot = ResolveLoadingRoot();
        }
    }

    private void ShowLoadingVisuals(float alpha, string tip)
    {
        bool show = alpha > 0.01f;
        SetLoadingRootActive(show);
        SetImageAlpha(fadeImage, alpha);
        SetImageAlpha(LoadfadeImage, alpha);
        SetInputBlockerActive(show);

        if (loadingTipText != null)
        {
            if (!string.IsNullOrEmpty(tip))
            {
                loadingTipText.text = tip;
            }

            Color color = loadingTipText.color;
            color.a = alpha;
            loadingTipText.color = color;
        }
    }

    private void FadeLoadingVisuals(float alpha, float duration)
    {
        fadeImage?.DOKill();
        LoadfadeImage?.DOKill();
        loadingTipText?.DOKill();

        bool blocksInput = alpha > 0.01f;
        if (blocksInput)
        {
            SetLoadingRootActive(true);
            SetInputBlockerActive(true);
        }

        if (duration <= 0f)
        {
            ShowLoadingVisuals(alpha, null);
            return;
        }

        Tween fadeTween = fadeImage?.DOFade(alpha, duration).SetUpdate(true);
        LoadfadeImage?.DOFade(alpha, duration).SetUpdate(true);
        loadingTipText?.DOFade(alpha, duration).SetUpdate(true);

        if (!blocksInput)
        {
            if (fadeTween != null)
            {
                fadeTween.OnComplete(() =>
                {
                    SetInputBlockerActive(false);
                    SetLoadingRootActive(false);
                });
            }
            else
            {
                SetInputBlockerActive(false);
                SetLoadingRootActive(false);
            }
        }
    }

    private string SelectTip()
    {
        if (loadingTipData == null)
        {
            loadingTipData = Resources.Load<LoadingTipData>("LoadingTipData");
        }

        if (loadingTipData != null && loadingTipData.HasTips)
        {
            return loadingTipData.GetRandomTip();
        }

        if (loadingTips == null || loadingTips.Length == 0)
        {
            return string.Empty;
        }

        return loadingTips[Random.Range(0, loadingTips.Length)];
    }

    private void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void SetInputBlockerActive(bool active)
    {
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = active;
        }
    }

    private void SetLoadingRootActive(bool active)
    {
        if (loadingRoot == null)
        {
            return;
        }

        if (loadingRoot.activeSelf != active)
        {
            loadingRoot.SetActive(active);
        }
    }

    private GameObject ResolveLoadingRoot()
    {
        GameObject root = GameObject.Find("SceneTransitionCanvas");
        if (root != null)
        {
            return root;
        }

        if (fadeImage == null)
        {
            return null;
        }

        Canvas canvas = fadeImage.GetComponentInParent<Canvas>(true);
        if (canvas != null && canvas.name.Contains("SceneTransition"))
        {
            return canvas.gameObject;
        }

        return null;
    }

    private Image FindFadeImage(string requiredNamePart, string excludedNamePart)
    {
        Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var img in images)
        {
            string imageName = img.name.ToLowerInvariant();
            if (!imageName.Contains(requiredNamePart))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(excludedNamePart) && imageName.Contains(excludedNamePart))
            {
                continue;
            }

            return img;
        }

        return null;
    }

    private TMP_Text FindLoadingTipText()
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text text in texts)
        {
            if (text.name.ToLowerInvariant().Contains("loadingtip"))
            {
                return text;
            }
        }

        return null;
    }
}
