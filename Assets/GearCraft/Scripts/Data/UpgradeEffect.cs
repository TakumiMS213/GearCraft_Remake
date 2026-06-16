[System.Serializable]
public class UpgradeEffect
{
    public UpgradeEffectType type;
    public float value;
}

public enum UpgradeEffectType
{
    DamageFlat = 0,
    AttackSpeedMult = 1,
    BulletDouble = 2,
    SpreadReduction = 3,
    SpreadIncrease = 4,
    Ricochet = 6,
    JunkCollector = 7,
    BulletSizeUp = 8,
    MagnetRangeUp = 10
}
