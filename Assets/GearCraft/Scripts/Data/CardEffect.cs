using UnityEngine;

[System.Serializable]
public class CardEffect
{
    public CardEffectType type;
    public float value;

    [Header("付与系（該当タイプのみ使用）")]
    public WeaponDataSO weaponToGrant;       // GrantWeapon 用
    public UpgradePartSO partToGrant;        // GrantUpgradePart 用
    public MaterialManager.MaterialType materialType; // AddMaterial 用
}

public enum CardEffectType
{
    AddHP,              // HP回復（value = 回復量）
    AddGateHP,          // ゲートHP回復（value = 回復量）
    AddSTR,             // STR増加（value = 増加量）
    AddACC,             // ACC増加（value = 増加量）
    AddMaterial,        // 素材付与（materialType + value = 個数）
    GrantWeapon,        // 武器付与（weaponToGrant）
    GrantUpgradePart,   // 強化パーツ付与（partToGrant）
    RepairDurability,   // 耐久回復（value = 回復量）
    MagnetRangeUp,      // 素材吸収範囲UP（value = 追加範囲）
    ConvertSTRtoACC,    // STR→ACC変換（value = 変換量）
    ConvertACCtoSTR     // ACC→STR変換（value = 変換量）
}
