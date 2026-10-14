using System.Collections.Generic;
using UnityEngine;

public class StatusManager : MonoBehaviour
{
    [System.Serializable]
    public class SavedUpgradePartPlacement
    {
        public UpgradePartSO partData;
        public int gridX;
        public int gridY;
        public int rotation;
        public int inventoryIndex = -1;
        public bool refundCostsOnRemove;
        public bool returnToShopOnSameDay;
        public int placedShopDay = -1;
    }

    public static StatusManager Instance;

    public int HP = 100;
    public int SAN = 100;
    public int STR = 0;
    public int ACC = 0;
    public float GATE = 100;

    [Header("Weapons")]
    public WeaponDataSO defaultWeapon;
    public WeaponDataSO currentWeapon;
    public List<WeaponDataSO> ownedWeapons = new List<WeaponDataSO>();
    public List<WeaponDataSO> weaponCatalog = new List<WeaponDataSO>();
    public List<UpgradePartSO> ownedUpgradeParts = new List<UpgradePartSO>();
    public List<SavedUpgradePartPlacement> savedUpgradePartPlacements = new List<SavedUpgradePartPlacement>();

    public int selectWeapon = 0;

    public string LastSelectedCraftRecipeId { get; set; } = string.Empty;

    public bool module_scrap = false;
    public bool module_repair = false;
    public bool module_barrier = false;
    public bool punkDrive = false;
    public int craftWeaponDamagebuff = 0;

    public bool killAllEnemies = true;
    public bool UseCraftSpacebuff = false;
    public int bossKillCount = 0;
    public int upgradeShopDay = 0;
    public bool hasSavedUpgradeShopLineup;
    public List<UpgradePartSO> savedUpgradeShopLineup = new List<UpgradePartSO>();
    [SerializeField] private List<string> notifiedCraftUnlockIds = new List<string>();

    [Header("Dorm Tasks")]
    [SerializeField] private DormTaskProgress dormTaskProgress = new DormTaskProgress();
    public DormTaskProgress DormTasks => dormTaskProgress ?? (dormTaskProgress = new DormTaskProgress());

    [Header("Craft Tutorial")]
    [SerializeField] private CraftTutorialRunProgress craftTutorialProgress = new CraftTutorialRunProgress();
    public CraftTutorialRunProgress CraftTutorials => craftTutorialProgress ?? (craftTutorialProgress = new CraftTutorialRunProgress());

    public float bonusDamage = 0f;
    public float attackSpeedMult = 1f;
    public bool hasBulletDouble = false;
    public float spreadModifier = 0f;
    public int ricochetCount = 0;
    public float junkCollectorMult = 1f;
    public float bulletSizeMult = 1f;
    public float magnetRange = 3f;
    public List<string> selectedUniqueBonusCardIds = new List<string>();

    [Header("Weapon Specific Bonus Cards")]
    public float gearCraftAxeSizeMultiplier = 1f;
    public float gearCraftSwordMoveSpeedBonus = 0f;
    public int gearCraftTransformGearGain = 0;
    public float steamCannonBulletSpeedMultiplier = 1f;
    public float steamCannonExplosionRadiusMultiplier = 1f;
    public float steamCannonGiantBulletChance = 0f;
    public float steamCannonDirectHitKnockback = 0f;
    public float steamThrowerOverheatSlipDamage = 0f;
    public bool steamThrowerNoBulletGravity = false;
    public float steamThrowerBoostDamageMultiplier = 1f;
    public bool steamThrowerBoostBurnDrops = false;
    public bool steamThrowerBoostBurnDropsActive = false;
    public float railCraftBulletSizeMultiplier = 1f;
    public bool railCraftApplyOverheat = false;
    public float railCraftRicochetMultiplier = 1f;
    public float steamGatlingBulletSpeedMultiplier = 1f;
    public float steamGatlingBossDamageMultiplier = 1f;
    public float steamGatlingNormalDamageMultiplier = 1f;
    public bool steamGatlingDownwardRecoil = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeWeapons();
    }

    public void AcquireWeapon(WeaponDataSO weapon)
    {
        if (weapon == null)
        {
            return;
        }

        if (!ownedWeapons.Contains(weapon))
        {
            ownedWeapons.Add(weapon);
        }

        AddUniqueWeapon(weaponCatalog, weapon);
        GearCraft.Gallery.GalleryProgress.RecordWeapon(weapon);
    }

    public void EquipWeapon(WeaponDataSO weapon)
    {
        if (weapon == null || !ownedWeapons.Contains(weapon))
        {
            return;
        }

        bool weaponTypeChanged = currentWeapon == null || currentWeapon.weaponType != weapon.weaponType;
        float temporaryAttackSpeedMultiplier = 1f;
        if (weaponTypeChanged)
        {
            float previousUpgradeSpeed = GetSavedUpgradeAttackSpeed(currentWeapon);
            if (previousUpgradeSpeed > 0f)
                temporaryAttackSpeedMultiplier = attackSpeedMult / previousUpgradeSpeed;
        }
        currentWeapon = weapon;
        AddUniqueWeapon(weaponCatalog, weapon);

        // The grid persists outside Craft, so changing weapon type must also refresh its effects.
        // Keep temporary attack-speed effects such as Boost while recomputing the grid bonus.
        if (weaponTypeChanged)
        {
            RecalculateUpgradeEffects();
            attackSpeedMult *= temporaryAttackSpeedMultiplier;
            UpgradeGridManager gridManager = UpgradeGridManager.Instance;
            if (gridManager != null)
            {
                gridManager.RefreshUI();
                gridManager.effectDisplay?.Refresh();
            }
        }
    }

    private float GetSavedUpgradeAttackSpeed(WeaponDataSO weapon)
    {
        float multiplier = 1f;
        foreach (UpgradePartSO part in GetSavedUpgradeParts())
        {
            if (part == null || part.effects == null ||
                (part.rangedOnly && (weapon == null || weapon.weaponType != WeaponType.Ranged)))
                continue;

            foreach (UpgradeEffect effect in part.effects)
                if (effect != null && effect.type == UpgradeEffectType.AttackSpeedMult)
                    multiplier *= effect.value;
        }
        return multiplier;
    }

    public float GetWeaponDamage(WeaponDataSO weapon)
    {
        return weapon != null ? weapon.baseDamage + GetWeaponDamageBonus(weapon) : 0f;
    }

    public float GetWeaponDamageBonus(WeaponDataSO weapon)
    {
        if (weapon == null) return 0f;

        float damageBonus = weapon.weaponType == WeaponType.Melee
            ? STR + weapon.GetScaledFlatDamageBonus(bonusDamage)
            : weapon.GetScaledFlatDamageBonus(ACC + bonusDamage);

        if (IsCraftedWeapon(weapon))
            damageBonus += craftWeaponDamagebuff;

        return damageBonus;
    }

    private static bool IsCraftedWeapon(WeaponDataSO weapon)
    {
        // AR is a supplied rifle even though its legacy isDefault flag is false.
        switch (weapon.weaponName)
        {
            case "GearCraft":
            case "GearCraft_Sword":
            case "GearCraft_Axe":
            case "SteamCannon":
            case "RailCraft":
            case "SteamGatling":
            case "SteamThrower":
                return true;
            default:
                return false;
        }
    }

    public void RecalculateUpgradeEffects()
    {
        RecalculateUpgradeEffects(GetSavedUpgradeParts());
    }

    private IEnumerable<UpgradePartSO> GetSavedUpgradeParts()
    {
        if (savedUpgradePartPlacements == null) yield break;
        foreach (SavedUpgradePartPlacement placement in savedUpgradePartPlacements)
            if (placement != null) yield return placement.partData;
    }

    public void RecalculateUpgradeEffects(IEnumerable<UpgradePartSO> parts)
    {
        bonusDamage = 0f;
        attackSpeedMult = 1f;
        hasBulletDouble = false;
        spreadModifier = 0f;
        ricochetCount = 0;
        junkCollectorMult = 1f;
        bulletSizeMult = 1f;
        magnetRange = 3f;

        if (parts == null) return;
        foreach (UpgradePartSO part in parts)
        {
            if (part == null || part.effects == null ||
                (part.rangedOnly && (currentWeapon == null || currentWeapon.weaponType != WeaponType.Ranged)))
                continue;

            foreach (UpgradeEffect effect in part.effects)
            {
                if (effect == null) continue;
                switch (effect.type)
                {
                    case UpgradeEffectType.DamageFlat:
                        bonusDamage += effect.value;
                        break;
                    case UpgradeEffectType.AttackSpeedMult:
                        attackSpeedMult *= effect.value;
                        break;
                    case UpgradeEffectType.BulletDouble:
                        hasBulletDouble = true;
                        break;
                    case UpgradeEffectType.SpreadReduction:
                        spreadModifier -= effect.value;
                        break;
                    case UpgradeEffectType.SpreadIncrease:
                        spreadModifier += effect.value;
                        break;
                    case UpgradeEffectType.Ricochet:
                        ricochetCount += (int)effect.value;
                        break;
                    case UpgradeEffectType.JunkCollector:
                        junkCollectorMult += effect.value;
                        break;
                    case UpgradeEffectType.BulletSizeUp:
                        bulletSizeMult += effect.value;
                        break;
                    case UpgradeEffectType.MagnetRangeUp:
                        magnetRange += effect.value;
                        break;
                }
            }
        }
    }

    public WeaponDataSO FindOwnedWeaponByName(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName))
        {
            return null;
        }

        return ownedWeapons.Find(weapon => weapon != null && weapon.weaponName == weaponName);
    }

    public WeaponDataSO FindWeaponDataByName(string weaponName)
    {
        WeaponDataSO ownedWeapon = FindOwnedWeaponByName(weaponName);
        if (ownedWeapon != null)
        {
            return ownedWeapon;
        }

        if (string.IsNullOrEmpty(weaponName) || weaponCatalog == null)
        {
            return null;
        }

        return weaponCatalog.Find(weapon => weapon != null && weapon.weaponName == weaponName);
    }

    public int AcquireUpgradePart(UpgradePartSO part)
    {
        if (part == null)
        {
            return -1;
        }

        ownedUpgradeParts.Add(part);
        return ownedUpgradeParts.Count - 1;
    }

    public UpgradePartSO GetOwnedUpgradePart(int index)
    {
        return index >= 0 && index < ownedUpgradeParts.Count ? ownedUpgradeParts[index] : null;
    }

    public int GetUpgradeGridSize()
    {
        return 3 + bossKillCount;
    }

    public int BeginUpgradeShopDay()
    {
        int stage = StageCounter.Instance != null ? Mathf.Max(1, StageCounter.Instance.StageCount) : 1;
        // Keep the stock on the same day shown by StageFlowManager, even after leaving Craft.
        int day = ((stage - 1) / 3) + 1;
        if (upgradeShopDay != day)
        {
            upgradeShopDay = day;
            hasSavedUpgradeShopLineup = false;
            savedUpgradeShopLineup.Clear();
        }

        return upgradeShopDay;
    }

    public bool IsCraftUnlockNotified(string unlockId)
    {
        return !string.IsNullOrWhiteSpace(unlockId) && notifiedCraftUnlockIds.Contains(unlockId);
    }

    public void MarkCraftUnlockNotified(string unlockId)
    {
        if (string.IsNullOrWhiteSpace(unlockId) || notifiedCraftUnlockIds.Contains(unlockId))
        {
            return;
        }

        notifiedCraftUnlockIds.Add(unlockId);
    }

    public bool IsUniqueBonusCardSelected(string uniqueId)
    {
        return !string.IsNullOrWhiteSpace(uniqueId) && selectedUniqueBonusCardIds.Contains(uniqueId);
    }

    public void MarkUniqueBonusCardSelected(string uniqueId)
    {
        if (string.IsNullOrWhiteSpace(uniqueId) || selectedUniqueBonusCardIds.Contains(uniqueId))
        {
            return;
        }

        selectedUniqueBonusCardIds.Add(uniqueId);
    }

    public void SaveUpgradeGridState(List<UpgradeGridManager.PlacedPart> placedParts)
    {
        savedUpgradePartPlacements.Clear();
        if (placedParts == null)
        {
            return;
        }

        for (int i = 0; i < placedParts.Count; i++)
        {
            UpgradeGridManager.PlacedPart part = placedParts[i];
            if (part == null || part.partData == null)
            {
                continue;
            }

            savedUpgradePartPlacements.Add(new SavedUpgradePartPlacement
            {
                partData = part.partData,
                gridX = part.gridX,
                gridY = part.gridY,
                rotation = part.rotation,
                inventoryIndex = part.inventoryIndex,
                refundCostsOnRemove = part.refundCostsOnRemove,
                returnToShopOnSameDay = part.returnToShopOnSameDay,
                placedShopDay = part.placedShopDay
            });
        }
    }

    public void ClearSavedUpgradeGridState()
    {
        savedUpgradePartPlacements.Clear();
    }

    public void ResetRunProgress()
    {
        HP = 100;
        SAN = 100;
        STR = 0;
        ACC = 0;
        GATE = 100;

        currentWeapon = defaultWeapon;
        ownedWeapons.Clear();
        ownedUpgradeParts.Clear();
        savedUpgradePartPlacements.Clear();
        selectWeapon = 0;
        LastSelectedCraftRecipeId = string.Empty;

        module_scrap = false;
        module_repair = false;
        module_barrier = false;
        punkDrive = false;
        craftWeaponDamagebuff = 0;

        killAllEnemies = true;
        UseCraftSpacebuff = false;
        bossKillCount = 0;
        upgradeShopDay = 0;
        hasSavedUpgradeShopLineup = false;
        savedUpgradeShopLineup.Clear();
        notifiedCraftUnlockIds.Clear();
        DormTasks.Reset();
        CraftTutorials.Reset();

        bonusDamage = 0f;
        attackSpeedMult = 1f;
        hasBulletDouble = false;
        spreadModifier = 0f;
        ricochetCount = 0;
        junkCollectorMult = 1f;
        bulletSizeMult = 1f;
        magnetRange = 3f;
        selectedUniqueBonusCardIds.Clear();

        gearCraftAxeSizeMultiplier = 1f;
        gearCraftSwordMoveSpeedBonus = 0f;
        gearCraftTransformGearGain = 0;
        steamCannonBulletSpeedMultiplier = 1f;
        steamCannonExplosionRadiusMultiplier = 1f;
        steamCannonGiantBulletChance = 0f;
        steamCannonDirectHitKnockback = 0f;
        steamThrowerOverheatSlipDamage = 0f;
        steamThrowerNoBulletGravity = false;
        steamThrowerBoostDamageMultiplier = 1f;
        steamThrowerBoostBurnDrops = false;
        steamThrowerBoostBurnDropsActive = false;
        railCraftBulletSizeMultiplier = 1f;
        railCraftApplyOverheat = false;
        railCraftRicochetMultiplier = 1f;
        steamGatlingBulletSpeedMultiplier = 1f;
        steamGatlingBossDamageMultiplier = 1f;
        steamGatlingNormalDamageMultiplier = 1f;
        steamGatlingDownwardRecoil = false;

        InitializeWeapons();
    }

    private void InitializeWeapons()
    {
        if (currentWeapon == null && defaultWeapon != null)
        {
            currentWeapon = defaultWeapon;
        }

        AddUniqueWeapon(ownedWeapons, defaultWeapon);
        AddUniqueWeapon(weaponCatalog, defaultWeapon);
        AddUniqueWeapon(weaponCatalog, currentWeapon);

        if (ownedWeapons == null)
        {
            return;
        }

        for (int i = 0; i < ownedWeapons.Count; i++)
        {
            AddUniqueWeapon(weaponCatalog, ownedWeapons[i]);
            GearCraft.Gallery.GalleryProgress.RecordWeapon(ownedWeapons[i]);
        }
    }

    private static void AddUniqueWeapon(List<WeaponDataSO> list, WeaponDataSO weapon)
    {
        if (list == null || weapon == null || list.Contains(weapon))
        {
            return;
        }

        list.Add(weapon);
    }
}
