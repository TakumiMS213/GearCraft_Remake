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

    public bool module_scrap = false;
    public bool module_repair = false;
    public bool module_barrier = false;
    public bool punkDrive = false;
    public int craftWeaponDamagebuff = 0;

    public bool killAllEnemies = true;
    public bool UseCraftSpacebuff = false;
    public int bossKillCount = 0;
    public int upgradeShopDay = 0;
    [SerializeField] private List<string> notifiedCraftUnlockIds = new List<string>();

    public float bonusDamage = 0f;
    public float attackSpeedMult = 1f;
    public bool hasBulletDouble = false;
    public float spreadModifier = 0f;
    public int ricochetCount = 0;
    public float junkCollectorMult = 1f;
    public float bulletSizeMult = 1f;
    public float magnetRange = 3f;

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
    }

    public void EquipWeapon(WeaponDataSO weapon)
    {
        if (weapon == null || !ownedWeapons.Contains(weapon))
        {
            return;
        }

        currentWeapon = weapon;
        AddUniqueWeapon(weaponCatalog, weapon);
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
        upgradeShopDay++;
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
