using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Button inventoryCloseButton;
    public GameObject inventoryPanel;

    private bool isPaused;

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
        isPaused = false;
        Time.timeScale = 1f;
        HideTitleReturnConfirm();

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    public void RetryDay()
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
        if (titleReturnConfirmPanel == null)
        {
            return;
        }

        titleReturnConfirmPanel.SetActive(true);
        titleReturnConfirmPanel.transform.SetAsLastSibling();
    }

    private void HideTitleReturnConfirm()
    {
        if (titleReturnConfirmPanel != null)
        {
            titleReturnConfirmPanel.SetActive(false);
        }
    }

    private void ConfirmReturnToTitle()
    {
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
