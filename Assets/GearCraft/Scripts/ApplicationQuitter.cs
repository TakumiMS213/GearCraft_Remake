using UnityEngine;

// タイトルの終了操作。プレイ中のEscは各画面の閉じる操作に任せる。
public class ApplicationQuitter : MonoBehaviour
{
    readonly KeyCode quitKey = KeyCode.Escape;

    void Update()
    {
        if (Input.GetKeyDown(quitKey))
        {
            HandleEscape();
        }
    }

    private void HandleEscape()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Title") return;
        foreach (UISwitcher switcher in FindObjectsByType<UISwitcher>(FindObjectsSortMode.None))
            if (switcher.TryCloseVisiblePanel()) return;
        QuitGame();
    }

    public static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
        Application.Quit();
#endif
    }

    // ゲームの開始時にインスタンスを作成します
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateInstanceOnLoad()
    {
        var instance = new GameObject(nameof(ApplicationQuitter), typeof(ApplicationQuitter));
        DontDestroyOnLoad(instance);
    }
}
