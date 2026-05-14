[System.Serializable]
public class UpgradeEffect
{
    public UpgradeEffectType type;
    public float value;
}

public enum UpgradeEffectType
{
    DamageFlat,
    AttackSpeedMult,
    BulletDouble,
    SpreadReduction,
    SpreadIncrease,
    DurabilityDrain,
    Ricochet,
    JunkCollector,
    BulletSizeUp,
    MaxDurabilityUp,
    MagnetRangeUp
}
