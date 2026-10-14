using UnityEngine;

public class UIHotkeyController : MonoBehaviour
{
    public UISwitcher uiSwitcher;
    private SceneTransitionManager sceneTransitionManager;

    private void Start()
    {
        sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleMenu();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMenu();
        }
    }

    public void ToggleMenu()
    {
        if (IsInputBlocked()) return;
        uiSwitcher?.TogglePanel();
    }

    public void CloseMenu()
    {
        if (IsInputBlocked()) return;
        uiSwitcher?.HidePanel();
    }

    private bool IsInputBlocked()
    {
        if (sceneTransitionManager == null)
            sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();
        return ArrivalMessagePresenter.IsMessagePlaying || ArrivalMessagePresenter.IsCameraPresentationPlaying ||
            sceneTransitionManager != null && sceneTransitionManager.IsLoading;
    }
}
