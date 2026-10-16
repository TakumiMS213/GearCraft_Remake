using UnityEngine;

[CreateAssetMenu(menuName = "GearCraft/Weapon Data", fileName = "NewWeapon")]
public class WeaponDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public string weaponName;
    public Sprite icon;
    [TextArea(2, 4)]
    public string description;
    public WeaponType weaponType;

    [Header("Attack Parameters")]
    public float coolTime = 0.5f;
    public float baseDamage = 20f;
    public float accDamageMultiplier = 1f;
    [Tooltip("Flat damage bonus multiplier. A negative value uses the ACC multiplier for existing weapon data.")]
    public float flatDamageMultiplier = -1f;
    [Min(0f), Tooltip("Damage gained from Upgrade Modules. Kept separate from ACC and grid parts.")]
    public float craftModuleDamageMultiplier = 1f;
    public float attackRange = 1f;

    [Header("Ranged Settings")]
    public float bulletSpeed = 25f;
    public GameObject bulletPrefab;
    public float spreadAngle = 0f;
    public Vector2 muzzleOffset = Vector2.zero;
    public bool useLineHitbox = false;
    public float lineHitboxWidth = 0.5f;
    public float lineHitboxDuration = 0.05f;
    public GameObject lineHitboxVisualPrefab;
    public Sprite lineHitboxVisualSprite;

    [Header("Animation")]
    public string armTriggerName;
    public float meleeRotateDuration = 0.5f;

    [Header("Special Settings")]
    public bool isDefault = false;
    public bool isPiercing = false;

    public float GetScaledAccDamageBonus(float acc)
    {
        return acc * accDamageMultiplier;
    }

    public float GetScaledFlatDamageBonus(float flatDamage)
    {
        float multiplier = flatDamageMultiplier < 0f ? accDamageMultiplier : flatDamageMultiplier;
        return flatDamage * multiplier;
    }
}
