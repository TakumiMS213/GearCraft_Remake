using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class StartingWeaponSelectionController : MonoBehaviour
{
    [System.Serializable]
    public sealed class Choice
    {
        public WeaponDataSO weapon;
        public WeaponDataSO additionalWeapon;
        public string displayName;
        [TextArea] public string description;
        public Button button;
        public GameObject selectionFrame;
        public GameObject selectedLabel;
    }

    [SerializeField] private GameObject window;
    [SerializeField] private CanvasGroup titleControls;
    [SerializeField] private Button startButton;
    [SerializeField] private Button deployButton;
    [SerializeField] private TMP_Text selectedName;
    [SerializeField] private TMP_Text selectedDescription;
    [SerializeField] private TMP_Text unlockNote;
    [SerializeField] private StorySceneLoader storyLoader;
    [SerializeField] private SceneTransitionManager sceneTransition;
    [SerializeField] private Choice[] choices;
    private int selectedIndex = -1;
    private bool departing;
    private bool wasTitleInteractable;
    private bool wasTitleBlocking;
    public bool IsOpen => window != null && window.activeSelf;

    private void Awake()
    {
        window.SetActive(false);
        for (int i = 0; i < choices.Length; i++)
        {
            int index = i;
            choices[i].button.onClick.AddListener(() => SelectWeapon(index));
        }
        RefreshSelection();
    }

    public void RequestStart()
    {
        if (departing || IsOpen || (sceneTransition != null && sceneTransition.IsLoading)) return;
        if (RunSaveUI.Instance != null) RunSaveUI.Instance.RequestStart(this);
        else StartNewGame();
    }

    public void StartNewGame()
    {
        if (departing || IsOpen || (sceneTransition != null && sceneTransition.IsLoading)) return;
        if (!StartingWeaponProgress.IsSelectionEnabled)
        {
            Depart(StatusManager.Instance != null ? StatusManager.Instance.defaultWeapon : null, null);
            return;
        }

        selectedIndex = -1;
        if (unlockNote != null)
            unlockNote.text = StartingWeaponProgress.IsUnlocked ? "AR-227 撃破報酬" : "SETTINGSで開始武器選択がONになっています";
        RefreshSelection();
        if (titleControls != null)
        {
            wasTitleInteractable = titleControls.interactable;
            wasTitleBlocking = titleControls.blocksRaycasts;
            titleControls.interactable = titleControls.blocksRaycasts = false;
        }
        window.SetActive(true);
        if (choices.Length > 0) choices[0].button.Select();
    }

    public void SelectWeapon(int index)
    {
        if (!IsOpen || departing || index < 0 || index >= choices.Length || choices[index].weapon == null) return;
        selectedIndex = index;
        RefreshSelection();
    }

    public void Confirm()
    {
        if (!IsOpen || departing || !StartingWeaponProgress.IsSelectionEnabled ||
            selectedIndex < 0 || selectedIndex >= choices.Length) return;
        Choice choice = choices[selectedIndex];
        Depart(choice.weapon, choice.additionalWeapon);
    }

    public void Close()
    {
        if (!IsOpen || departing) return;
        window.SetActive(false);
        RestoreTitleControls();
        selectedIndex = -1;
        RefreshSelection();
        if (startButton != null) startButton.Select();
    }

    private void RefreshSelection()
    {
        bool selected = selectedIndex >= 0 && selectedIndex < choices.Length;
        deployButton.interactable = selected && !departing;
        selectedName.text = selected ? choices[selectedIndex].displayName : "武器を選択してください";
        selectedDescription.text = selected ? choices[selectedIndex].description : "ブロックをクリックして選択できます。";
        for (int i = 0; i < choices.Length; i++)
        {
            choices[i].selectionFrame.SetActive(i == selectedIndex);
            choices[i].selectedLabel.SetActive(i == selectedIndex);
        }
    }

    private async void Depart(WeaponDataSO weapon, WeaponDataSO additionalWeapon)
    {
        if (weapon == null || StatusManager.Instance == null || storyLoader == null) return;
        departing = true;
        deployButton.interactable = false;
        if (RunSaveService.Instance != null && !await RunSaveService.Instance.BeginRunAsync())
        {
            departing = false;
            RefreshSelection();
            return;
        }
        UpgradeGridManager.Instance?.DiscardRuntimeState();
        // Preserve permanent unlocks, but always begin with a clean run.
        StatusManager status = StatusManager.Instance;
        status.ResetRunProgress();
        status.ownedWeapons.Clear();
        status.AcquireWeapon(weapon);
        if (additionalWeapon != null) status.AcquireWeapon(additionalWeapon);
        status.EquipWeapon(weapon);
        status.selectWeapon = 0;
        MaterialManager.Instance?.ClearAllMaterials();
        if (StageCounter.Instance != null) StageCounter.Instance.StageCount = 1;
        StoryPlaybackRequest.ClearRequest();
        Time.timeScale = 1f;
        storyLoader.LoadStory();
    }

    private void RestoreTitleControls()
    {
        if (titleControls == null) return;
        titleControls.interactable = wasTitleInteractable;
        titleControls.blocksRaycasts = wasTitleBlocking;
    }

    private void OnDisable()
    {
        if (IsOpen)
        {
            window.SetActive(false);
            RestoreTitleControls();
        }
    }
}
