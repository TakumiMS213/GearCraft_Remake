using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI is authored in the scene prefab; this component only controls its state.
[DefaultExecutionOrder(-500)]
public sealed class QuitConfirmationController : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject overlay;
    [SerializeField] private RectTransform window;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    private static QuitConfirmationController active;
    private static int consumedFrame = -1;
    private bool isOpen;
    private float previousTimeScale;
    private GameObject previousSelection;
    private bool awaitingSave;

    public static bool BlocksPlayerInput => active != null && active.isOpen || consumedFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { active = null; consumedFrame = -1; }

    private void Awake()
    {
        overlay.SetActive(false);
        yesButton.onClick.AddListener(ConfirmQuit);
        noButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        if (RunSaveUI.BlocksGameplay) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (isOpen) Close();
        else if (CanOpen()) Open();
    }

    private bool CanOpen()
    {
        if (BlocksPlayerInput || Time.timeScale <= 0f || EndingManager.IsLoadingEnding ||
            GearCraft.Gallery.GalleryNavigation.IsOpen || GearCraft.Gallery.GalleryNavigation.IsBusy ||
            ArrivalMessagePresenter.IsMessagePlaying || ArrivalMessagePresenter.IsCameraPresentationPlaying)
            return false;
        foreach (var transition in FindObjectsByType<SceneTransitionManager>(FindObjectsSortMode.None))
            if (transition.IsLoading) return false;
        foreach (var switcher in FindObjectsByType<UISwitcher>(FindObjectsSortMode.None))
            if (switcher.HasVisiblePanel) return false;

        string scene = SceneManager.GetActiveScene().name;
        if (scene == "Main")
            return StageFlowManager.Instance != null && !StageFlowManager.IsPlayerInputBlocked &&
                StatusManager.Instance != null && StatusManager.Instance.HP > 0;
        return scene == "CraftSpace" && FindFirstObjectByType<PlayerMovement>() != null;
    }

    private void Open()
    {
        consumedFrame = Time.frameCount;
        previousTimeScale = Time.timeScale;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        active = this;
        isOpen = true;
        Time.timeScale = 0f;
        overlay.SetActive(true);
        noButton.Select();
    }

    public void Close()
    {
        if (!isOpen) return;
        consumedFrame = Time.frameCount;
        isOpen = false;
        active = null;
        overlay.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy
                ? previousSelection : null);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isOpen && eventData.button == PointerEventData.InputButton.Left &&
            !RectTransformUtility.RectangleContainsScreenPoint(window, eventData.position, eventData.pressEventCamera))
            Close();
    }

    private async void ConfirmQuit()
    {
        if (!isOpen || awaitingSave) return;
        awaitingSave = true;
        if (RunSaveService.Instance != null) await RunSaveService.Instance.PendingSave;
        awaitingSave = false;
        if (isOpen) ApplicationQuitter.QuitGame();
    }

    private void OnDisable() { Close(); }
}
