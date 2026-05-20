using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName = "GearCraft/Upgrade Part", fileName = "NewUpgradePart")]
public class UpgradePartSO : ScriptableObject
{
    [Header("Basic")]
    public string partName;
    public Sprite icon;
    public PartRarity rarity = PartRarity.Common;
    public Color partColor = Color.cyan;

    [Header("Shape")]
    public int width = 1;
    public int height = 1;
    public bool[] shapeData = { true };

    [Header("Effects")]
    public UpgradeEffect[] effects;

    [Header("Costs")]
    public CraftCost[] costs;

    [Header("Restriction")]
    public bool rangedOnly;

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        int expectedLength = width * height;
        if (shapeData == null || shapeData.Length != expectedLength)
        {
            bool[] resized = new bool[expectedLength];
            for (int i = 0; i < resized.Length; i++)
            {
                resized[i] = shapeData == null || i >= shapeData.Length || shapeData[i];
            }

            shapeData = resized;
        }
    }

    public bool[,] GetRotatedShape(int rotation)
    {
        bool[,] original = new bool[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                original[y, x] = index < shapeData.Length && shapeData[index];
            }
        }

        rotation = ((rotation % 4) + 4) % 4;
        if (rotation == 0)
        {
            return original;
        }

        bool[,] result = original;
        for (int i = 0; i < rotation; i++)
        {
            result = Rotate90(result);
        }

        return result;
    }

    public int GetRotatedWidth(int rotation)
    {
        rotation = ((rotation % 4) + 4) % 4;
        return rotation % 2 == 0 ? width : height;
    }

    public int GetRotatedHeight(int rotation)
    {
        rotation = ((rotation % 4) + 4) % 4;
        return rotation % 2 == 0 ? height : width;
    }

    public string BuildTooltipText()
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.IsNullOrEmpty(partName) ? "強化パーツ" : partName);
        builder.AppendLine($"レアリティ: {FormatRarity(rarity)}");
        builder.AppendLine($"形状: {width}x{height}");

        if (rangedOnly)
        {
            builder.AppendLine("制限: 遠距離武器のみ");
        }

        AppendEffects(builder);
        AppendCosts(builder);
        return builder.ToString();
    }

    private void AppendEffects(StringBuilder builder)
    {
        builder.AppendLine("効果:");
        if (effects == null || effects.Length == 0)
        {
            builder.AppendLine("- なし");
            return;
        }

        for (int i = 0; i < effects.Length; i++)
        {
            UpgradeEffect effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            builder.AppendLine($"- {FormatEffect(effect)}");
        }
    }

    private void AppendCosts(StringBuilder builder)
    {
        builder.AppendLine("コスト:");
        if (costs == null || costs.Length == 0)
        {
            builder.AppendLine("- 無料");
            return;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            CraftCost cost = costs[i];
            if (cost == null)
            {
                continue;
            }

            builder.AppendLine($"- {FormatMaterialName(cost.type)} x{cost.amount}");
        }
    }

    private static string FormatEffect(UpgradeEffect effect)
    {
        switch (effect.type)
        {
            case UpgradeEffectType.DamageFlat:
                return $"ダメージ +{effect.value:0}";
            case UpgradeEffectType.AttackSpeedMult:
                return $"攻撃速度 x{effect.value:0.##}";
            case UpgradeEffectType.BulletDouble:
                return "弾数2倍";
            case UpgradeEffectType.SpreadReduction:
                return $"拡散 -{effect.value:0.#}";
            case UpgradeEffectType.SpreadIncrease:
                return $"拡散 +{effect.value:0.#}";
            case UpgradeEffectType.DurabilityDrain:
                return $"耐久吸収 {effect.value * 100f:0}%";
            case UpgradeEffectType.Ricochet:
                return $"跳弾 +{effect.value:0}";
            case UpgradeEffectType.JunkCollector:
                return $"素材ドロップ x{effect.value:0.##}";
            case UpgradeEffectType.BulletSizeUp:
                return $"弾サイズ x{effect.value:0.##}";
            case UpgradeEffectType.MaxDurabilityUp:
                return $"最大耐久 +{effect.value:0}";
            case UpgradeEffectType.MagnetRangeUp:
                return $"回収範囲 +{effect.value:0.#}";
            default:
                return $"{effect.type} {effect.value:0.##}";
        }
    }

    private static string FormatRarity(PartRarity rarity)
    {
        switch (rarity)
        {
            case PartRarity.Common: return "コモン";
            case PartRarity.Uncommon: return "アンコモン";
            case PartRarity.Rare: return "レア";
            case PartRarity.Epic: return "エピック";
            default: return rarity.ToString();
        }
    }

    private static string FormatMaterialName(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap: return "Scrap";
            case MaterialManager.MaterialType.Gear: return "Gear";
            case MaterialManager.MaterialType.UpgradeCore: return "UpCore";
            case MaterialManager.MaterialType.ModuleCore_lv1: return "ModC1";
            case MaterialManager.MaterialType.ModuleCore_lv2: return "ModC2";
            case MaterialManager.MaterialType.ModuleCore_lv3: return "ModC3";
            default: return type.ToString();
        }
    }

    private static bool[,] Rotate90(bool[,] source)
    {
        int rows = source.GetLength(0);
        int cols = source.GetLength(1);
        bool[,] result = new bool[cols, rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                result[x, rows - 1 - y] = source[y, x];
            }
        }

        return result;
    }
}
