using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StageFlowManager : MonoBehaviour
{
    private const string OpeningNoticeSeenKey = "GearCraft.Main.OpeningNotice.Seen.v1";

    public static StageFlowManager Instance { get; private set; }

    [Header("References")]
    public EnemySpawner enemySpawner;
    public BonusCardsManager bonusCardsManager;
    public TransitionManager transitionManager;
    public SceneTransitionManager sceneTransitionManager;
    public StageGeneratorSO stageConfig;

    [Header("UI")]
    public TextMeshProUGUI stageNumText;
    public GameObject perfectUI;
    public GameObject gateWarningUI;
    public RestResultReportUI restResultReportUI;

    [Header("Opening Notice")]
    [SerializeField] private GameObject openingNoticeRoot;
    [SerializeField] private RectTransform openingNoticePaper;
    [SerializeField] private Button openingNoticeButton;
    [SerializeField] private Vector2 openingNoticeHiddenPosition = new Vector2(0f, 980f);
    [SerializeField] private Vector2 openingNoticeShownPosition = Vector2.zero;
    [SerializeField] private float openingNoticeDuration = 0.65f;
    [SerializeField] private float dayDisplaySeconds = 1.2f;

    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private readonly Dictionary<MaterialManager.MaterialType, int> restMaterialGains = new Dictionary<MaterialManager.MaterialType, int>();
    private int currentStage = 1;
    private int stagesSinceRest;
    private int restDefeatedEnemies;
    private bool gateWasHit;
    private bool stageLastEnemyKilled;
    private bool stageCompletionTriggered;

    public bool IsStageActive { get; private set; }
    public int EnemyScalingStage { get; private set; } = 1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            EnemyScalingStage = GetStageFromCounter();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private async void Start()
    {
        currentStage = GetStageFromCounter();
        EnemyScalingStage = currentStage;
        stagesSinceRest = 0;
        gateWasHit = false;
        ResetRestResultStats();

        if (perfectUI != null)
        {
            perfectUI.SetActive(false);
        }

        if (openingNoticeRoot != null && openingNoticePaper != null && !HasSeenOpeningNotice())
        {
            await PlayOpeningNoticeAsync();
        }
        else if (openingNoticeRoot != null)
        {
            openingNoticeRoot.SetActive(false);
        }

        await ShowDayAsync();
        StartNextStage();
    }

    private int GetStageFromCounter()
    {
        return StageCounter.Instance != null ? Mathf.Max(1, StageCounter.Instance.StageCount) : 1;
    }

    public void RegisterEnemy(EnemyController enemy)
    {
        if (enemy != null && !activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
        }
    }

    public void OnEnemyKilled(EnemyController enemy, bool wasLastEnemy)
    {
        activeEnemies.Remove(enemy);
        restDefeatedEnemies++;

        if (wasLastEnemy)
        {
            stageLastEnemyKilled = true;
        }

        TryCompleteStage();
    }

    public void OnGateHit()
    {
        gateWasHit = true;
        if (StatusManager.Instance != null)
        {
            StatusManager.Instance.killAllEnemies = false;
        }
    }

    public void RecordMaterialGained(MaterialManager.MaterialType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (!restMaterialGains.ContainsKey(type))
        {
            restMaterialGains[type] = 0;
        }

        restMaterialGains[type] += amount;
    }

    private async UniTaskVoid OnStageComplete()
    {
        IsStageActive = false;
        ClearBullets();

        if (!gateWasHit)
        {
            ShowPerfect();
        }

        bool completedBossStage = stageConfig != null && stageConfig.IsBossStage(currentStage);
        if (completedBossStage && StatusManager.Instance != null)
        {
            int bossIndex = stageConfig.GetBossIndex(currentStage);
            int unlockedBossCount = bossIndex >= 0 ? bossIndex + 1 : StatusManager.Instance.bossKillCount + 1;
            StatusManager.Instance.bossKillCount = Mathf.Min(Mathf.Max(StatusManager.Instance.bossKillCount, unlockedBossCount), 4);
        }

        if (transitionManager != null)
        {
            transitionManager.PlayTransition(0);
        }

        await UniTask.Delay(TimeSpan.FromSeconds(2f));

        if (StatusManager.Instance != null && StatusManager.Instance.module_repair)
        {
            StatusManager.Instance.HP += 20;
        }

        if (stageConfig != null && currentStage >= stageConfig.totalStages)
        {
            HandleGameClear();
            return;
        }

        bool willReturnToRest = stageConfig != null &&
            stageConfig.stagesPerRest > 0 &&
            stagesSinceRest + 1 >= stageConfig.stagesPerRest;

        if (!willReturnToRest && bonusCardsManager != null)
        {
            await bonusCardsManager.ShowBonusCardsAsync();
        }

        currentStage++;
        stagesSinceRest++;
        if (StageCounter.Instance != null)
        {
            StageCounter.Instance.StageCount = currentStage;
        }

        if (stageConfig != null && stageConfig.stagesPerRest > 0 && stagesSinceRest >= stageConfig.stagesPerRest)
        {
            stagesSinceRest = 0;

            if (StatusManager.Instance != null && StatusManager.Instance.module_scrap)
            {
                MaterialManager.Instance?.AddMaterial(MaterialManager.MaterialType.Gear, 1);
            }

            await ShowRestResultReportAsync();

            if (transitionManager != null)
            {
                transitionManager.PlayTransition(2);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(1f));
            sceneTransitionManager?.LoadScene("CraftSpace");
            return;
        }

        gateWasHit = false;
        await UniTask.Delay(TimeSpan.FromSeconds(1f));
        StartNextStage();
    }

    private async UniTask ShowRestResultReportAsync()
    {
        if (restResultReportUI == null)
        {
            restResultReportUI = FindFirstObjectByType<RestResultReportUI>();
        }

        if (restResultReportUI == null)
        {
            GameObject reportObject = new GameObject("RestResultReportUI");
            restResultReportUI = reportObject.AddComponent<RestResultReportUI>();
        }

        await restResultReportUI.ShowAsync(restDefeatedEnemies, restMaterialGains, this.GetCancellationTokenOnDestroy());
        ResetRestResultStats();
    }

    private void ResetRestResultStats()
    {
        restDefeatedEnemies = 0;
        restMaterialGains.Clear();
    }

    private void StartNextStage()
    {
        activeEnemies.RemoveAll(enemy => enemy == null);
        stageLastEnemyKilled = false;
        stageCompletionTriggered = false;
        IsStageActive = true;
        if (enemySpawner != null)
        {
            enemySpawner.StartStage(currentStage);
        }
    }

    private void TryCompleteStage()
    {
        activeEnemies.RemoveAll(enemy => enemy == null);

        if (stageCompletionTriggered || !stageLastEnemyKilled || activeEnemies.Count > 0)
        {
            return;
        }

        stageCompletionTriggered = true;
        enemySpawner?.StopSpawning();
        OnStageComplete().Forget();
    }

    private async UniTask PlayOpeningNoticeAsync()
    {
        bool clicked = false;
        openingNoticeRoot.SetActive(true);
        openingNoticePaper.anchoredPosition = openingNoticeHiddenPosition;

        if (openingNoticeButton != null)
        {
            openingNoticeButton.onClick.RemoveAllListeners();
            openingNoticeButton.onClick.AddListener(() => clicked = true);
        }

        openingNoticePaper
            .DOAnchorPos(openingNoticeShownPosition, openingNoticeDuration)
            .SetEase(Ease.InCirc);

        await UniTask.Delay(TimeSpan.FromSeconds(openingNoticeDuration));
        await UniTask.WaitUntil(() => clicked || Input.GetMouseButtonDown(0), cancellationToken: this.GetCancellationTokenOnDestroy());

        openingNoticePaper
            .DOAnchorPos(openingNoticeHiddenPosition, openingNoticeDuration)
            .SetEase(Ease.InCirc);

        await UniTask.Delay(TimeSpan.FromSeconds(openingNoticeDuration));
        openingNoticeRoot.SetActive(false);
        MarkOpeningNoticeSeen();
    }

    private static bool HasSeenOpeningNotice()
    {
        return PlayerPrefs.GetInt(OpeningNoticeSeenKey, 0) == 1;
    }

    private static void MarkOpeningNoticeSeen()
    {
        PlayerPrefs.SetInt(OpeningNoticeSeenKey, 1);
        PlayerPrefs.Save();
    }

    private async UniTask ShowDayAsync()
    {
        if (stageNumText == null)
        {
            return;
        }

        int day = ((Mathf.Max(1, currentStage) - 1) / 3) + 1;
        stageNumText.text = $"Day{day}";
        stageNumText.alpha = 1f;
        await UniTask.Delay(TimeSpan.FromSeconds(dayDisplaySeconds));
        stageNumText.text = "";
    }

    private async void HandleGameClear()
    {
        if (transitionManager != null)
        {
            transitionManager.PlayTransition(3);
        }

        await UniTask.Delay(TimeSpan.FromSeconds(2f));

        bool trueRoute = StatusManager.Instance != null && StatusManager.Instance.killAllEnemies;
        EndingManager.LoadEndingScene(trueRoute ? 3 : 2);
    }

    private async void ShowPerfect()
    {
        if (perfectUI == null)
        {
            return;
        }

        perfectUI.SetActive(true);
        await UniTask.Delay(TimeSpan.FromSeconds(2f));
        if (perfectUI != null)
        {
            perfectUI.SetActive(false);
        }
    }

    private static void ClearBullets()
    {
        GameObject[] bullets = GameObject.FindGameObjectsWithTag("Bullet");
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null)
            {
                Destroy(bullets[i]);
            }
        }
    }
}
