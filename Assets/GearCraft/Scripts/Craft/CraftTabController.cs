using GearCraft.Scripts.Craft;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftTabController : MonoBehaviour
{
    [Header("Tab Buttons")]
    public Button craftTabButton;
    public Button upgradeTabButton;

    [Header("Panels")]
    public GameObject craftPanel;
    public GameObject upgradePanel;

    [Header("Tab Colors")]
    public Color activeTabColor = new Color(0.9f, 0.75f, 0.3f, 1f);
    public Color inactiveTabColor = new Color(0.3f, 0.3f, 0.35f, 1f);

    [Header("Audio")]
    public AudioSource tabSwitchSE;

    [Header("References")]
    public MaterialDisplay materialDisplay;
    public UpgradeShopManager upgradeShopManager;
    [SerializeField] private CraftTutorialDataSO tutorialData;

    [Header("Close")]
    [SerializeField] private Button craftCloseButton;
    [SerializeField] private Button upgradeCloseButton;
    [SerializeField] private string closeSceneName = "CraftSpace";

    private int currentTab;
    private CraftTutorialController tutorialController;

    private void Start()
    {
        EnsureTutorialController();
        RegisterTabButtons();
        RegisterCloseButtons();

        SwitchTab(0);
        tutorialController?.OnCraftSceneEntered();
    }

    public void SwitchTab(int tabIndex)
    {
        currentTab = tabIndex;

        if (craftPanel != null)
        {
            craftPanel.SetActive(tabIndex == 0);
        }

        if (upgradePanel != null)
        {
            upgradePanel.SetActive(tabIndex == 1);
        }

        UpdateTabColors();

        if (tabSwitchSE != null)
        {
            tabSwitchSE.Play();
        }

        if (tabIndex == 1)
        {
            if (upgradeShopManager != null)
            {
                upgradeShopManager.EnsureLineupInitialized();
            }

            if (UpgradeGridManager.Instance != null)
            {
                UpgradeGridManager.Instance.RefreshGridSize();
            }
        }

        if (materialDisplay != null)
        {
            materialDisplay.UpdateMaterialAmount();
        }

        tutorialController?.OnTabOpened(tabIndex);
    }

    private void RegisterTabButtons()
    {
        if (craftTabButton != null)
        {
            craftTabButton.onClick.AddListener(() => SwitchTab(0));
        }

        if (upgradeTabButton != null)
        {
            upgradeTabButton.onClick.AddListener(() => SwitchTab(1));
        }
    }

    private void RegisterCloseButtons()
    {
        if (craftCloseButton == null)
        {
            craftCloseButton = FindChildButton(craftPanel, "Close");
        }

        if (upgradeCloseButton == null)
        {
            upgradeCloseButton = FindChildButton(upgradePanel, "Close");
        }

        RegisterCloseButton(craftCloseButton);
        RegisterCloseButton(upgradeCloseButton);
    }

    private void RegisterCloseButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(CloseCraftScene);
        button.onClick.AddListener(CloseCraftScene);
    }

    private void CloseCraftScene()
    {
        SceneTransitionManager.LoadSceneWithTransition(closeSceneName);
    }

    private static Button FindChildButton(GameObject root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && button.gameObject.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private void UpdateTabColors()
    {
        SetTabColor(craftTabButton, currentTab == 0);
        SetTabColor(upgradeTabButton, currentTab == 1);
    }

    private void SetTabColor(Button button, bool isActive)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = isActive ? activeTabColor : inactiveTabColor;
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.color = isActive ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
        }
    }

    private void EnsureTutorialController()
    {
        tutorialController = GetComponent<CraftTutorialController>();
        if (tutorialController == null)
        {
            tutorialController = gameObject.AddComponent<CraftTutorialController>();
        }

        tutorialController.Configure(tutorialData);
    }
}
