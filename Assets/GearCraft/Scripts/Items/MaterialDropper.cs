using UnityEngine;

/// <summary>
/// 敵死亡時に素材をドロップするユーティリティ。
/// EnemyDataSO.dropsに基づいて各素材をランダム量生成する。
/// </summary>
public class MaterialDropper : MonoBehaviour
{
    [Header("素材プレハブ")]
    public GameObject gearDropPrefab;
    public GameObject scrapDropPrefab;
    public GameObject upgradeCoreDropPrefab;

    public static MaterialDropper Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(gameObject);
    }

    /// <summary>
    /// 指定位置にドロップテーブルに基づいて素材を散乱させる
    /// </summary>
    public void DropMaterials(Vector3 position, MaterialDropEntry[] drops)
    {
        if (drops == null || drops.Length == 0) return;

        // JunkCollector乗算
        float junkMult = 1f;
        if (StatusManager.Instance != null)
            junkMult = StatusManager.Instance.junkCollectorMult;

        foreach (var drop in drops)
        {
            if (drop == null) continue;
            if (Random.value > drop.dropChance) continue;

            int rawAmount = Random.Range(drop.minAmount, drop.maxAmount + 1);
            int amount = Mathf.RoundToInt(rawAmount * junkMult);
            if (amount <= 0) continue;

            GameObject prefab = GetPrefabForType(drop.type);
            if (prefab == null) continue;

            // 個数分のアイテムを生成（見た目的にドカドカ落ちる）
            for (int i = 0; i < amount; i++)
            {
                Vector3 spawnPos = position + new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    Random.Range(0f, 0.5f),
                    0f
                );

                GameObject item = Instantiate(prefab, spawnPos, Quaternion.identity);
                DroppedMaterialItem dmi = item.GetComponent<DroppedMaterialItem>();
                if (dmi != null)
                {
                    dmi.materialType = drop.type;
                    dmi.amount = 1;
                }
            }
        }
    }

    /// <summary>
    /// ステージ番号に応じたドロップ量乗算を適用してドロップする
    /// </summary>
    public void DropMaterialsWithStageBonus(Vector3 position, MaterialDropEntry[] drops, StageGeneratorSO stageConfig, int stageNum)
    {
        if (drops == null || drops.Length == 0) return;

        float stageMult = 1f;
        if (stageConfig != null)
            stageMult = stageConfig.dropMultiplierCurve.Evaluate(stageNum);

        float junkMult = 1f;
        if (StatusManager.Instance != null)
            junkMult = StatusManager.Instance.junkCollectorMult;

        float totalMult = stageMult * junkMult;

        foreach (var drop in drops)
        {
            if (drop == null) continue;
            if (Random.value > drop.dropChance) continue;

            int rawAmount = Random.Range(drop.minAmount, drop.maxAmount + 1);
            int amount = Mathf.Max(1, Mathf.RoundToInt(rawAmount * totalMult));

            GameObject prefab = GetPrefabForType(drop.type);
            if (prefab == null) continue;

            for (int i = 0; i < amount; i++)
            {
                Vector3 spawnPos = position + new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    Random.Range(0f, 0.5f),
                    0f
                );
                GameObject item = Instantiate(prefab, spawnPos, Quaternion.identity);
                DroppedMaterialItem dmi = item.GetComponent<DroppedMaterialItem>();
                if (dmi != null)
                {
                    dmi.materialType = drop.type;
                    dmi.amount = 1;
                }
            }
        }
    }

    private GameObject GetPrefabForType(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Gear: return gearDropPrefab;
            case MaterialManager.MaterialType.Scrap: return scrapDropPrefab;
            case MaterialManager.MaterialType.UpgradeCore: return upgradeCoreDropPrefab;
            default: return scrapDropPrefab;
        }
    }
}
