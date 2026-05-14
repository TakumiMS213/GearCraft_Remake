using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject pauseMenuPanel;
    public Button resumeButton;
    public Button retryButton;
    public Button inventoryButton;
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
        SceneManager.LoadScene("Main");
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
            return;
        }

        InventoryUI inventoryUI = inventoryPanel.GetComponent<InventoryUI>();
        if (inventoryUI != null)
        {
            inventoryUI.Refresh();
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
