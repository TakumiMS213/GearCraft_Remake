using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIRangeDisplay : MonoBehaviour
{
    [System.Serializable]
    public class UIRange
    {
        public GameObject uiObject;
        public float minX;
        public float maxX;
    }

    public Transform player;
    public UIRange[] uiRanges;
    [SerializeField] private CraftSpaceInteractionZone[] interactionZones;

    public TransitionManager transitionManager;

    public int uplimit;
    public AudioSource selectSound_statusUp;
    public AudioSource cantSelect;

    [SerializeField] private Image fadeImage;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject feedbackRoot;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField, Min(0f)] private float feedbackSeconds = 4f;
    public StatusManager status;

    private CraftSpaceInteractionZone activeZone;
    private SceneTransitionManager sceneTransitionManager;
    private bool menuWasOpen;
    private float feedbackUntil;
    private float feedbackPlayerX;
    private bool showingEquipmentGuide;

    private void Start()
    {
        status = StatusManager.Instance ?? FindAnyObjectByType<StatusManager>();
        sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();
        menuWasOpen = menuPanel != null && menuPanel.activeInHierarchy;
        uplimit = 1;

        if (fadeImage != null)
        {
            fadeImage.color = new Color(0, 0, 0, 0);
        }

        interactionZones = interactionZones != null && interactionZones.Length > 0
            ? interactionZones
            : FindObjectsByType<CraftSpaceInteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        HideAllPrompts();
        HideFeedback();
    }

    private void Update()
    {
        if (IsInputBlocked())
        {
            HideFeedback();
            return;
        }

        if (feedbackRoot != null && feedbackRoot.activeSelf && !showingEquipmentGuide &&
            (Time.unscaledTime >= feedbackUntil || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f ||
             player != null && Mathf.Abs(player.position.x - feedbackPlayerX) > 0.02f))
            HideFeedback();

        ShowEquipmentGuideIfNeeded();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ExecuteActiveAction();
        }
    }

    private void LateUpdate()
    {
        // UI buttons can open the menu after this component's Update.
        if (IsInputBlocked()) HideFeedback();
        menuWasOpen = menuPanel != null && menuPanel.activeInHierarchy;
    }

    private void OnDisable()
    {
        HideFeedback();
    }

    private bool IsInputBlocked()
    {
        if (sceneTransitionManager == null)
            sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();
        return Time.timeScale <= 0f || menuWasOpen ||
            menuPanel != null && menuPanel.activeInHierarchy ||
            Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape) ||
            ArrivalMessagePresenter.IsMessagePlaying || ArrivalMessagePresenter.IsCameraPresentationPlaying ||
            sceneTransitionManager != null && sceneTransitionManager.IsLoading;
    }

    public void RegisterZone(CraftSpaceInteractionZone zone)
    {
        if (zone == null)
        {
            return;
        }

        if (activeZone != zone) HideFeedback();
        activeZone?.SetPromptActive(false);
        activeZone = zone;
        activeZone.SetPromptActive(true);
    }

    public void UnregisterZone(CraftSpaceInteractionZone zone)
    {
        if (activeZone != zone)
        {
            return;
        }

        activeZone.SetPromptActive(false);
        activeZone = null;
        HideFeedback();
    }

    private void ExecuteActiveAction()
    {
        if (IsInputBlocked())
        {
            HideFeedback();
            return;
        }
        if (activeZone == null)
        {
            return;
        }

        switch (activeZone.ActionType)
        {
            case CraftSpaceInteractionAction.CraftTable:
                LoadCraftScene();
                break;
            case CraftSpaceInteractionAction.Bed:
                LoadMainScene();
                break;
            case CraftSpaceInteractionAction.GateRepair:
                RepairGate();
                break;
            case CraftSpaceInteractionAction.Freeze:
                ApplyFreezeBonus();
                break;
            case CraftSpaceInteractionAction.Medical:
                HealPlayer();
                break;
        }
    }

    private bool CanUseOneTimeBuff()
    {
        return uplimit >= 1 && status != null && status.UseCraftSpacebuff == false;
    }

    private void PlayCannotSelect(string reason)
    {
        cantSelect?.Play();
        ShowFeedback(reason);
    }

    private string GetUnavailableReason()
    {
        return status == null
            ? "設備を利用できません。一度宿舎に入り直してください。"
            : "今日は設備を利用済みです。\n次の防衛を終えると、また1つ選べます。";
    }

    private void ShowFeedback(string message)
    {
        if (feedbackRoot == null || feedbackText == null) return;
        showingEquipmentGuide = false;
        feedbackText.text = message;
        feedbackText.raycastTarget = false;
        feedbackUntil = Time.unscaledTime + feedbackSeconds;
        feedbackPlayerX = player != null ? player.position.x : 0f;
        feedbackRoot.SetActive(true);
    }

    private void HideFeedback()
    {
        showingEquipmentGuide = false;
        if (feedbackRoot != null) feedbackRoot.SetActive(false);
    }

    private void ShowEquipmentGuideIfNeeded()
    {
        if (activeZone == null || feedbackRoot == null || feedbackRoot.activeSelf ||
            feedbackText == null || !CanUseOneTimeBuff()) return;

        string guide;
        switch (activeZone.ActionType)
        {
            case CraftSpaceInteractionAction.GateRepair:
                guide = "門の耐久力を50増やせます。\n設備は一晩に一つです。修理するなら Space を押してください。";
                break;
            case CraftSpaceInteractionAction.Freeze:
                guide = "食事で近接か遠距離の攻撃力が上がります。\n設備は一晩に一つです。食事を取るなら Space を押してください。";
                break;
            case CraftSpaceInteractionAction.Medical:
                guide = status.HP >= 100
                    ? "体力は十分に回復しています。\n今夜は、食事か門の修理を選べます。"
                    : "体力を全回復できます。\n設備は一晩に一つです。手当てを受けるなら Space を押してください。";
                break;
            default: return;
        }
        ShowFeedback(guide);
        // Keep decision support visible until leaving the equipment or acting.
        showingEquipmentGuide = true;
    }

    private void ConsumeOneTimeBuff()
    {
        uplimit -= 1;
        status.UseCraftSpacebuff = true;
    }

    private void RepairGate()
    {
        if (!CanUseOneTimeBuff())
        {
            PlayCannotSelect(GetUnavailableReason());
            return;
        }

        float before = status.GATE;
        status.GATE += 50;
        ShowFeedback($"門を修理しました。\n門の耐久力 +50（{before:0.##} → {status.GATE:0.##}）");
        selectSound_statusUp?.Play();
        ConsumeOneTimeBuff();
    }

    private void ApplyFreezeBonus()
    {
        if (!CanUseOneTimeBuff())
        {
            PlayCannotSelect(GetUnavailableReason());
            return;
        }

        HideFeedback();
        int choice = UnityEngine.Random.Range(0, 2);
        if (choice == 0)
        {
            status.STR += 3;
            transitionManager?.PlayTransition(0);
        }
        else
        {
            status.ACC += 3;
            transitionManager?.PlayTransition(1);
        }

        selectSound_statusUp?.Play();
        ConsumeOneTimeBuff();
    }

    private void HealPlayer()
    {
        if (!CanUseOneTimeBuff())
        {
            PlayCannotSelect(GetUnavailableReason());
            return;
        }

        if (status.HP >= 100)
        {
            PlayCannotSelect("体力は十分に回復しています。\n食事か門の修理を選べます。");
            return;
        }

        HideFeedback();
        status.HP = 100;
        transitionManager?.PlayTransition(2);
        selectSound_statusUp?.Play();
        ConsumeOneTimeBuff();
    }

    private void LoadCraftScene()
    {
        HideFeedback();
        selectSound_statusUp?.Play();
        SceneTransitionManager.LoadSceneWithTransition("Craft");
    }

    private void LoadMainScene()
    {
        HideFeedback();
        selectSound_statusUp?.Play();
        SceneTransitionManager.LoadSceneWithTransition("Main");
    }

    private void HideAllPrompts()
    {
        if (uiRanges != null)
        {
            foreach (UIRange range in uiRanges)
            {
                if (range.uiObject != null)
                {
                    range.uiObject.SetActive(false);
                }
            }
        }

        if (interactionZones == null)
        {
            return;
        }

        foreach (CraftSpaceInteractionZone zone in interactionZones)
        {
            zone?.SetPromptActive(false);
        }
    }
}
