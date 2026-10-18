using UnityEngine;

public class MaterialManager : MonoBehaviour
{
    public static MaterialManager Instance { get; private set; }
    public static event System.Action MaterialsChanged;

    // 素材の種類を列挙
    public enum MaterialType
    {
        Scrap,
        Gear,
        UpgradeCore,        // 統合されたアップグレードコア
        ModuleCore_lv1,     // 旧互換
        ModuleCore_lv2,
        ModuleCore_lv3
    }

    [Header("素材の所持数（初期値）")]
    [SerializeField] public int scrap = 0;
    [SerializeField] public int gear = 0;
    [SerializeField] public int upgradeCore = 0;
    [SerializeField] public int moduleCore_lv1 = 0;
    [SerializeField] public int moduleCore_lv2 = 0;
    [SerializeField] public int moduleCore_lv3 = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool gearCheatWasPressed;
#endif
    private bool hasNotifiedMaterials;
    private int notificationBatchDepth;
    private (int, int, int, int, int, int) lastNotifiedMaterials;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        NotifyMaterialsChanged();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool gearCheatPressed =
            Input.GetKey(KeyCode.G) &&
            Input.GetKey(KeyCode.E) &&
            Input.GetKey(KeyCode.A) &&
            Input.GetKey(KeyCode.R);

        if (gearCheatPressed && !gearCheatWasPressed)
        {
            SetAllMaterials(99);
        }

        gearCheatWasPressed = gearCheatPressed;
#endif
    }

    private void LateUpdate()
    {
        // Preserve serialized/public fields while also observing Inspector edits and run rollback.
        NotifyMaterialsChanged();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        MaterialsChanged?.Invoke();
    }

    private void NotifyMaterialsChanged()
    {
        if (Instance != this || notificationBatchDepth > 0) return;

        var amounts = (scrap, gear, upgradeCore, moduleCore_lv1, moduleCore_lv2, moduleCore_lv3);
        if (hasNotifiedMaterials && amounts.Equals(lastNotifiedMaterials)) return;

        lastNotifiedMaterials = amounts;
        hasNotifiedMaterials = true;
        GearCraft.Gallery.GalleryProgress.RecordMaterials(scrap, gear);
        MaterialsChanged?.Invoke();
    }

    // ===== 外部からアクセスするための関数 =====

    public int GetMaterial(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Scrap: return scrap;
            case MaterialType.Gear: return gear;
            case MaterialType.UpgradeCore: return upgradeCore;
            case MaterialType.ModuleCore_lv1: return moduleCore_lv1;
            case MaterialType.ModuleCore_lv2: return moduleCore_lv2;
            case MaterialType.ModuleCore_lv3: return moduleCore_lv3;
            default: return 0;
        }
    }

    public void AddMaterial(MaterialType type, int amount)
    {
        if (amount <= 0) return;

        switch (type)
        {
            case MaterialType.Scrap: scrap += amount; break;
            case MaterialType.Gear: gear += amount; break;
            case MaterialType.UpgradeCore: upgradeCore += amount; break;
            case MaterialType.ModuleCore_lv1: moduleCore_lv1 += amount; break;
            case MaterialType.ModuleCore_lv2: moduleCore_lv2 += amount; break;
            case MaterialType.ModuleCore_lv3: moduleCore_lv3 += amount; break;
        }

        if (StageFlowManager.Instance != null)
        {
            StageFlowManager.Instance.RecordMaterialGained(type, amount);
        }

        NotifyMaterialsChanged();
    }

    public bool UseMaterial(MaterialType type, int amount)
    {
        if (amount <= 0) return false;

        switch (type)
        {
            case MaterialType.Scrap:
                if (scrap >= amount) { scrap -= amount; NotifyMaterialsChanged(); return true; }
                break;
            case MaterialType.Gear:
                if (gear >= amount) { gear -= amount; NotifyMaterialsChanged(); return true; }
                break;
            case MaterialType.UpgradeCore:
                if (upgradeCore >= amount) { upgradeCore -= amount; NotifyMaterialsChanged(); return true; }
                break;
            case MaterialType.ModuleCore_lv1:
                if (moduleCore_lv1 >= amount) { moduleCore_lv1 -= amount; NotifyMaterialsChanged(); return true; }
                break;
            case MaterialType.ModuleCore_lv2:
                if (moduleCore_lv2 >= amount) { moduleCore_lv2 -= amount; NotifyMaterialsChanged(); return true; }
                break;
            case MaterialType.ModuleCore_lv3:
                if (moduleCore_lv3 >= amount) { moduleCore_lv3 -= amount; NotifyMaterialsChanged(); return true; }
                break;
        }
        return false;
    }

    /// <summary>
    /// 指定素材が足りているかチェック
    /// </summary>
    public bool HasEnough(MaterialType type, int amount)
    {
        return GetMaterial(type) >= amount;
    }

    /// <summary>
    /// CraftCost配列で一括チェック
    /// </summary>
    public bool CanAfford(CraftCost[] costs)
    {
        if (costs == null) return true;
        foreach (var cost in costs)
        {
            if (!HasEnough(cost.type, cost.amount))
                return false;
        }
        return true;
    }

    public string GetShortageText(CraftCost[] costs)
    {
        var missing = new System.Collections.Generic.List<string>();
        var materials = GetAllMaterials();
        if (costs == null) return string.Empty;
        foreach (var cost in costs)
        {
            if (cost == null) continue;
            int shortage = cost.amount - GetMaterial(cost.type);
            if (shortage <= 0) continue;
            string name = materials.Find(material => material.type == cost.type).name;
            missing.Add($"{name} あと{shortage}");
        }
        return string.Join(" / ", missing);
    }

    /// <summary>
    /// CraftCost配列で一括消費
    /// </summary>
    public bool SpendCosts(CraftCost[] costs)
    {
        if (!CanAfford(costs)) return false;
        if (costs == null) return true;

        notificationBatchDepth++;
        try
        {
            foreach (var cost in costs)
                UseMaterial(cost.type, cost.amount);
        }
        finally
        {
            notificationBatchDepth--;
            NotifyMaterialsChanged();
        }
        return true;
    }

    public void ClearAllMaterials()
    {
        scrap = 0;
        gear = 0;
        upgradeCore = 0;
        moduleCore_lv1 = 0;
        moduleCore_lv2 = 0;
        moduleCore_lv3 = 0;
        NotifyMaterialsChanged();
    }

    public void SetAllMaterials(int amount)
    {
        scrap = amount;
        gear = amount;
        upgradeCore = amount;
        moduleCore_lv1 = amount;
        moduleCore_lv2 = amount;
        moduleCore_lv3 = amount;
        NotifyMaterialsChanged();
    }

    /// <summary>
    /// 全素材の種類と所持数をリストで返す（UI表示用）
    /// </summary>
    public System.Collections.Generic.List<(MaterialType type, int count, string name)> GetAllMaterials()
    {
        var list = new System.Collections.Generic.List<(MaterialType, int, string)>();
        list.Add((MaterialType.Scrap, scrap, "Scrap"));
        list.Add((MaterialType.Gear, gear, "Gear"));
        list.Add((MaterialType.UpgradeCore, upgradeCore, "UpCore"));
        list.Add((MaterialType.ModuleCore_lv1, moduleCore_lv1, "ModC1"));
        list.Add((MaterialType.ModuleCore_lv2, moduleCore_lv2, "ModC2"));
        list.Add((MaterialType.ModuleCore_lv3, moduleCore_lv3, "ModC3"));
        return list;
    }
}
