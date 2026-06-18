using UnityEngine;

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
}
