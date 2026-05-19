using UnityEngine;
using DG.Tweening;

/// <summary>
/// ラスボス(Stage31): 全パターン複合型
/// Phase0: Tank型（全方位弾幕）
/// Phase1: Speed型（突進+射撃）
/// Phase2: Artillery型（ミサイル+爆撃）
/// Phase3(HP10%以下): 全パターン同時 + 最終弾幕
/// </summary>
public class EnemyAI_BossFinal : MonoBehaviour, IEnemyAI
{
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform playerTarget;
    private float attackTimer;
    private int phase = 0;
    private Tween currentTween;
    private bool isCharging = false;
    private float chargeTimer;
    private float bombardTimer;
    private float runtimeMaxHp;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;
        runtimeMaxHp = owner != null ? owner.HP : data.baseHP;
        attackTimer = data.attackInterval;
        chargeTimer = 5f;
        bombardTimer = 10f;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTarget = playerObj.transform;
    }

    public void UpdateAI()
    {
        if (owner == null || isCharging) return;

        // フェーズ判定（4段階）
        float hpRatio = runtimeMaxHp > 0f ? owner.HP / runtimeMaxHp : 1f;
        if (hpRatio <= 0.1f) phase = 3;       // 最終形態
        else if (hpRatio <= 0.3f) phase = 2;   // Artillery
        else if (hpRatio <= 0.6f) phase = 1;   // Speed
        else phase = 0;                         // Tank

        // 通常攻撃
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            OnAttack();
            float intervalMult = phase == 3 ? 0.2f : phase == 2 ? 0.4f : phase == 1 ? 0.6f : 0.8f;
            attackTimer = data.attackInterval * intervalMult;
        }

        // Phase 1+: 突進
        if (phase >= 1)
        {
            chargeTimer -= Time.deltaTime;
            if (chargeTimer <= 0f)
            {
                PerformCharge();
                chargeTimer = phase == 3 ? 2f : phase == 2 ? 3f : 5f;
            }
        }

        // Phase 2+: 爆撃
        if (phase >= 2)
        {
            bombardTimer -= Time.deltaTime;
            if (bombardTimer <= 0f)
            {
                PerformBombardment();
                bombardTimer = phase == 3 ? 3f : 5f;
            }
        }
    }

    public void OnAttack()
    {
        if (playerTarget == null) return;

        switch (phase)
        {
            case 0:
                // Tank型: 全方位6方向
                ShootRadial(6);
                MoveInArc();
                break;
            case 1:
                // Speed型: 扇状3方向 + 円弧移動
                ShootAtPlayer(3, 15f);
                MoveInArc();
                break;
            case 2:
                // Artillery型: 全方位8方向 + プレイヤー狙い
                ShootRadial(8);
                ShootAtPlayer(2, 10f);
                break;
            case 3:
                // 最終形態: 全部同時
                ShootRadial(12);
                ShootAtPlayer(5, 25f);
                break;
        }
    }

    private void ShootRadial(int bulletCount)
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0) return;

        float angleStep = 360f / bulletCount;
        // 毎回少しずらす（回転弾幕）
        float offset = Time.time * 30f;

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = angleStep * i + offset;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            int prefabIdx = Random.Range(0, data.bulletPrefabs.Length);
            GameObject bullet = Instantiate(data.bulletPrefabs[prefabIdx], owner.transform.position, Quaternion.identity);
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = dir * data.bulletSpeed * (1f + phase * 0.3f);
        }
    }

    private void ShootAtPlayer(int bulletCount, float spreadAngle)
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0) return;

        Vector3 spawnPos = owner.transform.position;
        Vector2 baseDir = (playerTarget.position - spawnPos).normalized;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = baseAngle - spreadAngle / 2f + (bulletCount > 1 ? spreadAngle * i / (bulletCount - 1) : 0);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            GameObject bullet = Instantiate(data.bulletPrefabs[0], spawnPos, Quaternion.identity);
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = dir * data.bulletSpeed * 2f;
        }
    }

    private void PerformCharge()
    {
        if (playerTarget == null) return;
        isCharging = true;
        currentTween?.Kill();

        Vector3 chargeTarget = playerTarget.position;
        Vector2 pullDir = (owner.transform.position - chargeTarget).normalized;
        Vector3 pullPos = owner.transform.position + (Vector3)pullDir * 2f;

        Sequence seq = DOTween.Sequence();
        seq.Append(owner.transform.DOMove(pullPos, 0.2f).SetEase(Ease.OutQuad));
        seq.AppendInterval(0.15f);
        seq.Append(owner.transform.DOMove(chargeTarget, 0.4f).SetEase(Ease.InQuad));
        seq.OnComplete(() =>
        {
            isCharging = false;
            // 突進後に射撃
            ShootRadial(8);
        });
        currentTween = seq;
    }

    private void PerformBombardment()
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0 || playerTarget == null) return;

        int bombCount = phase == 3 ? 12 : 6;

        for (int i = 0; i < bombCount; i++)
        {
            Vector3 targetPos = playerTarget.position + new Vector3(
                Random.Range(-6f, 6f), 10f, 0f
            );
            int prefabIdx = Mathf.Min(data.bulletPrefabs.Length - 1, 1);
            GameObject bomb = Instantiate(data.bulletPrefabs[prefabIdx], targetPos, Quaternion.identity);
            Rigidbody2D rb = bomb.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.down * data.bulletSpeed * 2f;
                rb.gravityScale = 0.3f;
            }
            var bc = bomb.GetComponent<BulletController>();
            if (bc != null)
                bc.isCannonBullet = true;
        }
    }

    private void MoveInArc()
    {
        if (currentTween != null && currentTween.IsActive()) return;

        bool reverse = Random.value > 0.5f;
        float radius = data.circleRadius;
        float duration = data.circleDuration * (phase >= 2 ? 0.5f : 0.8f);
        Vector3 center = owner.transform.localPosition + Vector3.down * radius;
        float startAngle = reverse ? 180f : 0f;
        float endAngle = reverse ? 0f : 180f;

        currentTween = DOVirtual.Float(startAngle, endAngle, duration, angle =>
        {
            if (owner == null) { currentTween?.Kill(); return; }
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius, 0f);
            owner.transform.localPosition = center + offset;
        }).SetEase(Ease.InOutSine);
    }

    public void OnDeath()
    {
        currentTween?.Kill();
    }
}
