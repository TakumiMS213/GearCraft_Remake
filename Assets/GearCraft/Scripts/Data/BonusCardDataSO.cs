using UnityEngine;
using System.Text;

[CreateAssetMenu(menuName = "GearCraft/Bonus Card", fileName = "NewBonusCard")]
public class BonusCardDataSO : ScriptableObject
{
    [Header("基本情報")]
    public string cardName;
    public Sprite cardImage;
    public GameObject cardPrefab;

    [Header("出現設定")]
    [Range(0f, 1f)] public float weight = 0.1f;
    public string[] requiredWeaponNames;
    public bool selectableOnce = false;
    public string uniqueSelectionId;

    [Header("効果（複数可）")]
    public CardEffect[] effects;

    public string BuildTooltipText()
    {
        var text = new StringBuilder();
        text.AppendLine($"<b><color=#E2BE59>【{cardName}】</color></b>");
        if (effects != null)
            foreach (CardEffect effect in effects)
                if (effect != null) text.AppendLine(DescribeEffect(effect));
        if (requiredWeaponNames != null && requiredWeaponNames.Length > 0)
        {
            bool gearCraftOnly = System.Array.TrueForAll(requiredWeaponNames,
                weapon => !string.IsNullOrEmpty(weapon) && weapon.StartsWith("GearCraft"));
            text.AppendLine($"対象武器：{(gearCraftOnly ? "GearCraft" : string.Join(" / ", requiredWeaponNames))}");
        }
        if (selectableOnce) text.AppendLine("このカードは一度だけ選択できます。");
        return text.ToString().TrimEnd();
    }

    private static string DescribeEffect(CardEffect effect)
    {
        string value = effect.value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        int count = (int)effect.value;
        switch (effect.type)
        {
            case CardEffectType.AddHP: return $"HPを{count}回復（上限100）。";
            case CardEffectType.AddGateHP: return $"門の耐久力 +{value}。";
            case CardEffectType.AddSTR: return $"近接攻撃力（STR） +{count}。";
            case CardEffectType.AddACC: return $"射撃攻撃力（ACC） +{count}。";
            case CardEffectType.AddMaterial: return $"{MaterialName(effect.materialType)}を{count}個入手。";
            case CardEffectType.GrantWeapon:
                return effect.weaponToGrant != null ? $"{effect.weaponToGrant.weaponName}を入手し、装備します。" : "武器を入手します。";
            case CardEffectType.GrantUpgradePart:
                return effect.partToGrant != null ? $"強化パーツ「{effect.partToGrant.partName}」を入手。" : "強化パーツを入手します。";
            case CardEffectType.MagnetRangeUp: return $"素材を引き寄せる範囲 +{value}。";
            case CardEffectType.ConvertSTRtoACC: return $"STRが{count}以上なら、STR -{count} / ACC +{count}。";
            case CardEffectType.ConvertACCtoSTR: return $"ACCが{count}以上なら、ACC -{count} / STR +{count}。";
            case CardEffectType.GearCraftAxeSizeMultiplier: return $"斧に変形した時の武器サイズ {value}倍。";
            case CardEffectType.GearCraftSwordMoveSpeedBonus: return $"大剣を装備している時の移動速度 +{value}。";
            case CardEffectType.GearCraftTransformGainGear: return $"変形するたびに歯車を{Mathf.Max(0, count)}枚入手。";
            case CardEffectType.SteamCannonBulletSpeedMultiplier: return Multiplier("弾速", effect.value);
            case CardEffectType.SteamCannonExplosionRadiusMultiplier: return $"水蒸気爆発の範囲 {value}倍。";
            case CardEffectType.SteamCannonGiantBulletChance: return $"超巨大弾の発射確率 +{effect.value * 100f:0.##}%（上限100%）。";
            case CardEffectType.SteamCannonDirectHitKnockback: return "弾が直撃した敵を後方へ押し返します。";
            case CardEffectType.SteamThrowerOverheatSlipDamage: return $"オーバーヒート中の敵に継続ダメージ +{value}。";
            case CardEffectType.SteamThrowerNoBulletGravity: return "蒸気が重力で落下しなくなります。";
            case CardEffectType.SteamThrowerBoostDamageMultiplier: return $"BOOST中の蒸気ダメージ {value}倍。";
            case CardEffectType.SteamThrowerBoostBurnDrops: return "<color=#FF7070>BOOST中の蒸気が素材ドロップを焼却します。</color>";
            case CardEffectType.RailCraftBulletSizeMultiplier: return $"弾丸サイズ {value}倍。";
            case CardEffectType.RailCraftApplyOverheat: return "命中した敵にオーバーヒートを付与します。";
            case CardEffectType.RailCraftRicochetMultiplier: return $"弾丸の反射回数 {value}倍。";
            case CardEffectType.SteamGatlingBulletSpeedMultiplier: return $"弾速 {value}倍（WeaponCustomで強化した値に乗算）。";
            case CardEffectType.SteamGatlingBossDamageMultiplier: return Multiplier("ボスへのダメージ", effect.value);
            case CardEffectType.SteamGatlingNormalDamageMultiplier: return Multiplier("通常の敵へのダメージ", effect.value);
            case CardEffectType.SteamGatlingDownwardRecoil: return "下向きに撃つと、反動で跳び上がれます。";
            default: return $"{effect.type}：{value}。";
        }
    }

    private static string Multiplier(string label, float value)
    {
        string text = $"{label} {value:0.##}倍。";
        return value < 1f ? $"<color=#FF7070>{text}</color>" : text;
    }

    private static string MaterialName(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap: return "スクラップ";
            case MaterialManager.MaterialType.Gear: return "歯車";
            case MaterialManager.MaterialType.UpgradeCore: return "アップグレードコア";
            case MaterialManager.MaterialType.ModuleCore_lv1: return "低級モジュールコア";
            case MaterialManager.MaterialType.ModuleCore_lv2: return "中級モジュールコア";
            case MaterialManager.MaterialType.ModuleCore_lv3: return "上級モジュールコア";
            default: return type.ToString();
        }
    }
}
