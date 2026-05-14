using UnityEngine;

[CreateAssetMenu(menuName = "GearCraft/Weapon Data", fileName = "NewWeapon")]
public class WeaponDataSO : ScriptableObject
{
    [Header("基本情報")]
    public string weaponName;
    public Sprite icon;
    public WeaponType weaponType;

    [Header("攻撃パラメータ")]
    public float coolTime = 0.5f;
    public float baseDamage = 20f;
    public float attackRange = 1f;       // 近接の攻撃範囲

    [Header("遠距離設定（Ranged のみ）")]
    public float bulletSpeed = 25f;
    public GameObject bulletPrefab;
    public float spreadAngle = 0f;       // 拡散角度（度）

    [Header("耐久値")]
    public int maxDurability = 10;       // ステージ終了時に1減る

    [Header("アニメーション")]
    public string armTriggerName;        // Armアニメーターのトリガー名
    public float meleeRotateDuration = 0.5f; // 近接の腕回転時間

    [Header("特殊設定")]
    public bool isDefault = false;       // true = 刀（破壊後のフォールバック武器）
    public bool isPiercing = false;      // 貫通するか
}
