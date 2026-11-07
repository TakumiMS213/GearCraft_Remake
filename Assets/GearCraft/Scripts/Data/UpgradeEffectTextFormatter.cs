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
            case UpgradeEffectType.DamagePercent:
                return FormatPercentDelta("基礎ダメージ", effect.value);
            case UpgradeEffectType.AttackSpeedMult:
                return FormatAttackSpeedMultiplier(effect.value);
            case UpgradeEffectType.BulletDouble:
                return "弾数+1";
            case UpgradeEffectType.SpreadReduction:
                return $"拡散低下 {GetMagnitudeLabel(effect.value)}";
            case UpgradeEffectType.SpreadIncrease:
                return $"拡散増加 {GetMagnitudeLabel(effect.value)}";
            case UpgradeEffectType.Ricochet:
                return FormatMagnitudeDelta("跳弾", effect.value);
            case UpgradeEffectType.JunkCollector:
                return FormatPercentDelta("素材獲得数", effect.value);
            case UpgradeEffectType.BulletSizeUp:
                return FormatPercentDelta("弾サイズ", effect.value);
            case UpgradeEffectType.MagnetRangeUp:
                return FormatMagnitudeDelta("回収範囲", effect.value);
            case UpgradeEffectType.MeleeKnockback:
                return $"命中時に敵を{effect.value:0.##}押し戻す（合計上限1.5、ボスは効果20%）";
            case UpgradeEffectType.MeleeSwingSpeed:
                return $"振り時間を{(1f - effect.value) * 100f:0}%短縮（合計上限65%）";
            case UpgradeEffectType.MeleeSize:
                return $"武器サイズ・攻撃範囲+{effect.value * 100f:0}%（パーツ上限20%、巨大化と重複）";
            case UpgradeEffectType.MeleeExplosion:
                return $"命中時に爆発（近接ダメージの{effect.value * 100f:0}%、半径1.2）";
            case UpgradeEffectType.MeleeDriveBoost:
                return $"近接撃破後5秒、PunkDriveの火力+{effect.value * 100f:0}%（再撃破で時間更新）";
            case UpgradeEffectType.MeleeKillDrops:
                return $"近接撃破時の素材獲得数+{effect.value * 100f:0}%（Junk Magnetと加算）";
            case UpgradeEffectType.MeleeDefense:
                return $"被弾後の無敵時間+{effect.value * 100f:0}%（近接装備中、合計上限80%）";
            case UpgradeEffectType.MeleeExtraJump:
                return $"空中ジャンプ+{effect.value:0}回";
            case UpgradeEffectType.MeleeMoveSpeed:
                return FormatPercentDelta("移動速度", effect.value);
            case UpgradeEffectType.MeleeBossDamage:
                return FormatPercentDelta("ボスへの近接ダメージ", effect.value);
            default:
                return $"{effect.type} {effect.value:0.##}";
        }
    }

    public static string FormatEffectLine(UpgradeEffect effect, string positiveColor, string negativeColor)
    {
        bool negative = IsNegativeEffect(effect);
        string sign = negative ? "-" : "+";
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
            case UpgradeEffectType.MeleeSwingSpeed:
                return effect.value > 1f;
            case UpgradeEffectType.DamagePercent:
            case UpgradeEffectType.Ricochet:
            case UpgradeEffectType.JunkCollector:
            case UpgradeEffectType.BulletSizeUp:
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
        if (magnitude <= 1f) return "小";
        if (magnitude <= 3f) return "中";
        if (magnitude <= 6f) return "大";
        if (magnitude <= 10f) return "超";
        return "極";
    }
}
