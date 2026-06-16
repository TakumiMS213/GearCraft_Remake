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
    ConvertACCtoSTR = 10
}
