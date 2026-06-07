using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SceneTransitionManager : MonoBehaviour
{

    [Header("Fade Settings")]
    public Image fadeImage;      // Canvas上の黒いImage
    public Image LoadfadeImage;
    public float fadeDuration = 1f;

    public string sceneName = "CraftSpace"; // ロードするシーン名(デフォルト)

    private void Awake()
    {
        // 初期透明度0（ただしImageがnullの場合は探す）
        ResolveFadeImages();
        if (fadeImage != null)
        {
            fadeImage.color = new Color(0, 0, 0, 0);
        }

        if (LoadfadeImage != null)
        {
            LoadfadeImage.color = new Color(1f, 1f, 1f, 0f);
        }

        // シーンロード完了時にフェードイン
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // シーン切り替え後にfadeImageを再取得（新シーンでImageが変わる場合に備える）
        ResolveFadeImages();
        if (fadeImage != null)
        {
            // まず真っ黒にしてからフェードイン
            fadeImage.color = new Color(0, 0, 0, 1);
            fadeImage.DOFade(0f, fadeDuration).SetUpdate(true);
        }

        if (LoadfadeImage != null)
        {
            LoadfadeImage.color = new Color(1f, 1f, 1f, 1f);
            LoadfadeImage.DOFade(0f, fadeDuration).SetUpdate(true);
        }
    }

    /// <summary>
    /// フェードアウトしてシーンをロード
    /// </summary>
    public void LoadScene(string sceneName)
    {
        ResolveFadeImages();
        if (fadeImage != null)
        {
            // まず透明にしてからフェードアウト
            fadeImage.color = new Color(0, 0, 0, 0);
            if (LoadfadeImage != null)
            {
                LoadfadeImage.color = new Color(1f, 1f, 1f, 0f);
                LoadfadeImage.DOFade(1f, fadeDuration).SetUpdate(true);
            }

            fadeImage
                .DOFade(1f, fadeDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    SceneManager.LoadScene(sceneName);
                });
        }
        else
        {
            // フェードできない場合は即ロード
            SceneManager.LoadScene(sceneName);
        }
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
    }

    // シーン内のFade用Imageを探す
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
}
