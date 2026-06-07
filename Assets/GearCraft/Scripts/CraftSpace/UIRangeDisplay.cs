using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class UIRangeDisplay : MonoBehaviour
{
    [System.Serializable]
    public class UIRange
    {
        public GameObject uiObject;   // 表示切り替え対象のUI（画像など）
        public float minX;            // 表示開始X座標
        public float maxX;            // 表示終了X座標
    }

    public Transform player;          // プレイヤーのTransform
    public UIRange[] uiRanges;        // UIごとのX範囲設定
    [SerializeField] private CraftSpaceInteractionZone[] interactionZones;

    public TransitionManager transitionManager;  // ← これを追加

    public int uplimit;
    public AudioSource selectSound_statusUp;
    public AudioSource cantSelect;

    [SerializeField] private Image fadeImage; // 黒いImage (最初は Alpha=0 にしておくこと)
    public StatusManager status; // ステータスマネージャーの参照
    private CraftSpaceInteractionZone activeZone;

    void Start()
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

    void Update()
    {
        if (transitionManager == null) Debug.LogWarning("transitionManagerが未設定です");
        if (selectSound_statusUp == null) Debug.LogWarning("selectSound_statusUpが未設定です");
        if (cantSelect == null) Debug.LogWarning("cantSelectが未設定です");
        if (fadeImage == null) Debug.LogWarning("fadeImageが未設定です");

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
        selectSound_statusUp.Play();
        fadeImage
            .DOFade(0.5f, 0.5f)
            .SetUpdate(true)
            .OnComplete(() => SceneManager.LoadScene("Craft"));
    }

    private void LoadMainScene()
    {
        selectSound_statusUp.Play();
        fadeImage
            .DOFade(1f, 2f)
            .SetUpdate(true)
            .OnComplete(() => SceneManager.LoadScene("Main"));
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
