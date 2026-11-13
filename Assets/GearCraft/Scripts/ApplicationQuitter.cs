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
        if (RunSaveUI.Instance != null && RunSaveUI.Instance.TryHandleEscape()) return;
        TitleLicenseController licenses = FindFirstObjectByType<TitleLicenseController>();
        if (licenses != null && licenses.TryCloseDialog()) return;
        PlayerDataResetController reset = FindFirstObjectByType<PlayerDataResetController>();
        if (reset != null && reset.TryCloseDialog()) return;
        StartingWeaponSelectionController selection = FindFirstObjectByType<StartingWeaponSelectionController>();
        if (selection != null && selection.IsOpen)
        {
            selection.Close();
            return;
        }
        foreach (UISwitcher switcher in FindObjectsByType<UISwitcher>(FindObjectsSortMode.None))
            if (switcher.TryCloseVisiblePanel()) return;
        QuitGame();
    }

    public static async void QuitGame()
    {
        if (RunSaveService.Instance != null)
        {
            await RunSaveService.Instance.PendingSave;
            if (!string.IsNullOrEmpty(RunSaveService.Instance.LastError) && RunSaveService.Instance.RunId != null)
            {
                RunSaveUI.Instance?.ConfirmUnsavedExit(QuitImmediately, null);
                return;
            }
        }
        QuitImmediately();
    }

    public static void QuitImmediately()
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
