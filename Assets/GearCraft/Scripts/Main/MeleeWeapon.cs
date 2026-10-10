using UnityEngine;

public class MeleeWeapon : MonoBehaviour
{
    private PlayerController playerController;
    private StatusManager runtimeStatus;
    private BoxCollider2D hitCollider;
    private Vector2 baseColliderSize;
    private Vector2 baseColliderOffset;
    private bool isAttacking;
    private float attackEndsAt;
    public GameObject BurnPtPrehub;
    public GameObject ExplosionPrehub;

    void Awake()
    {
        hitCollider = GetComponent<BoxCollider2D>();
        if (hitCollider != null)
        {
            baseColliderSize = hitCollider.size;
            baseColliderOffset = hitCollider.offset;
            hitCollider.enabled = false;
        }
    }

    void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
        runtimeStatus = StatusManager.Instance;
    }

    void Update()
    {
        if (!isAttacking) return;
        if (Time.time >= attackEndsAt || runtimeStatus == null || runtimeStatus.currentWeapon == null ||
            runtimeStatus.currentWeapon.weaponType != WeaponType.Melee)
            EndAttack();
    }

    public void BeginAttack(float duration)
    {
        EndAttack();
        if (!isActiveAndEnabled || hitCollider == null || duration <= 0f) return;
        if (runtimeStatus == null) runtimeStatus = StatusManager.Instance;
        if (runtimeStatus == null || runtimeStatus.currentWeapon == null ||
            runtimeStatus.currentWeapon.weaponType != WeaponType.Melee) return;

        isAttacking = true;
        attackEndsAt = Time.time + duration;
        hitCollider.enabled = true;
    }

    private void EndAttack()
    {
        isAttacking = false;
        if (hitCollider != null) hitCollider.enabled = false;
    }

    private void OnDisable()
    {
        EndAttack();
    }

    public void ApplyWeaponData(WeaponDataSO weapon)
    {
        if (weapon == null || weapon.weaponType != WeaponType.Melee)
        {
            EndAttack();
            return;
        }
        if (hitCollider == null) return;

        float range = Mathf.Max(0.1f, weapon.attackRange);
        hitCollider.size = new Vector2(baseColliderSize.x, baseColliderSize.y * range);
        hitCollider.offset = new Vector2(baseColliderOffset.x, baseColliderOffset.y * range);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 迎撃も攻撃中だけ受け付け、終了直後に残った物理通知は無視する。
        if (!isAttacking || Time.time >= attackEndsAt ||
            runtimeStatus == null || runtimeStatus.currentWeapon == null) return;

        // 近接武器でのみダメージ判定
        if (other.CompareTag("Enemy") && runtimeStatus.currentWeapon.weaponType == WeaponType.Melee)
        {
            float damage = runtimeStatus.GetWeaponDamage(runtimeStatus.currentWeapon);

            EnemyController ec = other.GetComponent<EnemyController>();
            if (ec != null)
            {
                if (ec.HP <= 0f) return;
                Vector2 hitPosition = other.ClosestPoint(hitCollider != null ? hitCollider.bounds.center : transform.position);
                if (hitCollider != null)
                {
                    ColliderDistance2D contact = other.Distance(hitCollider);
                    if (contact.isValid) hitPosition = contact.pointA;
                }
                ec.TakeDamage(damage, hitPosition);
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
