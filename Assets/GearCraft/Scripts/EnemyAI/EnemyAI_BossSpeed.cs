using UnityEngine;
using DG.Tweening;

/// <summary>
/// ボス2(Stage20): 高速移動・突進＋射撃型
/// 旧AR型の円弧移動と突進を組み合わせ。Phaseが進むと高速化＆突進頻度UP。
/// </summary>
public class EnemyAI_BossSpeed : MonoBehaviour, IEnemyAI
{
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform playerTarget;
    private Transform returnPoint;
    private float attackTimer;
    private int phase = 0;
    private Tween currentTween;
    private bool isCharging = false;
    private float chargeTimer;
    private float summonTimer;
    private int summonedCount;
    private float runtimeMaxHp;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;
        runtimeMaxHp = owner != null ? owner.HP : data.baseHP;
        attackTimer = data.attackInterval;
        chargeTimer = data.attackInterval * 3f;
        summonTimer = data.summonInterval;
        summonedCount = 0;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTarget = playerObj.transform;

        GameObject returnGO = GameObject.FindGameObjectWithTag("AR_returnPoint");
        if (returnGO != null)
            returnPoint = returnGO.transform;
    }

    public void UpdateAI()
    {
        if (owner == null || isCharging) return;

        // フェーズ判定
        float hpRatio = runtimeMaxHp > 0f ? owner.HP / runtimeMaxHp : 1f;
        if (hpRatio <= 0.3f) phase = 2;
        else if (hpRatio <= 0.6f) phase = 1;
        else phase = 0;

        // 通常攻撃
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            OnAttack();
            float intervalMult = phase == 2 ? 0.3f : phase == 1 ? 0.5f : 1f;
            attackTimer = data.attackInterval * intervalMult;
        }

        // 突進タイマー
        chargeTimer -= Time.deltaTime;
        if (chargeTimer <= 0f)
        {
            PerformCharge();
            float chargeCooldown = phase == 2 ? 2f : phase == 1 ? 4f : 6f;
            chargeTimer = chargeCooldown;
        }

        UpdateSummon();
    }

    public void OnAttack()
    {
        if (playerTarget == null) return;

        // 円弧移動しながら射撃
        MoveInArc();

        switch (phase)
        {
            case 0:
                ShootAtPlayer(1);
                break;
            case 1:
                // 2連射
                ShootAtPlayer(2);
                break;
            case 2:
                // 3方向 + 高速弾
                ShootAtPlayer(3);
                break;
        }
    }

    private void ShootAtPlayer(int bulletCount)
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0) return;

        Vector3 spawnPos = owner.transform.position;
        Vector2 baseDir = (playerTarget.position - spawnPos).normalized;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        float spread = 10f * (bulletCount - 1);

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = baseAngle - spread / 2f + (bulletCount > 1 ? spread * i / (bulletCount - 1) : 0);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            int prefabIdx = Random.Range(0, data.bulletPrefabs.Length);
            GameObject bullet = Instantiate(data.bulletPrefabs[prefabIdx], spawnPos, Quaternion.identity);
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                float speedMult = 1f + phase * 0.5f;
                rb.linearVelocity = dir * data.bulletSpeed * speedMult;
            }
        }
    }

    /// <summary>
    /// プレイヤーに向かって高速突進
    /// </summary>
    private void PerformCharge()
    {
        if (playerTarget == null) return;
        isCharging = true;
        currentTween?.Kill();

        Vector3 chargeTarget = playerTarget.position;
        float chargeSpeed = phase == 2 ? 0.3f : phase == 1 ? 0.5f : 0.7f;

        // 予備動作（少し引く）
        Vector2 pullDir = (owner.transform.position - chargeTarget).normalized;
        Vector3 pullPos = owner.transform.position + (Vector3)pullDir * 1.5f;

        Sequence seq = DOTween.Sequence();
        seq.Append(owner.transform.DOMove(pullPos, 0.3f).SetEase(Ease.OutQuad));
        seq.AppendInterval(0.2f);
        seq.Append(owner.transform.DOMove(chargeTarget, chargeSpeed).SetEase(Ease.InQuad));
        seq.OnComplete(() =>
        {
            isCharging = false;
            // 突進後に復帰
            if (returnPoint != null)
            {
                currentTween = owner.transform.DOMove(returnPoint.position, 1f).SetEase(Ease.OutSine);
            }
        });
        currentTween = seq;
    }

    private void MoveInArc()
    {
        if (currentTween != null && currentTween.IsActive()) return;

        bool reverse = Random.value > 0.5f;
        float radius = data.circleRadius;
        float duration = data.circleDuration * (phase == 2 ? 0.4f : phase == 1 ? 0.6f : 1f);
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

    private void UpdateSummon()
    {
        if (data == null || data.summonEnemyData == null || data.summonCount <= 0)
        {
            return;
        }

        if (summonedCount >= data.summonCount)
        {
            return;
        }

        summonTimer -= Time.deltaTime;
        if (summonTimer > 0f)
        {
            return;
        }

        int count = phase >= 2 ? 3 : phase == 1 ? 2 : 1;
        for (int i = 0; i < count && summonedCount < data.summonCount; i++)
        {
            SummonEnemy(data.summonEnemyData);
            summonedCount++;
        }

        summonTimer = Mathf.Max(1f, data.summonInterval);
    }

    private void SummonEnemy(EnemyDataSO summonData)
    {
        if (summonData == null || summonData.prefab == null || owner == null)
        {
            return;
        }

        Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.5f, 1.5f), 0f);
        GameObject spawned = Instantiate(summonData.prefab, owner.transform.position + offset, Quaternion.identity);
        EnemyController controller = spawned.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.enemyData = summonData;
            controller.SetLastEnemy(false);
        }
    }

    public void OnDeath()
    {
        currentTween?.Kill();
    }
}
