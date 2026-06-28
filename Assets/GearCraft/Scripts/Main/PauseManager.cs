using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject pauseMenuPanel;
    public Button resumeButton;
    public Button retryButton;
    public Button inventoryButton;
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

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;

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

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
