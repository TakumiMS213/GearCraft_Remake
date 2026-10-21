using UnityEngine;

/// <summary>
/// BB-413: 上下に揺れながら前進し、雑魚召喚と撃ち落とせる強攻撃で牽制する。
/// 強攻撃は予告を挟み、プレイヤーが迎撃する時間を確保する。
/// </summary>
public class EnemyAI_BossArtillery : MonoBehaviour, IEnemyAI
{
    private const float SwayAmplitude = 1.1f;
    private const float SwayPeriod = 4f;
    private const float AttackWindup = 0.6f;
    private const float ProjectileReactionTime = 0.75f;
    private const float ProjectileGateClearance = 0.1f;
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform playerTarget;
    private BossArena arena;
    private Collider2D gateCollider;
    private SpriteRenderer sprite;
    private Color originalColor;
    private float elapsed;
    private float attackTimer;
    private float windupTimer;
    private bool windingUp;
    private float summonTimer;
    private int summonedCount;
    private float runtimeMaxHp;
    private int phase;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;
        runtimeMaxHp = owner.HP;
        attackTimer = Mathf.Max(1f, data.attackInterval);
        summonTimer = Mathf.Max(1f, data.summonInterval);
        arena = new BossArena(owner);
        owner.transform.position = arena.GetPosition(1f, 0.6f);
        sprite = owner.GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) originalColor = sprite.color;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTarget = player.transform;
    }

    public void UpdateAI()
    {
        if (owner == null || owner.HP <= 0f || owner.IsStunned) return;
        float hpRatio = runtimeMaxHp > 0f ? owner.HP / runtimeMaxHp : 1f;
        phase = hpRatio <= 0.3f ? 2 : hpRatio <= 0.6f ? 1 : 0;

        elapsed += Time.deltaTime;
        Rect rect = arena.GetMovementRect();
        Vector3 position = owner.transform.position;
        // 本体だけでなく、発射した強弾にも門までの迎撃距離を残す。
        float advanceLimit = GetAdvanceLimit(rect);
        position.x = Mathf.MoveTowards(position.x, advanceLimit,
            owner.GetMoveSpeed(data.speed) * 0.3f * Time.deltaTime);
        float centerY = Mathf.Lerp(rect.yMin, rect.yMax, 0.6f);
        float amplitude = Mathf.Min(SwayAmplitude, Mathf.Min(centerY - rect.yMin, rect.yMax - centerY));
        position.y = centerY + Mathf.Sin(elapsed * Mathf.PI * 2f / SwayPeriod) * amplitude;
        owner.transform.position = arena.Clamp(position);

        UpdateSummon();
        if (windingUp)
        {
            windupTimer -= Time.deltaTime;
            if (windupTimer <= 0f)
            {
                windingUp = false;
                if (sprite != null) sprite.color = originalColor;
                LaunchCounterableProjectile();
                attackTimer = Mathf.Max(1.2f, data.attackInterval * (phase == 2 ? 0.6f : phase == 1 ? 0.8f : 1f));
            }
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f) OnAttack();
    }

    private void LateUpdate()
    {
        if (!windingUp || owner == null || !owner.isActiveAndEnabled || owner.HP <= 0f || owner.IsStunned ||
            sprite == null || !sprite.enabled) return;
        // 被弾点滅の終了処理より後に予告色を確定させる。
        sprite.color = new Color(1f, 0.45f, 0.2f, originalColor.a);
    }

    public void OnAttack()
    {
        if (owner == null || owner.HP <= 0f || owner.IsStunned || playerTarget == null || windingUp) return;
        windingUp = true;
        windupTimer = AttackWindup;
        if (sprite != null) sprite.color = new Color(1f, 0.45f, 0.2f, originalColor.a);
    }

    private void LaunchCounterableProjectile()
    {
        if (playerTarget == null) return;
        GameObject prefab = GetCounterableProjectilePrefab();
        if (prefab == null) return;
        // 本体の中から発射すると迎撃弾がボスに遮られるため、左の砲口から発射する。
        Vector3 spawnPosition = GetCounterableProjectileSpawnPosition(prefab);
        float projectileLeft;
        float projectileRight;
        GetProjectileHorizontalOffsets(prefab, out projectileLeft, out projectileRight);
        Collider2D gate = GetGateCollider();
        // 狭い画面や外力で距離を確保できない場合も、門の中・裏へ弾を生成しない。
        if (gate != null && spawnPosition.x + projectileLeft <=
            GetGateRightEdge(gate) + ProjectileGateClearance) return;
        GameObject projectile = Instantiate(prefab, spawnPosition, Quaternion.identity);
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearVelocity = (Vector2)(playerTarget.position - spawnPosition).normalized * data.bulletSpeed;
        }
        Destroy(projectile, 8f);
    }

    private float GetAdvanceLimit(Rect movementRect)
    {
        float limit = Mathf.Lerp(movementRect.xMin, movementRect.xMax, 0.35f);
        GameObject prefab = GetCounterableProjectilePrefab();
        Collider2D gate = GetGateCollider();
        if (prefab != null && gate != null)
        {
            float projectileLeft;
            float projectileRight;
            GetProjectileHorizontalOffsets(prefab, out projectileLeft, out projectileRight);
            // BossArena の初期本体オフセットと現 Transform を使い、物理同期前でも一致させる。
            float muzzleOffset = GetCounterableProjectileSpawnPosition(prefab).x - owner.transform.position.x;
            float travelDistance = Mathf.Abs(data.bulletSpeed) * ProjectileReactionTime;
            float safeLimit = GetGateRightEdge(gate) + ProjectileGateClearance + travelDistance -
                muzzleOffset - projectileLeft;
            limit = Mathf.Max(limit, safeLimit);
        }
        // 必要距離より画面が狭い場合は、本体の画面内制約を優先する。
        return Mathf.Clamp(limit, movementRect.xMin, movementRect.xMax);
    }

    private Vector3 GetCounterableProjectileSpawnPosition(GameObject prefab)
    {
        float projectileLeft;
        float projectileRight;
        GetProjectileHorizontalOffsets(prefab, out projectileLeft, out projectileRight);
        // 右端を本体の左へ出す。Collider の中心が原点からずれた弾にも対応する。
        return arena.GetLeftMuzzlePosition(projectileRight);
    }

    private static void GetProjectileHorizontalOffsets(GameObject prefab, out float left, out float right)
    {
        BoxCollider2D body = prefab.GetComponent<BoxCollider2D>();
        if (body == null)
        {
            left = -0.5f;
            right = 0.5f;
            return;
        }
        float scale = prefab.transform.localScale.x;
        float center = body.offset.x * scale;
        float halfWidth = body.size.x * Mathf.Abs(scale) * 0.5f;
        left = center - halfWidth;
        right = center + halfWidth;
    }

    private GameObject GetCounterableProjectilePrefab()
    {
        return data != null && data.bulletPrefabs != null && data.bulletPrefabs.Length > 0 ?
            data.bulletPrefabs[0] : null;
    }

    private Collider2D GetGateCollider()
    {
        if (gateCollider == null && GateManager.Instance != null)
            gateCollider = GateManager.Instance.GetComponent<Collider2D>();
        return gateCollider != null && gateCollider.enabled && gateCollider.gameObject.activeInHierarchy ?
            gateCollider : null;
    }

    private static float GetGateRightEdge(Collider2D gate)
    {
        BoxCollider2D box = gate as BoxCollider2D;
        if (box == null) return gate.bounds.max.x;
        // Main の門は BoxCollider。Transform 移動直後でも古い physics bounds を参照しない。
        Transform gateTransform = box.transform;
        float center = gateTransform.TransformPoint(box.offset).x;
        float extentX = Mathf.Abs(gateTransform.TransformVector(Vector3.right * box.size.x * 0.5f).x);
        float extentY = Mathf.Abs(gateTransform.TransformVector(Vector3.up * box.size.y * 0.5f).x);
        return center + extentX + extentY;
    }

    private void UpdateSummon()
    {
        EnemyDataSO minion = data.summonEnemyData;
        if (minion == null || minion.prefab == null || summonedCount >= data.summonCount) return;
        summonTimer -= Time.deltaTime;
        if (summonTimer > 0f) return;

        int count = phase == 2 ? 3 : phase == 1 ? 2 : 1;
        for (int i = 0; i < count && summonedCount < data.summonCount; i++)
        {
            Vector3 position = owner.transform.position + new Vector3(-1.5f, (i - (count - 1) * 0.5f) * 0.8f, 0f);
            GameObject spawned = Instantiate(minion.prefab, arena.Clamp(position), Quaternion.identity);
            EnemyController controller = spawned.GetComponent<EnemyController>();
            if (controller != null)
            {
                controller.enemyData = minion;
                controller.SetLastEnemy(false);
            }
            summonedCount++;
        }
        summonTimer = Mathf.Max(1f, data.summonInterval);
    }

    public void OnDeath()
    {
        windingUp = false;
    }
}
