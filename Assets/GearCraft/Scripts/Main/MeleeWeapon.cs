using UnityEngine;

public class MeleeWeapon : MonoBehaviour
{
    private PlayerController playerController;
    private StatusManager runtimeStatus;
    private BoxCollider2D hitCollider;
    private Vector2 baseColliderSize;
    private Vector2 baseColliderOffset;
    public GameObject BurnPtPrehub;
    public GameObject ExplosionPrehub;

    void Awake()
    {
        hitCollider = GetComponent<BoxCollider2D>();
        if (hitCollider != null)
        {
            baseColliderSize = hitCollider.size;
            baseColliderOffset = hitCollider.offset;
        }
    }

    void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
        runtimeStatus = StatusManager.Instance;
    }

    public void ApplyWeaponData(WeaponDataSO weapon)
    {
        if (hitCollider == null || weapon == null || weapon.weaponType != WeaponType.Melee) return;

        float range = Mathf.Max(0.1f, weapon.attackRange);
        hitCollider.size = new Vector2(baseColliderSize.x, baseColliderSize.y * range);
        hitCollider.offset = new Vector2(baseColliderOffset.x, baseColliderOffset.y * range);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (runtimeStatus == null || runtimeStatus.currentWeapon == null) return;

        // 近接武器でのみダメージ判定
        if (other.CompareTag("Enemy") && runtimeStatus.currentWeapon.weaponType == WeaponType.Melee)
        {
            float damage = runtimeStatus.currentWeapon.baseDamage;

            // STR加算 + 強化パーツのボーナスダメージ
            damage += runtimeStatus.STR + runtimeStatus.currentWeapon.GetScaledFlatDamageBonus(runtimeStatus.bonusDamage);

            // クラフト武器ボーナス（GearCraft系）
            if (!runtimeStatus.currentWeapon.isDefault)
            {
                damage += runtimeStatus.craftWeaponDamagebuff;
            }

            EnemyController ec = other.GetComponent<EnemyController>();
            if (ec != null)
            {
                ec.TakeDamage(damage);
                return;
            }

            EnemyProjectileDamageable projectile = other.GetComponent<EnemyProjectileDamageable>();
            if (projectile == null)
            {
                projectile = other.GetComponentInParent<EnemyProjectileDamageable>();
            }

            if (projectile != null)
            {
                projectile.TakeDamage(damage);
            }
        }

        // 弾丸反射（近接武器で弾を弾く）
        if (other.CompareTag("Bullet") && runtimeStatus.currentWeapon.weaponType == WeaponType.Melee)
        {
            Destroy(other.gameObject);
            if (other.gameObject.name.Contains("Missile"))
            {
                if (ExplosionPrehub != null)
                    Instantiate(ExplosionPrehub, transform.position, Quaternion.identity);
            }
            else
            {
                if (BurnPtPrehub != null)
                    Instantiate(BurnPtPrehub, transform.position, Quaternion.identity);
            }
        }
    }
}
