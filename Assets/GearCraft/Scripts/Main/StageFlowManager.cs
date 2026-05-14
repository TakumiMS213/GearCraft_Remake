using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class StageFlowManager : MonoBehaviour
{
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

    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private int currentStage = 1;
    private int stagesSinceRest;
    private bool gateWasHit;

    public bool IsStageActive { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        currentStage = StageCounter.Instance != null ? StageCounter.Instance.StageCount : 1;
        stagesSinceRest = 0;
        gateWasHit = false;
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

        if (StatusManager.Instance != null)
        {
            StatusManager.Instance.TryDurabilityDrain();
        }

        if (wasLastEnemy)
        {
            OnStageComplete().Forget();
        }
    }

    public void OnGateHit()
    {
        gateWasHit = true;
        if (StatusManager.Instance != null)
        {
            StatusManager.Instance.killAllEnemies = false;
        }
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

        if (StatusManager.Instance != null)
        {
            bool weaponBroken = StatusManager.Instance.ReduceDurability();
            if (weaponBroken)
            {
                Debug.Log("Weapon was broken. Switching to default weapon.");
            }
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

        if (bonusCardsManager != null)
        {
            bonusCardsManager.ShowBonusCards();
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
            await UniTask.Delay(TimeSpan.FromSeconds(1f));

            if (transitionManager != null)
            {
                transitionManager.PlayTransition(2);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(1f));

            if (StatusManager.Instance != null && StatusManager.Instance.module_scrap)
            {
                MaterialManager.Instance?.AddMaterial(MaterialManager.MaterialType.Gear, 1);
            }

            sceneTransitionManager?.LoadScene("CraftSpace");
            return;
        }

        gateWasHit = false;
        await UniTask.Delay(TimeSpan.FromSeconds(1f));
        StartNextStage();
    }

    private void StartNextStage()
    {
        IsStageActive = true;
        if (enemySpawner != null)
        {
            enemySpawner.StartStage(currentStage);
        }
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
