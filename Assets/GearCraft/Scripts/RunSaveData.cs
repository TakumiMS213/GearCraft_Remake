using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[Serializable]
public sealed class RunSaveAsset
{
    public string id;
    public UnityEngine.Object asset;
}

public sealed class RunSaveData
{
    public int version = 1;
    public string runId;
    public long revision;
    public int stage;
    public int seed;
    public int shopReroll;
    public string lastRecipe;
    public int[] materials;
    public JObject status;
}

// Explicit whitelist: derived part bonuses and transient combat state are not persisted.
public sealed class RunSaveCodec
{
    private static readonly string[] Fields = (
        "HP SAN STR ACC GATE currentWeapon ownedWeapons ownedUpgradeParts savedUpgradePartPlacements selectWeapon " +
        "module_scrap module_repair module_barrier punkDrive craftWeaponDamagebuff killAllEnemies UseCraftSpacebuff " +
        "bossKillCount upgradeShopDay hasSavedUpgradeShopLineup savedUpgradeShopLineup notifiedCraftUnlockIds " +
        "dormTaskProgress craftTutorialProgress selectedUniqueBonusCardIds permanentMagnetRangeBonus " +
        "gearCraftAxeSizeMultiplier gearCraftSwordMoveSpeedBonus gearCraftTransformGearGain " +
        "steamCannonBulletSpeedMultiplier steamCannonExplosionRadiusMultiplier steamCannonGiantBulletChance steamCannonDirectHitKnockback " +
        "steamThrowerOverheatSlipDamage steamThrowerNoBulletGravity steamThrowerBoostDamageMultiplier steamThrowerBoostBurnDrops " +
        "railCraftBulletSizeMultiplier railCraftApplyOverheat railCraftRicochetMultiplier " +
        "steamGatlingBulletSpeedMultiplier steamGatlingBossDamageMultiplier steamGatlingNormalDamageMultiplier steamGatlingDownwardRecoil").Split(' ');
    private static FieldInfo Field(string name) => typeof(StatusManager).GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    private readonly JsonSerializer serializer;

    private sealed class AssetConverter : JsonConverter
    {
        private readonly Dictionary<string, UnityEngine.Object> byId = new Dictionary<string, UnityEngine.Object>();
        private readonly Dictionary<UnityEngine.Object, string> byAsset = new Dictionary<UnityEngine.Object, string>();
        public AssetConverter(IEnumerable<RunSaveAsset> entries)
        {
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id) || e.asset == null || byId.ContainsKey(e.id) || byAsset.ContainsKey(e.asset))
                    throw new InvalidDataException("セーブ用アセット一覧に不正な項目があります。");
                byId.Add(e.id, e.asset); byAsset.Add(e.asset, e.id);
            }
        }
        public override bool CanConvert(Type type) => typeof(WeaponDataSO).IsAssignableFrom(type) || typeof(UpgradePartSO).IsAssignableFrom(type);
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer s)
        {
            if (value == null) { writer.WriteNull(); return; }
            if (!byAsset.TryGetValue((UnityEngine.Object)value, out var id)) throw new InvalidDataException("保存対象のアセットが一覧にありません。");
            writer.WriteValue(id);
        }
        public override object ReadJson(JsonReader reader, Type type, object oldValue, JsonSerializer s)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            if (reader.TokenType != JsonToken.String || !byId.TryGetValue((string)reader.Value, out var asset) || !type.IsInstanceOfType(asset))
                throw new InvalidDataException("セーブに含まれる武器・パーツを復元できません。");
            return asset;
        }
    }
    public RunSaveCodec(IEnumerable<RunSaveAsset> entries)
    {
        serializer = JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None,
            Converters = new List<JsonConverter> { new AssetConverter(entries) } });
    }
    public RunSaveData Capture(StatusManager status, MaterialManager materials, string runId, long revision, int seed, int reroll)
    {
        var values = new JObject();
        foreach (string name in Fields)
        {
            object value = Field(name).GetValue(status);
            values[name] = value == null ? JValue.CreateNull() : JToken.FromObject(value, serializer);
        }
        return new RunSaveData { runId = runId, revision = revision, seed = seed, shopReroll = reroll,
            stage = StageCounter.Instance.StageCount, status = values, lastRecipe = status.LastSelectedCraftRecipeId,
            materials = Enum.GetValues(typeof(MaterialManager.MaterialType)).Cast<MaterialManager.MaterialType>().Select(materials.GetMaterial).ToArray() };
    }
    public Dictionary<string, object> Validate(RunSaveData data, StageGeneratorSO config)
    {
        if (data == null || data.version != 1) throw new InvalidDataException("対応していないセーブ形式です。");
        if (string.IsNullOrEmpty(data.runId) || data.revision < 1 || data.stage < 2 || data.stage > config.totalStages ||
            config.stagesPerRest < 1 || (data.stage - 1) % config.stagesPerRest != 0 || data.shopReroll < 0 ||
            data.materials == null || data.materials.Length != 6 || data.materials.Any(v => v < 0) || data.status == null)
            throw new InvalidDataException("セーブの進行情報が不正です。");
        var values = new Dictionary<string, object>();
        foreach (string name in Fields)
        {
            if (!data.status.TryGetValue(name, out var token)) throw new InvalidDataException("セーブに必要な情報がありません: " + name);
            object value = token.ToObject(Field(name).FieldType, serializer);
            if (value == null) throw new InvalidDataException("セーブの情報が欠落しています: " + name);
            if (value is float f && (float.IsNaN(f) || float.IsInfinity(f))) throw new InvalidDataException("能力値が不正です。");
            values[name] = value;
        }
        int hp = (int)values["HP"], bosses = (int)values["bossKillCount"];
        if (hp <= 0 || hp > 100 || (float)values["GATE"] <= 0 || bosses < 0 || bosses > 4 ||
            (int)values["STR"] < 0 || (int)values["ACC"] < 0) throw new InvalidDataException("能力値が不正です。");
        var weapons = (List<WeaponDataSO>)values["ownedWeapons"];
        if (weapons.Any(w => w == null) || !weapons.Contains((WeaponDataSO)values["currentWeapon"]))
            throw new InvalidDataException("装備武器の情報が不正です。");
        var parts = (List<UpgradePartSO>)values["ownedUpgradeParts"];
        if (((List<UpgradePartSO>)values["savedUpgradeShopLineup"]).Any(p => p == null) ||
            ((List<string>)values["selectedUniqueBonusCardIds"]).Any(string.IsNullOrWhiteSpace) ||
            ((List<string>)values["notifiedCraftUnlockIds"]).Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("保存された在庫・解放情報が不正です。");
        var tutorials = (CraftTutorialRunProgress)values["craftTutorialProgress"];
        ValidateTutorial(tutorials.craft); ValidateTutorial(tutorials.weaponCustom);
        var usedIndexes = new HashSet<int>(); var unique = new HashSet<UpgradePartSO>();
        var cells = new bool[3 + bosses, 3 + bosses];
        if (parts.Any(p => p == null)) throw new InvalidDataException("所有パーツの情報が不正です。");
        foreach (var p in (List<StatusManager.SavedUpgradePartPlacement>)values["savedUpgradePartPlacements"])
        {
            if (p == null || p.partData == null || p.rotation < 0 || p.rotation > 3 ||
                p.inventoryIndex < -1 || p.inventoryIndex >= parts.Count || p.placedShopDay < -1 ||
                p.inventoryIndex >= 0 && (parts[p.inventoryIndex] != p.partData || !usedIndexes.Add(p.inventoryIndex)) ||
                p.partData.unique && !unique.Add(p.partData)) throw new InvalidDataException("パーツ配置の情報が不正です。");
            var shape = p.partData.GetRotatedShape(p.rotation);
            for (int y = 0; y < shape.GetLength(0); y++) for (int x = 0; x < shape.GetLength(1); x++)
                if (shape[y, x])
                {
                    int gx = p.gridX + x, gy = p.gridY + y;
                    if (gx < 0 || gy < 0 || gx >= 3 + bosses || gy >= 3 + bosses || cells[gx, gy])
                        throw new InvalidDataException("パーツが盤面に収まりません。");
                    cells[gx, gy] = true;
                }
        }
        return values;
    }
    private static void ValidateTutorial(CraftTutorialTrackProgress progress)
    {
        if (progress == null || progress.stepIndex < 0 || progress.hintLevel < 0 || progress.hintLevel > 3 ||
            progress.hintRequests < 0 || progress.invalidAttempts < 0 || progress.elapsedSeconds < 0 ||
            float.IsNaN(progress.elapsedSeconds) || float.IsInfinity(progress.elapsedSeconds) ||
            float.IsNaN(progress.baselineDamage) || float.IsInfinity(progress.baselineDamage) ||
            float.IsNaN(progress.baselineAttackSpeed) || float.IsInfinity(progress.baselineAttackSpeed) ||
            progress.baselineParts == null || progress.baselineParts.Any(p => p == null) ||
            progress.craftedRecipeIds == null || progress.craftedRecipeIds.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("チュートリアルの進行情報が不正です。");
    }
    public void Restore(RunSaveData data, Dictionary<string, object> values, StatusManager status, MaterialManager materials)
    {
        foreach (string name in Fields) Field(name).SetValue(status, values[name]);
        status.LastSelectedCraftRecipeId = data.lastRecipe ?? "";
        StageCounter.Instance.StageCount = data.stage;
        for (int i = 0; i < data.materials.Length; i++) materials.SetMaterialForRestore((MaterialManager.MaterialType)i, data.materials[i]);
        status.steamThrowerBoostBurnDropsActive = false;
        status.RecalculateUpgradeEffects();
    }
}
