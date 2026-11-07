using UnityEngine;

[System.Serializable]
public sealed class MeleeUpgradeStats
{
    public float knockbackDistance;
    public float swingDurationMultiplier = 1f;
    public float sizeBonus;
    public float explosionDamageRatio;
    [UnityEngine.Serialization.FormerlySerializedAs("punkDriveHitBonus")]
    public float punkDriveKillBonus;
    public float killDropBonus;
    [UnityEngine.Serialization.FormerlySerializedAs("damageReduction")]
    public float invincibilityBonus;
    public int extraJumps;
    public float moveSpeedBonus;
    public float bossDamageBonus;

    public float SizeMultiplier => 1f + Mathf.Clamp(sizeBonus, 0f, 0.2f);
    public float InvincibilityDurationMultiplier => 1f + Mathf.Clamp(invincibilityBonus, 0f, 0.8f);
    public float SwingMultiplier => Mathf.Max(0.35f, swingDurationMultiplier);
    public MeleeUpgradeStats Copy() => (MeleeUpgradeStats)MemberwiseClone();

    public void Add(UpgradeEffect effect)
    {
        switch (effect.type)
        {
            case UpgradeEffectType.MeleeKnockback: knockbackDistance += effect.value; break;
            case UpgradeEffectType.MeleeSwingSpeed: swingDurationMultiplier *= effect.value; break;
            case UpgradeEffectType.MeleeSize: sizeBonus += effect.value; break;
            case UpgradeEffectType.MeleeExplosion: explosionDamageRatio = Mathf.Max(explosionDamageRatio, effect.value); break;
            case UpgradeEffectType.MeleeDriveBoost: punkDriveKillBonus = Mathf.Max(punkDriveKillBonus, effect.value); break;
            case UpgradeEffectType.MeleeKillDrops: killDropBonus += effect.value; break;
            case UpgradeEffectType.MeleeDefense: invincibilityBonus += effect.value; break;
            case UpgradeEffectType.MeleeExtraJump: extraJumps += Mathf.Max(0, Mathf.RoundToInt(effect.value)); break;
            case UpgradeEffectType.MeleeMoveSpeed: moveSpeedBonus += effect.value; break;
            case UpgradeEffectType.MeleeBossDamage: bossDamageBonus += effect.value; break;
        }
    }
}
