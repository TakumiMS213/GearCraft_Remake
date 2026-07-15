using GearCraft.Scripts.Items;
using System.Collections.Generic;
using UnityEngine;

public class BulletController : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float bulletDamage = 10f;
    public float bulletLifeTime = 2f;
    public bool destroyOnHit = true;
    public bool DoSmoke = false;
    public bool isCannonBullet = false;
    public bool useSweptHitDetection = false;
    public float sweptHitRadius = 0.08f;
    public float explosionRadiusMultiplier = 1f;
    public float directHitKnockback = 0f;
    public bool applyOverheatOnHit = false;
    public float overheatDuration = 2.5f;
    [Range(0.05f, 1f)] public float overheatSpeedMultiplier = 0.55f;
    public GameObject overheatEffectPrefab;
    public float bossDamageMultiplier = 1f;
    public float normalDamageMultiplier = 1f;

    [Header("跳弾設定")]
    public int ricochetCount = 0;      // 残り跳弾回数

    [Header("Effects (Optional)")]
    public GameObject hitEffect;
    public GameObject smoke;

    [Header("Trail (Optional)")]
    public bool configureRailCraftTrail = false;
    public float trailTime = 2f;
    public float trailWidth = 0.16f;
    public float trailMinVertexDistance = 0.002f;

    private readonly HashSet<EnemyController> damagedEnemies = new HashSet<EnemyController>();
    private Vector2 previousPosition;
    private Collider2D ignoredSurfaceAfterBounce;
    private float ignoreSurfaceUntilTime;

    private void OnEnable()
    {
        previousPosition = transform.position;
        damagedEnemies.Clear();
    }

    private void Start()
    {
        if (configureRailCraftTrail)
        {
            ConfigureRailCraftTrail();
        }

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

    private void LateUpdate()
    {
        if (!useSweptHitDetection)
        {
            previousPosition = transform.position;
            return;
        }

        ProcessSweptHits();
        previousPosition = transform.position;
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

        if (TryDamageEnemyProjectile(collision))
        {
            SpawnHitEffect();
            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
        }
        else if (TryDamageEnemy(collision))
        {
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
            if (useSweptHitDetection)
            {
                return;
            }

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

    private void ProcessSweptHits()
    {
        Vector2 currentPosition = transform.position;
        Vector2 movement = currentPosition - previousPosition;
        float distance = movement.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return;
        }

        bool previousQueriesHitTriggers = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;

        RaycastHit2D[] hits;
        try
        {
            hits = Physics2D.CircleCastAll(previousPosition, Mathf.Max(0.01f, sweptHitRadius), movement.normalized, distance);
        }
        finally
        {
            Physics2D.queriesHitTriggers = previousQueriesHitTriggers;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.gameObject == gameObject || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hitCollider.GetComponentInParent<DroppedMaterialItem>() != null || hitCollider.CompareTag("Player"))
            {
                continue;
            }

            if (TryDamageEnemyProjectile(hitCollider))
            {
                SpawnHitEffect(hit.point);
                if (destroyOnHit)
                {
                    transform.position = hit.point;
                    Destroy(gameObject);
                    return;
                }

                continue;
            }

            if (TryDamageEnemy(hitCollider))
            {
                SpawnHitEffect(hit.point);
                if (isCannonBullet)
                {
                    BombDamage();
                }

                if (destroyOnHit)
                {
                    transform.position = hit.point;
                    Destroy(gameObject);
                    return;
                }

                continue;
            }

            if (!hitCollider.isTrigger)
            {
                if (ShouldIgnoreSurfaceAfterBounce(hitCollider))
                {
                    continue;
                }

                Vector2 reflectionNormal = GetReflectionNormal(hit);
                transform.position = GetSafeBouncePosition(hit, reflectionNormal);
                if (ricochetCount > 0)
                {
                    ricochetCount--;
                    ReflectBullet(reflectionNormal);
                    IgnoreSurfaceBriefly(hitCollider);
                    SpawnHitEffect();
                    previousPosition = transform.position;
                    return;
                }

                SpawnHitEffect();
                Destroy(gameObject);
                return;
            }
        }
    }

    private bool ShouldIgnoreSurfaceAfterBounce(Collider2D hitCollider)
    {
        return hitCollider == ignoredSurfaceAfterBounce && Time.time <= ignoreSurfaceUntilTime;
    }

    private void IgnoreSurfaceBriefly(Collider2D hitCollider)
    {
        ignoredSurfaceAfterBounce = hitCollider;
        ignoreSurfaceUntilTime = Time.time + 0.05f;
    }

    private Vector2 GetReflectionNormal(RaycastHit2D hit)
    {
        if (hit.normal.sqrMagnitude > 0.01f)
        {
            return hit.normal;
        }

        Vector2 fallbackNormal = ((Vector2)transform.position - hit.point).normalized;
        if (fallbackNormal.sqrMagnitude > 0.01f)
        {
            return fallbackNormal;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            return -rb.linearVelocity.normalized;
        }

        return Vector2.up;
    }

    private Vector2 GetSafeBouncePosition(RaycastHit2D hit, Vector2 normal)
    {
        Vector2 centerAtHit = hit.centroid;
        if (centerAtHit.sqrMagnitude <= 0.0001f)
        {
            centerAtHit = hit.point + normal.normalized * Mathf.Max(0.01f, sweptHitRadius);
        }

        return centerAtHit + normal.normalized * 0.02f;
    }

    private bool TryDamageEnemy(Collider2D collision)
    {
        EnemyController enemy = collision.GetComponent<EnemyController>();
        if (enemy == null)
        {
            enemy = collision.GetComponentInParent<EnemyController>();
        }

        if (enemy == null || damagedEnemies.Contains(enemy))
        {
            return false;
        }

        damagedEnemies.Add(enemy);
        enemy.TakeDamage(GetDamageForEnemy(enemy));
        if (applyOverheatOnHit)
        {
            enemy.ApplyOverheat(overheatDuration, overheatSpeedMultiplier, overheatEffectPrefab);
        }

        ApplyDirectHitKnockback(enemy);
        return true;
    }

    private bool TryDamageEnemyProjectile(Collider2D collision)
    {
        EnemyProjectileDamageable projectile = collision.GetComponent<EnemyProjectileDamageable>();
        if (projectile == null)
        {
            projectile = collision.GetComponentInParent<EnemyProjectileDamageable>();
        }

        if (projectile == null)
        {
            return false;
        }

        projectile.TakeDamage(bulletDamage);
        return true;
    }

    private float GetDamageForEnemy(EnemyController enemy)
    {
        if (enemy != null && enemy.enemyData != null && enemy.enemyData.IsBossType)
        {
            return bulletDamage * Mathf.Max(0f, bossDamageMultiplier);
        }

        return bulletDamage * Mathf.Max(0f, normalDamageMultiplier);
    }

    private void ApplyDirectHitKnockback(EnemyController enemy)
    {
        if (enemy == null || directHitKnockback <= 0f)
        {
            return;
        }

        Rigidbody2D enemyBody = enemy.GetComponent<Rigidbody2D>();
        Rigidbody2D bulletBody = GetComponent<Rigidbody2D>();
        if (enemyBody == null || bulletBody == null || bulletBody.linearVelocity.sqrMagnitude <= 0.01f)
        {
            return;
        }

        enemyBody.MovePosition(enemyBody.position + bulletBody.linearVelocity.normalized * directHitKnockback);
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

    private void ReflectBullet(Vector2 normal)
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb == null) return;

        Vector2 reflected = Vector2.Reflect(rb.linearVelocity, normal.normalized);
        rb.linearVelocity = reflected;

        float angle = Mathf.Atan2(reflected.y, reflected.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void ConfigureRailCraftTrail()
    {
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
        }

        trail.emitting = true;
        trail.time = Mathf.Max(0.05f, trailTime);
        trail.minVertexDistance = Mathf.Max(0.001f, trailMinVertexDistance);
        trail.widthMultiplier = Mathf.Max(0.01f, trailWidth);
        trail.numCapVertices = 4;
        trail.numCornerVertices = 4;
        trail.autodestruct = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.textureMode = LineTextureMode.Stretch;
        trail.alignment = LineAlignment.View;

        AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f));
        trail.widthCurve = widthCurve;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.yellow, 0.33f),
                new GradientColorKey(new Color(1f, 0.45f, 0f), 0.66f),
                new GradientColorKey(Color.red, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f),
            });
        trail.colorGradient = gradient;

        if (trail.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                trail.sharedMaterial = new Material(shader);
            }
        }
    }

    private void SpawnHitEffect()
    {
        SpawnHitEffect(transform.position);
    }

    private void SpawnHitEffect(Vector3 position)
    {
        if (hitEffect != null)
        {
            GameObject effect = Instantiate(hitEffect, position, Quaternion.identity);
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
        float bombRadius = 5f * Mathf.Max(0.01f, explosionRadiusMultiplier);
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
                    continue;
                }

                EnemyProjectileDamageable projectile = hitCollider.GetComponent<EnemyProjectileDamageable>();
                if (projectile != null)
                {
                    projectile.TakeDamage(bombDamage);
                }
            }
        }
    }
}
