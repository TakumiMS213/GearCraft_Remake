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

    // シングルトン本体
    public static StatusManager Instance;

    // ---- プレイヤーステータス ----
    public int HP = 100;
    public int SAN = 100;
    public int STR = 0;
    public int ACC = 0;
    public float GATE = 100;

    // ---- 武器管理 ----
    [Header("武器設定")]
    public WeaponDataSO defaultWeapon;           // 刀（破壊後のフォールバック）
    public WeaponDataSO currentWeapon;            // 現在装備中の武器
    public List<WeaponDataSO> ownedWeapons = new List<WeaponDataSO>(); // 所持武器一覧
    public List<WeaponDataSO> weaponCatalog = new List<WeaponDataSO>(); // 参照用の全武器データ
    public List<UpgradePartSO> ownedUpgradeParts = new List<UpgradePartSO>();
    public List<SavedUpgradePartPlacement> savedUpgradePartPlacements = new List<SavedUpgradePartPlacement>();

    // ---- 耐久値管理 ----
    // 武器名をキーにした耐久値辞書（SOはシーンを跨ぐとインスタンスが変わる場合があるため名前で管理）
    private Dictionary<string, int> weaponDurabilities = new Dictionary<string, int>();
    private Dictionary<string, int> weaponMaxDurabilities = new Dictionary<string, int>();

    // ---- 旧互換（段階的移行のため残す） ----
    public int selectWeapon = 0;

    // ---- モジュール ----
    public bool module_scrap = false;
    public bool module_repair = false;
    public bool module_barrier = false;
    public bool punkDrive = false;
    public int craftWeaponDamagebuff = 0;

    // ---- ゲーム進行 ----
    public bool killAllEnemies = true;
    public bool UseCraftSpacebuff = false;
    public int bossKillCount = 0;         // ボス撃破数（グリッドサイズ決定用）
    public int upgradeShopDay = 0;

    // ---- 強化効果キャッシュ ----
    public float bonusDamage = 0f;
    public float attackSpeedMult = 1f;
    public bool hasBulletDouble = false;
    public float spreadModifier = 0f;
    public float durabilityDrainChance = 0f;
    public int ricochetCount = 0;
    public float junkCollectorMult = 1f;
    public float bulletSizeMult = 1f;
    public float magnetRange = 3f;        // 初期吸収範囲
    public int maxDurabilityBonus = 0;

    private void Awake()
    {
        // シングルトン実装
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeWeapons();
    }

    // ---- 武器耐久値管理 ----

    /// <summary>
    /// 武器を取得し、耐久値を初期化する
    /// </summary>
    public void AcquireWeapon(WeaponDataSO weapon)
    {
        if (weapon == null) return;
        if (!ownedWeapons.Contains(weapon))
            ownedWeapons.Add(weapon);

        EnsureWeaponDurability(weapon);
    }

    /// <summary>
    /// 武器を装備する
    /// </summary>
    public void EquipWeapon(WeaponDataSO weapon)
    {
        if (weapon == null || !ownedWeapons.Contains(weapon)) return;
        EnsureWeaponDurability(weapon);
        currentWeapon = weapon;
    }

    public WeaponDataSO FindOwnedWeaponByName(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return null;
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

    private void InitializeWeapons()
    {
        if (currentWeapon == null && defaultWeapon != null)
        {
            currentWeapon = defaultWeapon;
        }

        AddUniqueWeapon(ownedWeapons, defaultWeapon);
        AddUniqueWeapon(weaponCatalog, defaultWeapon);
        AddUniqueWeapon(weaponCatalog, currentWeapon);

        EnsureWeaponDurability(defaultWeapon);
        EnsureWeaponDurability(currentWeapon);

        if (ownedWeapons == null)
        {
            return;
        }

        foreach (WeaponDataSO weapon in ownedWeapons)
        {
            EnsureWeaponDurability(weapon);
            AddUniqueWeapon(weaponCatalog, weapon);
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

    private void EnsureWeaponDurability(WeaponDataSO weapon)
    {
        if (weapon == null || string.IsNullOrEmpty(weapon.weaponName)) return;

        string key = weapon.weaponName;
        if (!weaponMaxDurabilities.ContainsKey(key))
        {
            weaponMaxDurabilities[key] = weapon.maxDurability;
        }
        if (!weaponDurabilities.ContainsKey(key))
        {
            weaponDurabilities[key] = GetBaseMaxDurability(weapon);
        }
    }

    private int GetBaseMaxDurability(WeaponDataSO weapon)
    {
        if (weapon == null || string.IsNullOrEmpty(weapon.weaponName)) return 0;

        string key = weapon.weaponName;
        return weaponMaxDurabilities.ContainsKey(key) ? weaponMaxDurabilities[key] : weapon.maxDurability;
    }

    public int AcquireUpgradePart(UpgradePartSO part)
    {
        if (part == null) return -1;

        ownedUpgradeParts.Add(part);
        return ownedUpgradeParts.Count - 1;
    }

    public UpgradePartSO GetOwnedUpgradePart(int index)
    {
        if (index < 0 || index >= ownedUpgradeParts.Count) return null;
        return ownedUpgradeParts[index];
    }

    /// <summary>
    /// 現在の武器の耐久値を取得
    /// </summary>
    public int GetCurrentDurability()
    {
        if (currentWeapon == null) return -1;
        EnsureWeaponDurability(currentWeapon);
        string key = currentWeapon.weaponName;
        return weaponDurabilities.ContainsKey(key) ? weaponDurabilities[key] : currentWeapon.maxDurability;
    }

    /// <summary>
    /// 現在の武器の最大耐久値を取得（強化パーツのMaxDurabilityUp含む）
    /// </summary>
    public int GetCurrentMaxDurability()
    {
        if (currentWeapon == null) return -1;
        return GetBaseMaxDurability(currentWeapon) + maxDurabilityBonus;
    }

    /// <summary>
    /// ステージ終了時に呼ぶ：現在の武器の耐久値を1減らす
    /// </summary>
    public bool ReduceDurability()
    {
        if (currentWeapon == null || currentWeapon.isDefault) return false;

        EnsureWeaponDurability(currentWeapon);
        string key = currentWeapon.weaponName;
        weaponDurabilities[key]--;

        if (weaponDurabilities[key] <= 0)
        {
            // 武器破壊
            DestroyCurrentWeapon();
            return true; // 武器が破壊された
        }
        return false;
    }

    /// <summary>
    /// 耐久値を回復する
    /// </summary>
    public void RepairDurability(int amount)
    {
        if (currentWeapon == null || currentWeapon.isDefault) return;
        EnsureWeaponDurability(currentWeapon);
        string key = currentWeapon.weaponName;

        int max = GetCurrentMaxDurability();
        weaponDurabilities[key] = Mathf.Min(weaponDurabilities[key] + amount, max);
    }

    /// <summary>
    /// 耐久ドレイン：敵撃破時に確率で耐久回復
    /// </summary>
    public void TryDurabilityDrain()
    {
        if (durabilityDrainChance <= 0f) return;
        if (Random.value <= durabilityDrainChance)
        {
            RepairDurability(1);
            Debug.Log("耐久ドレイン発動！耐久+1");
        }
    }

    /// <summary>
    /// 現在の武器を破壊し、デフォルト武器（刀）に切り替える
    /// </summary>
    private void DestroyCurrentWeapon()
    {
        Debug.Log($"武器「{currentWeapon.weaponName}」が破壊されました！");
        string key = currentWeapon.weaponName;
        weaponDurabilities.Remove(key);
        weaponMaxDurabilities.Remove(key);
        ownedWeapons.Remove(currentWeapon);

        // デフォルト武器に切り替え
        currentWeapon = defaultWeapon;

        // 強化パーツは全て消去（UpgradeGridManagerが処理する）
    }

    /// <summary>
    /// 強化グリッドサイズを返す（ボス撃破数に応じて拡張）
    /// </summary>
    public int GetUpgradeGridSize()
    {
        return 3 + bossKillCount; // 3→4→5→6
    }

    public int BeginUpgradeShopDay()
    {
        upgradeShopDay++;
        return upgradeShopDay;
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
}
