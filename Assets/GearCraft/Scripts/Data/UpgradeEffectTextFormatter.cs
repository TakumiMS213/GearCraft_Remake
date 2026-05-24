using UnityEngine;

public static class UpgradeEffectTextFormatter
{
    public static string FormatEffect(UpgradeEffect effect)
    {
        if (effect == null)
        {
            return "効果なし";
        }

        switch (effect.type)
        {
            case UpgradeEffectType.DamageFlat:
                return FormatMagnitudeDelta("ダメージ", effect.value);
            case UpgradeEffectType.AttackSpeedMult:
                return FormatAttackSpeedMultiplier(effect.value);
            case UpgradeEffectType.BulletDouble:
                return "弾数100%増加";
            case UpgradeEffectType.SpreadReduction:
                return $"拡散低下 {GetMagnitudeLabel(effect.value)}";
            case UpgradeEffectType.SpreadIncrease:
                return $"拡散増加 {GetMagnitudeLabel(effect.value)}";
            case UpgradeEffectType.DurabilityDrain:
                return FormatPercentDelta("耐久吸収率", effect.value);
            case UpgradeEffectType.Ricochet:
                return FormatMagnitudeDelta("跳弾", effect.value);
            case UpgradeEffectType.JunkCollector:
                return FormatPercentDelta("素材ドロップ率", effect.value);
            case UpgradeEffectType.BulletSizeUp:
                return FormatPercentDelta("弾サイズ", effect.value);
            case UpgradeEffectType.MaxDurabilityUp:
                return FormatMagnitudeDelta("最大耐久", effect.value);
            case UpgradeEffectType.MagnetRangeUp:
                return FormatMagnitudeDelta("回収範囲", effect.value);
            default:
                return $"{effect.type} {effect.value:0.##}";
        }
    }

    public static string FormatEffectLine(UpgradeEffect effect, string positiveColor, string negativeColor)
    {
        bool negative = IsNegativeEffect(effect);
        string sign = negative ? "-" : "＋";
        string color = negative ? negativeColor : positiveColor;
        return $"<color={color}>{sign} {FormatEffect(effect)}</color>";
    }

    public static bool IsNegativeEffect(UpgradeEffect effect)
    {
        if (effect == null)
        {
            return false;
        }

        switch (effect.type)
        {
            case UpgradeEffectType.SpreadIncrease:
                return true;
            case UpgradeEffectType.AttackSpeedMult:
                return effect.value > 1f;
            case UpgradeEffectType.DamageFlat:
            case UpgradeEffectType.DurabilityDrain:
            case UpgradeEffectType.Ricochet:
            case UpgradeEffectType.JunkCollector:
            case UpgradeEffectType.BulletSizeUp:
            case UpgradeEffectType.MaxDurabilityUp:
            case UpgradeEffectType.MagnetRangeUp:
                return effect.value < 0f;
            default:
                return false;
        }
    }

    public static string FormatMagnitudeDelta(string label, float value)
    {
        string direction = value >= 0f ? "増加" : "低下";
        return $"{label}{direction} {GetMagnitudeLabel(value)}";
    }

    public static string FormatPercentDelta(string label, float value)
    {
        string direction = value >= 0f ? "増加" : "低下";
        return $"{label}{Mathf.Abs(value) * 100f:0}%{direction}";
    }

    public static string FormatAttackSpeedMultiplier(float multiplier)
    {
        float percent = (1f - multiplier) * 100f;
        string direction = percent >= 0f ? "増加" : "低下";
        return $"攻撃速度{Mathf.Abs(percent):0}%{direction}";
    }

    public static string FormatMultiplierDelta(string label, float multiplier)
    {
        float percent = (multiplier - 1f) * 100f;
        string direction = percent >= 0f ? "増加" : "低下";
        return $"{label}{Mathf.Abs(percent):0}%{direction}";
    }

    public static string FormatSpreadModifier(float modifier)
    {
        if (modifier < 0f)
        {
            return $"拡散低下 {GetMagnitudeLabel(modifier)}";
        }

        return $"拡散増加 {GetMagnitudeLabel(modifier)}";
    }

    public static string GetMagnitudeLabel(float value)
    {
        float magnitude = Mathf.Abs(value);
        if (magnitude <= 1f)
        {
            return "小";
        }

        if (magnitude <= 3f)
        {
            return "中";
        }

        if (magnitude <= 6f)
        {
            return "大";
        }

        if (magnitude <= 10f)
        {
            return "超";
        }

        return "極";
    }
}
