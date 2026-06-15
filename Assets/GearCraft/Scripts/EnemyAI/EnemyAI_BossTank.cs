using UnityEngine;
using DG.Tweening;

/// <summary>
/// ボス1(Stage10): 高HP・低速・広範囲弾幕型
/// 動きは遅いが硬い。広範囲の弾幕で制圧。Phaseが進むとバリア＆弾密度UP。
/// </summary>
public class EnemyAI_BossTank : MonoBehaviour, IEnemyAI
{
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform playerTarget;
    private float attackTimer;
    private int phase = 0;
    private Tween moveTween;
    private float runtimeMaxHp;

    // バリア状態
    private bool barrierActive = false;
    private float barrierTimer = 0f;
    private const float BARRIER_DURATION = 3f;
    private const float BARRIER_COOLDOWN = 8f;
    private float barrierCooldownTimer = 0f;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;
        runtimeMaxHp = owner != null ? owner.HP : data.baseHP;
        attackTimer = data.attackInterval;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTarget = playerObj.transform;
    }

    public void UpdateAI()
    {
        if (owner == null) return;

        // フェーズ判定
        float hpRatio = runtimeMaxHp > 0f ? owner.HP / runtimeMaxHp : 1f;
        if (hpRatio <= 0.3f) phase = 2;
        else if (hpRatio <= 0.6f) phase = 1;
        else phase = 0;

        // ゆっくり左右に移動
        if (moveTween == null || !moveTween.IsActive())
            StartSlowMove();

        // バリア管理
        UpdateBarrier();

        // 攻撃
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            OnAttack();
            float intervalMult = phase == 2 ? 0.4f : phase == 1 ? 0.6f : 1f;
            attackTimer = data.attackInterval * intervalMult;
        }
    }

    public void OnAttack()
    {
        if (playerTarget == null) return;

        switch (phase)
        {
            case 0:
                // 全方位4方向弾幕
                ShootRadial(4);
                break;
            case 1:
                // 8方向弾幕 + バリア発動
                ShootRadial(8);
                if (!barrierActive && barrierCooldownTimer <= 0f)
                    ActivateBarrier();
                break;
            case 2:
                // 12方向弾幕 + プレイヤー狙い撃ち同時
                ShootRadial(12);
                ShootAtPlayer(3);
                break;
        }
    }

    /// <summary>
    /// 全方位に均等に弾を発射
    /// </summary>
    private void ShootRadial(int bulletCount)
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0) return;

        float angleStep = 360f / bulletCount;
        Vector3 spawnPos = owner.transform.position;

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = angleStep * i;
            Vector2 dir = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );

            int prefabIdx = Random.Range(0, data.bulletPrefabs.Length);
            GameObject bullet = Instantiate(data.bulletPrefabs[prefabIdx], spawnPos, Quaternion.identity);
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = dir * data.bulletSpeed;
        }
    }

    /// <summary>
    /// プレイヤー方向に扇状射撃
    /// </summary>
    private void ShootAtPlayer(int bulletCount)
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0) return;

        Vector3 spawnPos = owner.transform.position;
        Vector2 baseDir = (playerTarget.position - spawnPos).normalized;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        float spread = 20f;

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = baseAngle - spread + (bulletCount > 1 ? spread * 2f * i / (bulletCount - 1) : 0);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            GameObject bullet = Instantiate(data.bulletPrefabs[0], spawnPos, Quaternion.identity);
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = dir * data.bulletSpeed * 1.5f;
        }
    }

    private void StartSlowMove()
    {
        float targetX = owner.transform.position.x + Random.Range(-4f, 4f);
        targetX = Mathf.Clamp(targetX, -8f, 8f);
        float speed = owner.GetMoveSpeed(data.speed) * 0.3f;
        float duration = Mathf.Abs(targetX - owner.transform.position.x) / Mathf.Max(speed, 0.1f);

        moveTween = owner.transform.DOMoveX(targetX, duration)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => moveTween = null);
    }

    private void ActivateBarrier()
    {
        barrierActive = true;
        barrierTimer = BARRIER_DURATION;
        // 視覚的にバリア表現（色変更）
        var sr = owner.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(0.5f, 0.8f, 1f, 1f);
    }

    private void UpdateBarrier()
    {
        if (barrierActive)
        {
            barrierTimer -= Time.deltaTime;
            if (barrierTimer <= 0f)
            {
                barrierActive = false;
                barrierCooldownTimer = BARRIER_COOLDOWN;
                var sr = owner.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = Color.white;
            }
        }
        else
        {
            barrierCooldownTimer -= Time.deltaTime;
        }
    }

    public void OnDeath()
    {
        moveTween?.Kill();
    }
}
