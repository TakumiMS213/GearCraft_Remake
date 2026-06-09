using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

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
    public StatusManager status;

    private CraftSpaceInteractionZone activeZone;

    private void Start()
    {
        status = StatusManager.Instance ?? FindAnyObjectByType<StatusManager>();
        uplimit = 1;

        if (fadeImage != null)
        {
            fadeImage.color = new Color(0, 0, 0, 0);
        }

        interactionZones = interactionZones != null && interactionZones.Length > 0
            ? interactionZones
            : FindObjectsByType<CraftSpaceInteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        HideAllPrompts();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ExecuteActiveAction();
        }
    }

    public void RegisterZone(CraftSpaceInteractionZone zone)
    {
        if (zone == null)
        {
            return;
        }

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
    }

    private void ExecuteActiveAction()
    {
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

    private void PlayCannotSelect()
    {
        cantSelect?.Play();
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
            PlayCannotSelect();
            return;
        }

        status.GATE += 50;
        transitionManager.PlayTransition(3);
        selectSound_statusUp.Play();
        ConsumeOneTimeBuff();
    }

    private void ApplyFreezeBonus()
    {
        if (!CanUseOneTimeBuff())
        {
            PlayCannotSelect();
            return;
        }

        int choice = UnityEngine.Random.Range(0, 2);
        if (choice == 0)
        {
            status.STR += 3;
            transitionManager.PlayTransition(0);
        }
        else
        {
            status.ACC += 3;
            transitionManager.PlayTransition(1);
        }

        selectSound_statusUp.Play();
        ConsumeOneTimeBuff();
    }

    private void HealPlayer()
    {
        if (status != null && status.HP >= 100)
        {
            PlayCannotSelect();
            return;
        }

        if (!CanUseOneTimeBuff())
        {
            PlayCannotSelect();
            return;
        }

        status.HP += 999;
        transitionManager.PlayTransition(2);
        selectSound_statusUp.Play();
        ConsumeOneTimeBuff();
    }

    private void LoadCraftScene()
    {
        selectSound_statusUp?.Play();
        SceneTransitionManager.LoadSceneWithTransition("Craft");
    }

    private void LoadMainScene()
    {
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
