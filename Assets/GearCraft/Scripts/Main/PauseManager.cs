using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PauseManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject pauseMenuPanel;
    public Button resumeButton;
    public Button retryButton;
    public Button inventoryButton;
    [SerializeField] private Button titleReturnButton;
    [SerializeField] private GameObject titleReturnConfirmPanel;
    [SerializeField] private Button titleReturnYesButton;
    [SerializeField] private Button titleReturnNoButton;
    [SerializeField] private TMP_Text actionConfirmText;
    [SerializeField] private Button inventoryCloseButton;
    public GameObject inventoryPanel;

    private bool isPaused;
    private float previousTimeScale = 1f;
    public bool IsPaused => isPaused;
    private bool retryConfirmationPending;
    private bool leavingScene;

    private void Start()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(Resume);
        }

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(RetryDay);
        }

        if (inventoryButton != null)
        {
            inventoryButton.onClick.AddListener(ToggleInventory);
        }

        ResolveTitleReturnReferences();
        RegisterTitleReturnButtons();
        RegisterInventoryCloseButton();
    }

    private void Update()
    {
        if (StageFlowManager.Instance != null && StageFlowManager.Instance.IsBlockingGameplayUI) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Back();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Pause()
    {
        if (StageFlowManager.Instance != null && StageFlowManager.Instance.IsBlockingGameplayUI) return;
        if (!isPaused) previousTimeScale = Time.timeScale;
        isPaused = true;
        Time.timeScale = 0f;
        HideTitleReturnConfirm();

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
    }

    public void Resume()
    {
        StageFlowManager.BlockPlayerInputForCurrentFrame();
        isPaused = false;
        Time.timeScale = previousTimeScale;
        HideTitleReturnConfirm();
        FindFirstObjectByType<GearCraft.Scripts.Main.MainOptionManualController>()?.HideManual();

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    public void Back()
    {
        if (StageFlowManager.Instance != null && StageFlowManager.Instance.IsBlockingGameplayUI) return;
        if (titleReturnConfirmPanel != null && titleReturnConfirmPanel.activeInHierarchy)
        {
            HideTitleReturnConfirm();
            return;
        }
        var manual = FindFirstObjectByType<GearCraft.Scripts.Main.MainOptionManualController>();
        if (manual != null && manual.IsManualOpen) { manual.HideManual(); return; }
        if (inventoryPanel != null && inventoryPanel.activeInHierarchy) { CloseInventory(); return; }
        if (isPaused) Resume();
        else Pause();
    }

    public void RetryDay()
    {
        if (leavingScene) return;
        retryConfirmationPending = true;
        ShowActionConfirmation("この日をやり直しますか？\n装備と素材は、この日の開始時点に戻ります。", "この日をやり直す");
    }

    private void ConfirmRetryDay()
    {
        Time.timeScale = 1f;
        isPaused = false;
        StageFlowManager.Instance?.RestoreDayStartSnapshot();
        SceneTransitionManager.LoadSceneWithTransition("Main");
    }

    public void ToggleInventory()
    {
        if (inventoryPanel == null)
        {
            return;
        }

        bool show = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(show);

        if (!show)
        {
            if (isPaused && pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(true);
            }

            return;
        }

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        HideTitleReturnConfirm();

        InventoryUI inventoryUI = inventoryPanel.GetComponent<InventoryUI>();
        if (inventoryUI != null)
        {
            inventoryUI.Refresh();
        }
    }

    public void CloseInventory()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        if (isPaused && pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
            if (inventoryButton != null) inventoryButton.Select();
        }
    }

    private void RegisterInventoryCloseButton()
    {
        if (inventoryCloseButton == null && inventoryPanel != null)
        {
            Transform closeTransform = inventoryPanel.transform.Find("Close");
            if (closeTransform != null)
            {
                inventoryCloseButton = closeTransform.GetComponent<Button>();
            }
        }

        if (inventoryCloseButton == null)
        {
            return;
        }

        inventoryCloseButton.onClick.RemoveListener(CloseInventory);
        inventoryCloseButton.onClick.AddListener(CloseInventory);
    }

    private void ResolveTitleReturnReferences()
    {
        if (titleReturnButton == null && pauseMenuPanel != null)
        {
            titleReturnButton = FindChildButton(pauseMenuPanel.transform, "TitleReturnButton");
        }

        if (titleReturnConfirmPanel == null && pauseMenuPanel != null)
        {
            Transform panelTransform = FindChildTransform(pauseMenuPanel.transform, "TitleReturnConfirmPanel");
            titleReturnConfirmPanel = panelTransform != null ? panelTransform.gameObject : null;
        }

        if (titleReturnYesButton == null && titleReturnConfirmPanel != null)
        {
            titleReturnYesButton = FindChildButton(titleReturnConfirmPanel.transform, "YesButton");
        }

        if (titleReturnNoButton == null && titleReturnConfirmPanel != null)
        {
            titleReturnNoButton = FindChildButton(titleReturnConfirmPanel.transform, "NoButton");
        }
    }

    private void RegisterTitleReturnButtons()
    {
        if (titleReturnConfirmPanel != null)
        {
            titleReturnConfirmPanel.SetActive(false);
        }

        if (titleReturnButton != null)
        {
            titleReturnButton.onClick.RemoveListener(ShowTitleReturnConfirm);
            titleReturnButton.onClick.AddListener(ShowTitleReturnConfirm);
        }

        if (titleReturnYesButton != null)
        {
            titleReturnYesButton.onClick.RemoveListener(ConfirmReturnToTitle);
            titleReturnYesButton.onClick.AddListener(ConfirmReturnToTitle);
        }

        if (titleReturnNoButton != null)
        {
            titleReturnNoButton.onClick.RemoveListener(HideTitleReturnConfirm);
            titleReturnNoButton.onClick.AddListener(HideTitleReturnConfirm);
        }
    }

    private void ShowTitleReturnConfirm()
    {
        if (leavingScene) return;
        retryConfirmationPending = false;
        ShowActionConfirmation("タイトルへ戻りますか？\n今回の防衛の進行・装備・素材は失われます。", "タイトルへ戻る");
    }

    private void ShowActionConfirmation(string message, string confirmLabel)
    {
        if (titleReturnConfirmPanel == null)
        {
            return;
        }

        titleReturnConfirmPanel.SetActive(true);
        titleReturnConfirmPanel.transform.SetAsLastSibling();
        if (actionConfirmText != null) actionConfirmText.text = message;
        if (titleReturnYesButton != null)
            titleReturnYesButton.GetComponentInChildren<TMP_Text>().text = confirmLabel;
        if (titleReturnNoButton != null)
        {
            titleReturnNoButton.GetComponentInChildren<TMP_Text>().text = "やめる";
            titleReturnNoButton.Select();
        }
    }

    private void HideTitleReturnConfirm()
    {
        retryConfirmationPending = false;
        if (titleReturnConfirmPanel != null)
        {
            titleReturnConfirmPanel.SetActive(false);
        }
    }

    private void ConfirmReturnToTitle()
    {
        if (leavingScene) return;
        leavingScene = true;
        if (retryConfirmationPending)
        {
            ConfirmRetryDay();
            return;
        }
        Time.timeScale = 1f;
        isPaused = false;
        ResetRunProgress();
        SceneTransitionManager.LoadSceneWithTransition("Title");
    }

    private static void ResetRunProgress()
    {
        StoryPlaybackRequest.ClearRequest();

        if (StageCounter.Instance != null)
        {
            StageCounter.Instance.StageCount = 1;
        }

        StatusManager.Instance?.ResetRunProgress();
        MaterialManager.Instance?.ClearAllMaterials();
    }

    private static Button FindChildButton(Transform parent, string childName)
    {
        Transform child = FindChildTransform(parent, childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static Transform FindChildTransform(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
