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
    private DayStartSnapshot dayStartSnapshot;

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
        CaptureDayStartSnapshot();

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
                MaterialManager.Instance?.AddMaterial(MaterialManager.MaterialType.Gear, 5);
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

    public void RestoreDayStartSnapshot()
    {
        if (dayStartSnapshot == null)
        {
            return;
        }

        dayStartSnapshot.Restore();
        ResetRestResultStats();
        activeEnemies.Clear();
        IsStageActive = false;
        stageLastEnemyKilled = false;
        stageCompletionTriggered = false;
        gateWasHit = false;
    }

    private void CaptureDayStartSnapshot()
    {
        dayStartSnapshot = DayStartSnapshot.Capture();
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

    private sealed class DayStartSnapshot
    {
        private int stageCount;
        private int hp;
        private int san;
        private int str;
        private int acc;
        private float gate;
        private WeaponDataSO currentWeapon;
        private List<WeaponDataSO> ownedWeapons;
        private List<UpgradePartSO> ownedUpgradeParts;
        private List<StatusManager.SavedUpgradePartPlacement> savedUpgradePlacements;
        private int selectWeapon;
        private bool moduleScrap;
        private bool moduleRepair;
        private bool moduleBarrier;
        private bool punkDrive;
        private int craftWeaponDamageBuff;
        private bool killAllEnemies;
        private bool useCraftSpaceBuff;
        private int bossKillCount;
        private int upgradeShopDay;
        private float bonusDamage;
        private float attackSpeedMult;
        private bool hasBulletDouble;
        private float spreadModifier;
        private int ricochetCount;
        private float junkCollectorMult;
        private float bulletSizeMult;
        private float magnetRange;
        private List<string> selectedUniqueBonusCardIds;
        private float gearCraftAxeSizeMultiplier;
        private float gearCraftSwordMoveSpeedBonus;
        private int gearCraftTransformGearGain;
        private float steamCannonBulletSpeedMultiplier;
        private float steamCannonExplosionRadiusMultiplier;
        private float steamCannonGiantBulletChance;
        private float steamCannonDirectHitKnockback;
        private float steamThrowerOverheatSlipDamage;
        private bool steamThrowerNoBulletGravity;
        private float steamThrowerBoostDamageMultiplier;
        private bool steamThrowerBoostBurnDrops;
        private float railCraftBulletSizeMultiplier;
        private bool railCraftApplyOverheat;
        private float railCraftRicochetMultiplier;
        private float steamGatlingBulletSpeedMultiplier;
        private float steamGatlingBossDamageMultiplier;
        private float steamGatlingNormalDamageMultiplier;
        private bool steamGatlingDownwardRecoil;
        private int scrap;
        private int gear;
        private int upgradeCore;
        private int moduleCoreLv1;
        private int moduleCoreLv2;
        private int moduleCoreLv3;

        public static DayStartSnapshot Capture()
        {
            DayStartSnapshot snapshot = new DayStartSnapshot();
            snapshot.stageCount = StageCounter.Instance != null ? StageCounter.Instance.StageCount : 1;

            StatusManager status = StatusManager.Instance;
            if (status != null)
            {
                snapshot.hp = status.HP;
                snapshot.san = status.SAN;
                snapshot.str = status.STR;
                snapshot.acc = status.ACC;
                snapshot.gate = status.GATE;
                snapshot.currentWeapon = status.currentWeapon;
                snapshot.ownedWeapons = status.ownedWeapons != null
                    ? new List<WeaponDataSO>(status.ownedWeapons)
                    : new List<WeaponDataSO>();
                snapshot.ownedUpgradeParts = status.ownedUpgradeParts != null
                    ? new List<UpgradePartSO>(status.ownedUpgradeParts)
                    : new List<UpgradePartSO>();
                snapshot.savedUpgradePlacements = ClonePlacements(status.savedUpgradePartPlacements);
                snapshot.selectWeapon = status.selectWeapon;
                snapshot.moduleScrap = status.module_scrap;
                snapshot.moduleRepair = status.module_repair;
                snapshot.moduleBarrier = status.module_barrier;
                snapshot.punkDrive = status.punkDrive;
                snapshot.craftWeaponDamageBuff = status.craftWeaponDamagebuff;
                snapshot.killAllEnemies = status.killAllEnemies;
                snapshot.useCraftSpaceBuff = status.UseCraftSpacebuff;
                snapshot.bossKillCount = status.bossKillCount;
                snapshot.upgradeShopDay = status.upgradeShopDay;
                snapshot.bonusDamage = status.bonusDamage;
                snapshot.attackSpeedMult = status.attackSpeedMult;
                snapshot.hasBulletDouble = status.hasBulletDouble;
                snapshot.spreadModifier = status.spreadModifier;
                snapshot.ricochetCount = status.ricochetCount;
                snapshot.junkCollectorMult = status.junkCollectorMult;
                snapshot.bulletSizeMult = status.bulletSizeMult;
                snapshot.magnetRange = status.magnetRange;
                snapshot.selectedUniqueBonusCardIds = status.selectedUniqueBonusCardIds != null
                    ? new List<string>(status.selectedUniqueBonusCardIds)
                    : new List<string>();
                snapshot.gearCraftAxeSizeMultiplier = status.gearCraftAxeSizeMultiplier;
                snapshot.gearCraftSwordMoveSpeedBonus = status.gearCraftSwordMoveSpeedBonus;
                snapshot.gearCraftTransformGearGain = status.gearCraftTransformGearGain;
                snapshot.steamCannonBulletSpeedMultiplier = status.steamCannonBulletSpeedMultiplier;
                snapshot.steamCannonExplosionRadiusMultiplier = status.steamCannonExplosionRadiusMultiplier;
                snapshot.steamCannonGiantBulletChance = status.steamCannonGiantBulletChance;
                snapshot.steamCannonDirectHitKnockback = status.steamCannonDirectHitKnockback;
                snapshot.steamThrowerOverheatSlipDamage = status.steamThrowerOverheatSlipDamage;
                snapshot.steamThrowerNoBulletGravity = status.steamThrowerNoBulletGravity;
                snapshot.steamThrowerBoostDamageMultiplier = status.steamThrowerBoostDamageMultiplier;
                snapshot.steamThrowerBoostBurnDrops = status.steamThrowerBoostBurnDrops;
                snapshot.railCraftBulletSizeMultiplier = status.railCraftBulletSizeMultiplier;
                snapshot.railCraftApplyOverheat = status.railCraftApplyOverheat;
                snapshot.railCraftRicochetMultiplier = status.railCraftRicochetMultiplier;
                snapshot.steamGatlingBulletSpeedMultiplier = status.steamGatlingBulletSpeedMultiplier;
                snapshot.steamGatlingBossDamageMultiplier = status.steamGatlingBossDamageMultiplier;
                snapshot.steamGatlingNormalDamageMultiplier = status.steamGatlingNormalDamageMultiplier;
                snapshot.steamGatlingDownwardRecoil = status.steamGatlingDownwardRecoil;
            }

            MaterialManager material = MaterialManager.Instance;
            if (material != null)
            {
                snapshot.scrap = material.scrap;
                snapshot.gear = material.gear;
                snapshot.upgradeCore = material.upgradeCore;
                snapshot.moduleCoreLv1 = material.moduleCore_lv1;
                snapshot.moduleCoreLv2 = material.moduleCore_lv2;
                snapshot.moduleCoreLv3 = material.moduleCore_lv3;
            }

            return snapshot;
        }

        public void Restore()
        {
            if (StageCounter.Instance != null)
            {
                StageCounter.Instance.StageCount = Mathf.Max(1, stageCount);
            }

            StatusManager status = StatusManager.Instance;
            if (status != null)
            {
                status.HP = hp;
                status.SAN = san;
                status.STR = str;
                status.ACC = acc;
                status.GATE = gate;
                status.currentWeapon = currentWeapon;
                status.ownedWeapons = ownedWeapons != null ? new List<WeaponDataSO>(ownedWeapons) : new List<WeaponDataSO>();
                status.ownedUpgradeParts = ownedUpgradeParts != null ? new List<UpgradePartSO>(ownedUpgradeParts) : new List<UpgradePartSO>();
                status.savedUpgradePartPlacements = ClonePlacements(savedUpgradePlacements);
                status.selectWeapon = selectWeapon;
                status.module_scrap = moduleScrap;
                status.module_repair = moduleRepair;
                status.module_barrier = moduleBarrier;
                status.punkDrive = punkDrive;
                status.craftWeaponDamagebuff = craftWeaponDamageBuff;
                status.killAllEnemies = killAllEnemies;
                status.UseCraftSpacebuff = useCraftSpaceBuff;
                status.bossKillCount = bossKillCount;
                status.upgradeShopDay = upgradeShopDay;
                status.bonusDamage = bonusDamage;
                status.attackSpeedMult = attackSpeedMult;
                status.hasBulletDouble = hasBulletDouble;
                status.spreadModifier = spreadModifier;
                status.ricochetCount = ricochetCount;
                status.junkCollectorMult = junkCollectorMult;
                status.bulletSizeMult = bulletSizeMult;
                status.magnetRange = magnetRange;
                status.selectedUniqueBonusCardIds = selectedUniqueBonusCardIds != null
                    ? new List<string>(selectedUniqueBonusCardIds)
                    : new List<string>();
                status.gearCraftAxeSizeMultiplier = gearCraftAxeSizeMultiplier;
                status.gearCraftSwordMoveSpeedBonus = gearCraftSwordMoveSpeedBonus;
                status.gearCraftTransformGearGain = gearCraftTransformGearGain;
                status.steamCannonBulletSpeedMultiplier = steamCannonBulletSpeedMultiplier;
                status.steamCannonExplosionRadiusMultiplier = steamCannonExplosionRadiusMultiplier;
                status.steamCannonGiantBulletChance = steamCannonGiantBulletChance;
                status.steamCannonDirectHitKnockback = steamCannonDirectHitKnockback;
                status.steamThrowerOverheatSlipDamage = steamThrowerOverheatSlipDamage;
                status.steamThrowerNoBulletGravity = steamThrowerNoBulletGravity;
                status.steamThrowerBoostDamageMultiplier = steamThrowerBoostDamageMultiplier;
                status.steamThrowerBoostBurnDrops = steamThrowerBoostBurnDrops;
                status.steamThrowerBoostBurnDropsActive = false;
                status.railCraftBulletSizeMultiplier = railCraftBulletSizeMultiplier;
                status.railCraftApplyOverheat = railCraftApplyOverheat;
                status.railCraftRicochetMultiplier = railCraftRicochetMultiplier;
                status.steamGatlingBulletSpeedMultiplier = steamGatlingBulletSpeedMultiplier;
                status.steamGatlingBossDamageMultiplier = steamGatlingBossDamageMultiplier;
                status.steamGatlingNormalDamageMultiplier = steamGatlingNormalDamageMultiplier;
                status.steamGatlingDownwardRecoil = steamGatlingDownwardRecoil;
            }

            MaterialManager material = MaterialManager.Instance;
            if (material != null)
            {
                material.scrap = scrap;
                material.gear = gear;
                material.upgradeCore = upgradeCore;
                material.moduleCore_lv1 = moduleCoreLv1;
                material.moduleCore_lv2 = moduleCoreLv2;
                material.moduleCore_lv3 = moduleCoreLv3;
            }
        }

        private static List<StatusManager.SavedUpgradePartPlacement> ClonePlacements(List<StatusManager.SavedUpgradePartPlacement> source)
        {
            List<StatusManager.SavedUpgradePartPlacement> result = new List<StatusManager.SavedUpgradePartPlacement>();
            if (source == null)
            {
                return result;
            }

            for (int i = 0; i < source.Count; i++)
            {
                StatusManager.SavedUpgradePartPlacement placement = source[i];
                if (placement == null)
                {
                    continue;
                }

                result.Add(new StatusManager.SavedUpgradePartPlacement
                {
                    partData = placement.partData,
                    gridX = placement.gridX,
                    gridY = placement.gridY,
                    rotation = placement.rotation,
                    inventoryIndex = placement.inventoryIndex,
                    refundCostsOnRemove = placement.refundCostsOnRemove,
                    returnToShopOnSameDay = placement.returnToShopOnSameDay,
                    placedShopDay = placement.placedShopDay
                });
            }

            return result;
        }
    }
}
