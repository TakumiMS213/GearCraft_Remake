[System.Serializable]
public class UpgradeEffect
{
    public UpgradeEffectType type;
    [UnityEngine.Tooltip("DamagePercent uses a fraction: 0.1 = +10% base damage. Multiple parts add together.")]
    public float value;
}

public enum UpgradeEffectType
{
    DamagePercent = 0,
    AttackSpeedMult = 1,
    BulletDouble = 2,
    SpreadReduction = 3,
    SpreadIncrease = 4,
    Ricochet = 6,
    JunkCollector = 7,
    BulletSizeUp = 8,
    MagnetRangeUp = 10,
    MeleeKnockback = 11,
    MeleeSwingSpeed = 12,
    MeleeSize = 13,
    MeleeExplosion = 14,
    MeleeDriveBoost = 15,
    MeleeKillDrops = 16,
    MeleeDefense = 17,
    MeleeExtraJump = 18,
    MeleeMoveSpeed = 19,
    MeleeBossDamage = 20
}
