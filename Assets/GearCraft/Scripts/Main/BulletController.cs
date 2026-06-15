using GearCraft.Scripts.Items;
using UnityEngine;

public class BulletController : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float bulletDamage = 10f;
    public float bulletLifeTime = 2f;
    public bool destroyOnHit = true;
    public bool DoSmoke = false;
    public bool isCannonBullet = false;

    [Header("跳弾設定")]
    public int ricochetCount = 0;      // 残り跳弾回数

    [Header("Effects (Optional)")]
    public GameObject hitEffect;
    public GameObject smoke;

    private void Start()
    {
        Destroy(gameObject, bulletLifeTime);
    }

    private void Update()
    {
        if (smoke == null || !DoSmoke) return;

        GameObject effect = Instantiate(smoke, transform.position, Quaternion.identity);
        effect.SetActive(true);
        var ps = effect.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Simulate(0, true, true);
            ps.Play();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null)
        {
            return;
        }

        if (collision.GetComponent<DroppedMaterialItem>() != null)
        {
            return;
        }

        if (collision.CompareTag("Player"))
        {
            return;
        }

        if (collision.CompareTag("Enemy"))
        {
            EnemyController ec = collision.GetComponent<EnemyController>();
            if (ec != null)
            {
                ec.TakeDamage(bulletDamage);
            }

            SpawnHitEffect();
            if (isCannonBullet)
            {
                BombDamage();
            }

            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
        }
        else if (!collision.isTrigger)
        {
            if (ricochetCount > 0)
            {
                ricochetCount--;
                ReflectBullet(collision);
                SpawnHitEffect();
                return;
            }

            SpawnHitEffect();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 壁に当たった際に弾を反射する
    /// </summary>
    private void ReflectBullet(Collider2D collision)
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb == null) return;

        // 衝突面の法線を推定（簡易的にコライダーの最近接点から計算）
        Vector2 closestPoint = collision.ClosestPoint(transform.position);
        Vector2 normal = ((Vector2)transform.position - closestPoint).normalized;

        // 法線が不正な場合はY軸反転をフォールバック
        if (normal.sqrMagnitude < 0.01f)
            normal = Vector2.up;

        Vector2 reflected = Vector2.Reflect(rb.linearVelocity, normal);
        rb.linearVelocity = reflected;

        // 弾の向きも更新
        float angle = Mathf.Atan2(reflected.y, reflected.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void SpawnHitEffect()
    {
        if (hitEffect != null)
        {
            GameObject effect = Instantiate(hitEffect, transform.position, Quaternion.identity);
            effect.SetActive(true);
            var ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Simulate(0, true, true);
                ps.Play();
            }
        }
    }

    void BombDamage()
    {
        float bombRadius = 5f;
        float bombDamage = bulletDamage;

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, bombRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy"))
            {
                EnemyController ec = hitCollider.GetComponent<EnemyController>();
                if (ec != null)
                {
                    ec.TakeDamage(bombDamage);
                }
            }
        }
    }
}
