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
    public float accDamageMultiplier = 1f;
    public float attackRange = 1f;       // 近接は判定長、遠距離は弾の生存時間として扱う

    [Header("遠距離設定（Ranged のみ）")]
    public float bulletSpeed = 25f;
    public GameObject bulletPrefab;
    public float spreadAngle = 0f;       // 拡散角度（度）
    public Vector2 muzzleOffset = Vector2.zero; // x=照準方向, y=照準方向に対する上方向
    public bool useLineHitbox = false;   // true = 弾速に依存しない直線判定を使用
    public float lineHitboxWidth = 0.5f; // 直線判定の太さ
    public float lineHitboxDuration = 0.05f; // 判定を残す時間
    public GameObject lineHitboxVisualPrefab; // 直線判定の表示用Prefab
    public Sprite lineHitboxVisualSprite; // 表示Prefabへ後から差し替えるSprite

    [Header("耐久値")]
    public int maxDurability = 10;       // ステージ終了時に1減る

    [Header("アニメーション")]
    public string armTriggerName;        // Armアニメーターのトリガー名
    public float meleeRotateDuration = 0.5f; // 近接の腕回転時間

    [Header("特殊設定")]
    public bool isDefault = false;       // true = 刀（破壊後のフォールバック武器）
    public bool isPiercing = false;      // 貫通するか
    public float GetScaledFlatDamageBonus(float flatDamage)
    {
        return flatDamage * accDamageMultiplier;
    }
}
