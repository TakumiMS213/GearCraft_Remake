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
        if (fadeImage == null)
        {
            fadeImage = FindFadeImage();
            LoadfadeImage = FindFadeImage();
        }
        if (fadeImage != null)
            fadeImage.color = new Color(0, 0, 0, 0);
            LoadfadeImage.color = new Color(1f, 1f, 1f, 0f);

        // シーンロード完了時にフェードイン
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // シーン切り替え後にfadeImageを再取得（新シーンでImageが変わる場合に備える）
        if (fadeImage == null)
        {
            fadeImage = FindFadeImage();
            LoadfadeImage = FindFadeImage();
        }
        if (fadeImage != null)
        {
            // まず真っ黒にしてからフェードイン
            fadeImage.color = new Color(0, 0, 0, 1);
            LoadfadeImage.color = new Color(1f, 1f, 1f, 1f);
            fadeImage.DOFade(0f, fadeDuration);
            LoadfadeImage.DOFade(0f, fadeDuration);
        }
    }

    /// <summary>
    /// フェードアウトしてシーンをロード
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (fadeImage == null)
        {
            fadeImage = FindFadeImage();
            LoadfadeImage = FindFadeImage();
        }
        if (fadeImage != null)
        {
            // まず透明にしてからフェードアウト
            fadeImage.color = new Color(0, 0, 0, 0);
            LoadfadeImage.color = new Color(1f, 1f, 1f, 0f);
            LoadfadeImage.DOFade(1f, fadeDuration);
            fadeImage.DOFade(1f, fadeDuration).OnComplete(() =>
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

    // シーン内のFade用Imageを探す
    private Image FindFadeImage()
    {
        // Canvas内のImageで"Fade"という名前のものを優先
        Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var img in images)
        {
            if (img.name.ToLower().Contains("fade")) return img;
        }
        // なければ最初のImageを返す
        return images.Length > 0 ? images[0] : null;
    }
}
