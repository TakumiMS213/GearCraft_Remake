using UnityEngine;
using System.Collections.Generic;

public class MeleeWeapon : MonoBehaviour
{
    private PlayerController playerController;
    private StatusManager runtimeStatus;
    private BoxCollider2D hitCollider;
    private Vector2 baseColliderSize;
    private Vector2 baseColliderOffset;
    private bool isAttacking;
    private float attackEndsAt;
    private readonly HashSet<EnemyController> hitEnemies = new HashSet<EnemyController>();
    private readonly HashSet<int> hitProjectiles = new HashSet<int>();
    private const float ExplosionRadius = 1.2f;
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
        hitEnemies.Clear();
        hitProjectiles.Clear();
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
        EndAttack();
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

            EnemyController ec = other.GetComponentInParent<EnemyController>();
            if (ec != null)
            {
                if (ec.HP <= 0f || !hitEnemies.Add(ec)) return;
                Vector2 hitPosition = other.ClosestPoint(hitCollider != null ? hitCollider.bounds.center : transform.position);
                if (hitCollider != null)
                {
                    ColliderDistance2D contact = other.Distance(hitCollider);
                    if (contact.isValid) hitPosition = contact.pointA;
                }
                var stats = runtimeStatus.meleeUpgrades;
                float directDamage = damage * GetBossMultiplier(ec, stats);
                if (ApplyMeleeDamage(ec, directDamage, hitPosition, stats))
                {
                    if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
                    Vector2 attacker = playerController != null ? (Vector2)playerController.transform.position : (Vector2)transform.position;
                    ec.ApplyMeleeKnockback(stats.knockbackDistance, attacker);
                    if (stats.explosionDamageRatio > 0f)
                        ExplodeOnHit(hitPosition, damage * stats.explosionDamageRatio, stats);
                }
                return;
            }

            EnemyProjectileDamageable projectile = other.GetComponent<EnemyProjectileDamageable>();
            if (projectile == null)
            {
                projectile = other.GetComponentInParent<EnemyProjectileDamageable>();
            }

            if (projectile != null)
            {
                if (hitProjectiles.Add(projectile.GetInstanceID())) projectile.TakeDamage(damage);
            }
        }

        // 弾丸反射（近接武器で弾を弾く）
        if (other.CompareTag("Bullet") && runtimeStatus.currentWeapon.weaponType == WeaponType.Melee)
        {
            if (!hitProjectiles.Add(other.gameObject.GetInstanceID())) return;
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

    // Starting a new swing while still overlapping must also count as a fresh hit.
    private void OnTriggerStay2D(Collider2D other) => OnTriggerEnter2D(other);

    private static float GetBossMultiplier(EnemyController enemy, MeleeUpgradeStats stats)
    {
        return enemy.enemyData != null && enemy.enemyData.IsBossType ? 1f + stats.bossDamageBonus : 1f;
    }

    private bool ApplyMeleeDamage(EnemyController enemy, float damage, Vector2 position, MeleeUpgradeStats stats)
    {
        // Only the hit that actually kills can activate or refresh the drive boost.
        // TakeMeleeDamage rejects dead enemies and blocked damage before this check.
        if (!enemy.TakeMeleeDamage(damage, position, stats.killDropBonus)) return false;
        if (enemy.HP <= 0f)
        {
            if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
            playerController?.NotifyMeleeKill();
        }
        return true;
    }

    private void ExplodeOnHit(Vector2 position, float damage, MeleeUpgradeStats stats)
    {
        if (ExplosionPrehub != null)
        {
            var effect = Instantiate(ExplosionPrehub, position, Quaternion.identity);
            effect.transform.localScale *= 0.6f;
            Destroy(effect, 3f);
        }
        var affected = new HashSet<EnemyController>();
        foreach (var collider in Physics2D.OverlapCircleAll(position, ExplosionRadius))
        {
            var enemy = collider.GetComponentInParent<EnemyController>();
            if (enemy == null || enemy.HP <= 0f || !affected.Add(enemy)) continue;
            // Explosion damage never invokes the hit proc again, avoiding chain recursion.
            ApplyMeleeDamage(enemy, damage * GetBossMultiplier(enemy, stats),
                collider.ClosestPoint(position), stats);
        }
    }
}
