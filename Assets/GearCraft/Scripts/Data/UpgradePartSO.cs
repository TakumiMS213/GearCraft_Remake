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
        builder.AppendLine(string.IsNullOrEmpty(partName) ? "Upgrade Part" : partName);
        builder.AppendLine($"Rarity: {rarity}");
        builder.AppendLine($"Shape: {width}x{height}");

        if (rangedOnly)
        {
            builder.AppendLine("Restriction: ranged weapons only");
        }

        AppendEffects(builder);
        AppendCosts(builder);
        return builder.ToString();
    }

    private void AppendEffects(StringBuilder builder)
    {
        builder.AppendLine("Effects:");
        if (effects == null || effects.Length == 0)
        {
            builder.AppendLine("- None");
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
        builder.AppendLine("Cost:");
        if (costs == null || costs.Length == 0)
        {
            builder.AppendLine("- Free");
            return;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            CraftCost cost = costs[i];
            if (cost == null)
            {
                continue;
            }

            builder.AppendLine($"- {cost.type}: {cost.amount}");
        }
    }

    private static string FormatEffect(UpgradeEffect effect)
    {
        switch (effect.type)
        {
            case UpgradeEffectType.DamageFlat:
                return $"Damage +{effect.value:0}";
            case UpgradeEffectType.AttackSpeedMult:
                return $"Attack speed x{effect.value:0.##}";
            case UpgradeEffectType.BulletDouble:
                return "Bullet double";
            case UpgradeEffectType.SpreadReduction:
                return $"Spread -{effect.value:0.#}";
            case UpgradeEffectType.SpreadIncrease:
                return $"Spread +{effect.value:0.#}";
            case UpgradeEffectType.DurabilityDrain:
                return $"Durability drain {effect.value * 100f:0}%";
            case UpgradeEffectType.Ricochet:
                return $"Ricochet +{effect.value:0}";
            case UpgradeEffectType.JunkCollector:
                return $"Material drop x{effect.value:0.##}";
            case UpgradeEffectType.BulletSizeUp:
                return $"Bullet size x{effect.value:0.##}";
            case UpgradeEffectType.MaxDurabilityUp:
                return $"Max durability +{effect.value:0}";
            case UpgradeEffectType.MagnetRangeUp:
                return $"Magnet range +{effect.value:0.#}";
            default:
                return $"{effect.type} {effect.value:0.##}";
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
