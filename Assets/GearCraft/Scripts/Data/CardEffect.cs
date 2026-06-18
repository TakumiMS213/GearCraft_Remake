using UnityEngine;

[System.Serializable]
public class CardEffect
{
    public CardEffectType type;
    public float value;

    [Header("Only used by matching effect types")]
    public WeaponDataSO weaponToGrant;
    public UpgradePartSO partToGrant;
    public MaterialManager.MaterialType materialType;
}

public enum CardEffectType
{
    AddHP = 0,
    AddGateHP = 1,
    AddSTR = 2,
    AddACC = 3,
    AddMaterial = 4,
    GrantWeapon = 5,
    GrantUpgradePart = 6,
    MagnetRangeUp = 8,
    ConvertSTRtoACC = 9,
    ConvertACCtoSTR = 10,
    GearCraftAxeSizeMultiplier = 100,
    GearCraftSwordMoveSpeedBonus = 101,
    GearCraftTransformGainGear = 102,
    SteamCannonBulletSpeedMultiplier = 110,
    SteamCannonExplosionRadiusMultiplier = 111,
    SteamCannonGiantBulletChance = 112,
    SteamCannonDirectHitKnockback = 113,
    SteamThrowerOverheatSlipDamage = 120,
    SteamThrowerNoBulletGravity = 121,
    SteamThrowerBoostDamageMultiplier = 122,
    SteamThrowerBoostBurnDrops = 123,
    RailCraftBulletSizeMultiplier = 130,
    RailCraftApplyOverheat = 131,
    RailCraftRicochetMultiplier = 132,
    SteamGatlingBulletSpeedMultiplier = 140,
    SteamGatlingBossDamageMultiplier = 141,
    SteamGatlingNormalDamageMultiplier = 142,
    SteamGatlingDownwardRecoil = 143
}
