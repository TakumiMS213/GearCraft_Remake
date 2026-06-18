using UnityEngine;

public enum CraftResultType
{
    Weapon,
    Module,
    PunkDrive
}

[CreateAssetMenu(menuName = "GearCraft/Craft Recipe", fileName = "NewRecipe")]
public class CraftRecipeSO : ScriptableObject
{
    [Header("基本情報")]
    public string recipeName;
    public Sprite icon;
    public Sprite completedImage;    // 完成時に表示する画像

    [Header("コスト")]
    public CraftCost[] costs;

    [Header("結果")]
    public CraftResultType resultType;
    public CraftResultDescriptionSO resultDescription;

    [Header("Weapon結果（resultType == Weapon のとき）")]
    public WeaponDataSO weaponResult;
    [Tooltip("同時に所持させる追加武器。GearCraft Sword/Axeのようなペア装備に使用")]
    public WeaponDataSO additionalWeaponResult;


    [Header("Module結果（resultType == Module のとき）")]
    [Tooltip("StatusManagerのフラグ名: module_scrap, module_repair, module_barrier")]
    public string moduleFlag;

    [Header("その他数値結果")]
    [Tooltip("craftWeaponDamagebuffなどに加算する値")]
    public int statBonus = 0;

    [Header("前提条件")]
    [Tooltip("特定の武器を所持している必要がある場合に設定")]
    public WeaponDataSO requiredWeapon;

    [Header("Unlock")]
    [SerializeField, Min(0)] private int unlockBossKills;
    [SerializeField, TextArea(2, 4)] private string summary;
    [SerializeField] private string unlockId;
    [SerializeField] private int unlockSortOrder;
    [SerializeField] private bool hiddenFromUnlockList;

    public int UnlockBossKills => unlockBossKills;
    public int UnlockSortOrder => unlockSortOrder;
    public string Summary => summary;
    public bool HiddenFromUnlockList => hiddenFromUnlockList;
    public string DisplayName => string.IsNullOrWhiteSpace(recipeName) ? name : recipeName;
    public string UnlockId => string.IsNullOrWhiteSpace(unlockId) ? DisplayName : unlockId;
    public Sprite NotificationIcon => icon != null ? icon : completedImage;
}
