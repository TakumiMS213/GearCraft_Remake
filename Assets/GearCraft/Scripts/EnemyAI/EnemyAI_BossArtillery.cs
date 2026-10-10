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
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform playerTarget;
    private BossArena arena;
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
        sprite = owner.GetComponent<SpriteRenderer>();
        if (sprite != null) originalColor = sprite.color;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTarget = player.transform;
    }

    public void UpdateAI()
    {
        if (owner == null || owner.HP <= 0f) return;
        float hpRatio = runtimeMaxHp > 0f ? owner.HP / runtimeMaxHp : 1f;
        phase = hpRatio <= 0.3f ? 2 : hpRatio <= 0.6f ? 1 : 0;

        elapsed += Time.deltaTime;
        Rect rect = arena.GetMovementRect();
        Vector3 position = owner.transform.position;
        // 画面左側へ消えず、プレイヤーに迫った位置で攻撃を続ける。
        float advanceLimit = Mathf.Lerp(rect.xMin, rect.xMax, 0.35f);
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
        if (!windingUp || owner == null || !owner.isActiveAndEnabled || owner.HP <= 0f ||
            sprite == null || !sprite.enabled) return;
        // 被弾点滅の終了処理より後に予告色を確定させる。
        sprite.color = new Color(1f, 0.45f, 0.2f, originalColor.a);
    }

    public void OnAttack()
    {
        if (owner == null || owner.HP <= 0f || playerTarget == null || windingUp) return;
        windingUp = true;
        windupTimer = AttackWindup;
        if (sprite != null) sprite.color = new Color(1f, 0.45f, 0.2f, originalColor.a);
    }

    private void LaunchCounterableProjectile()
    {
        if (playerTarget == null || data.bulletPrefabs == null || data.bulletPrefabs.Length == 0) return;
        GameObject prefab = data.bulletPrefabs[0];
        if (prefab == null) return;
        // 本体の中から発射すると迎撃弾がボスに遮られるため、左の砲口から発射する。
        BoxCollider2D projectileBody = prefab.GetComponent<BoxCollider2D>();
        float halfWidth = projectileBody != null ? projectileBody.size.x * Mathf.Abs(prefab.transform.localScale.x) * 0.5f : 0.5f;
        Vector3 spawnPosition = arena.GetLeftMuzzlePosition(halfWidth);
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
