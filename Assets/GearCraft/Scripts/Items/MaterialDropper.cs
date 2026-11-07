using UnityEngine;

namespace GearCraft.Scripts.Items
{
    public class MaterialDropper : MonoBehaviour
    {
        [Header("Drop Prefabs")]
        [SerializeField] private GameObject gearDropPrefab;
        [SerializeField] private GameObject scrapDropPrefab;
        [SerializeField] private GameObject upgradeCoreDropPrefab;
        [SerializeField] private GameObject moduleCoreLv1DropPrefab;
        [SerializeField] private GameObject moduleCoreLv2DropPrefab;
        [SerializeField] private GameObject moduleCoreLv3DropPrefab;

        public static MaterialDropper Instance { get; private set; }

        public GameObject GearDropPrefab => gearDropPrefab;
        public GameObject ScrapDropPrefab => scrapDropPrefab;
        public GameObject UpgradeCoreDropPrefab => upgradeCoreDropPrefab;
        public GameObject ModuleCoreLv1DropPrefab => moduleCoreLv1DropPrefab;
        public GameObject ModuleCoreLv2DropPrefab => moduleCoreLv2DropPrefab;
        public GameObject ModuleCoreLv3DropPrefab => moduleCoreLv3DropPrefab;

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

        public void DropMaterials(Vector3 position, MaterialDropEntry[] drops, float meleeKillBonus = 0f)
        {
            if (drops == null || drops.Length == 0) return;

            float junkMult = 1f;
            if (StatusManager.Instance != null)
            {
                junkMult = StatusManager.Instance.junkCollectorMult;
            }

            foreach (var drop in drops)
            {
                if (drop == null) continue;
                if (Random.value > drop.dropChance) continue;

                int rawAmount = Random.Range(drop.minAmount, drop.maxAmount + 1);
                int amount = GetDropAmount(rawAmount, 1f, junkMult, meleeKillBonus);
                if (amount <= 0) continue;

                GameObject prefab = GetPrefabForType(drop.type);
                if (prefab == null) continue;

                SpawnItems(position, amount, 0.3f, drop.type, prefab);
            }
        }

        public void DropMaterialsWithStageBonus(Vector3 position, MaterialDropEntry[] drops, StageGeneratorSO stageConfig, int stageNum, float meleeKillBonus = 0f)
        {
            if (drops == null || drops.Length == 0) return;

            float stageMult = 1f;
            if (stageConfig != null)
            {
                stageMult = stageConfig.dropMultiplierCurve.Evaluate(stageNum);
            }

            float junkMult = 1f;
            if (StatusManager.Instance != null)
            {
                junkMult = StatusManager.Instance.junkCollectorMult;
            }

            foreach (var drop in drops)
            {
                if (drop == null) continue;
                if (Random.value > drop.dropChance) continue;

                int rawAmount = Random.Range(drop.minAmount, drop.maxAmount + 1);
                int amount = Mathf.Max(1, GetDropAmount(rawAmount, stageMult, junkMult, meleeKillBonus));

                GameObject prefab = GetPrefabForType(drop.type);
                if (prefab == null) continue;

                SpawnItems(position, amount, 0.5f, drop.type, prefab);
            }
        }

        private static int GetDropAmount(int rawAmount, float stageMult, float junkMult, float meleeKillBonus)
        {
            if (meleeKillBonus <= 0f) return Mathf.RoundToInt(rawAmount * stageMult * junkMult);
            return CalculateMeleeDropAmount(rawAmount, stageMult, junkMult, meleeKillBonus, Random.value);
        }

        public static int CalculateMeleeDropAmount(int rawAmount, float stageMult, float junkMult, float meleeKillBonus, float roll)
        {
            float amount = Mathf.Max(0f, rawAmount * stageMult * (junkMult + meleeKillBonus));
            int whole = Mathf.FloorToInt(amount);
            // Preserve small increases even when the original drop is just one item.
            return whole + (roll < amount - whole ? 1 : 0);
        }

        private void SpawnItems(Vector3 position, int amount, float horizontalRange, MaterialManager.MaterialType type, GameObject prefab)
        {
            for (int i = 0; i < amount; i++)
            {
                Vector3 spawnPos = position + new Vector3(
                    Random.Range(-horizontalRange, horizontalRange),
                    Random.Range(0f, 0.5f),
                    0f
                );

                GameObject item = Instantiate(prefab, spawnPos, Quaternion.identity);
                DroppedMaterialItem droppedItem = item != null ? item.GetComponent<DroppedMaterialItem>() : null;
                if (droppedItem != null)
                {
                    droppedItem.Configure(type, 1);
                }
            }
        }

        private GameObject GetPrefabForType(MaterialManager.MaterialType type)
        {
            switch (type)
            {
                case MaterialManager.MaterialType.Gear:
                    return gearDropPrefab;
                case MaterialManager.MaterialType.Scrap:
                    return scrapDropPrefab;
                case MaterialManager.MaterialType.UpgradeCore:
                    return upgradeCoreDropPrefab;
                case MaterialManager.MaterialType.ModuleCore_lv1:
                    return moduleCoreLv1DropPrefab != null ? moduleCoreLv1DropPrefab : upgradeCoreDropPrefab;
                case MaterialManager.MaterialType.ModuleCore_lv2:
                    return moduleCoreLv2DropPrefab != null ? moduleCoreLv2DropPrefab : upgradeCoreDropPrefab;
                case MaterialManager.MaterialType.ModuleCore_lv3:
                    return moduleCoreLv3DropPrefab != null ? moduleCoreLv3DropPrefab : upgradeCoreDropPrefab;
                default:
                    return scrapDropPrefab;
            }
        }
    }
}
