using UnityEngine;
using DG.Tweening;

/// <summary>
/// ボス2(Stage20): 高速移動・突進＋射撃型
/// 旧AR型の円弧移動と突進を組み合わせ。Phaseが進むと高速化＆突進頻度UP。
/// </summary>
public class EnemyAI_BossSpeed : MonoBehaviour, IEnemyAI
{
    private const int ArBulletBurstCount = 10;
    private const float ArBulletBurstInterval = 0.06f;
    private const float ArMissileSpeedMultiplier = 1.5f;
    private const float ChargeDurationMultiplier = 2f;
    private const float FlightMinX = -8f;
    private const float FlightMaxX = 14f;
    private const float FlightMinY = -3f;
    private const float FlightMaxY = 5f;
    private const float ReturnDistanceThreshold = 8f;

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
    private Sequence attackSequence;

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

        if (ShouldReturnToFlightArea())
        {
            MoveToReturn();
            return;
        }

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
                ShootArBulletBurst();
                break;
            case 1:
                ShootArBulletBurst();
                ShootArMissile();
                break;
            case 2:
                ShootArBulletBurst();
                ShootArMissile();
                DropArBomb();
                break;
        }
    }

    private void ShootArBulletBurst()
    {
        GameObject bulletPrefab = GetArProjectilePrefab("Bullet_AR-221", 0);
        if (bulletPrefab == null || playerTarget == null)
        {
            return;
        }

        attackSequence?.Kill();
        attackSequence = DOTween.Sequence();
        for (int i = 0; i < ArBulletBurstCount; i++)
        {
            attackSequence.AppendCallback(() => ShootArBullet(bulletPrefab));
            if (i < ArBulletBurstCount - 1)
            {
                attackSequence.AppendInterval(ArBulletBurstInterval);
            }
        }
    }

    private void ShootArBullet(GameObject bulletPrefab)
    {
        if (owner == null || playerTarget == null || bulletPrefab == null)
        {
            return;
        }

        Vector3 spawnPos = owner.transform.position;
        Vector2 dir = (playerTarget.position - spawnPos).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + Random.Range(-4f, 4f);
        dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = dir * data.bulletSpeed;
        }
    }

    private void ShootArMissile()
    {
        GameObject missilePrefab = GetArProjectilePrefab("Missile_AR-221", 1);
        if (missilePrefab == null || playerTarget == null)
        {
            return;
        }

        Vector3 spawnPos = owner.transform.position;
        Vector2 dir = (playerTarget.position - spawnPos).normalized;
        GameObject missile = Instantiate(missilePrefab, spawnPos, Quaternion.identity);
        Rigidbody2D rb = missile.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = dir * data.bulletSpeed * ArMissileSpeedMultiplier;
        }
    }

    private void DropArBomb()
    {
        GameObject bombPrefab = GetArProjectilePrefab("Bomb_AR-221", 2);
        if (bombPrefab == null)
        {
            return;
        }

        GameObject bomb = Instantiate(bombPrefab, owner.transform.position, Quaternion.identity);
        Rigidbody2D rb = bomb.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 1f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private GameObject GetArProjectilePrefab(string prefabName, int fallbackIndex)
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < data.bulletPrefabs.Length; i++)
        {
            GameObject prefab = data.bulletPrefabs[i];
            if (prefab != null && prefab.name == prefabName)
            {
                return prefab;
            }
        }

        if (fallbackIndex >= 0 && fallbackIndex < data.bulletPrefabs.Length)
        {
            return data.bulletPrefabs[fallbackIndex];
        }

        return null;
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
        seq.Append(owner.transform.DOMove(chargeTarget, chargeSpeed * ChargeDurationMultiplier).SetEase(Ease.InQuad));
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
        float radius = Mathf.Min(data.circleRadius, 2.5f);
        float duration = data.circleDuration * (phase == 2 ? 0.4f : phase == 1 ? 0.6f : 1f);
        Vector3 center = GetFlightCenter(radius);
        Vector3 relative = owner.transform.position - center;
        float currentAngle = Mathf.Atan2(relative.x, relative.y) * Mathf.Rad2Deg;
        float targetAngle = currentAngle + (reverse ? -180f : 180f);

        currentTween = DOVirtual.Float(currentAngle, targetAngle, duration, angle =>
        {
            if (owner == null) { currentTween?.Kill(); return; }
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius, 0f);
            owner.transform.position = ClampFlightPosition(center + offset);
        }).SetEase(Ease.InOutSine);
    }

    private bool ShouldReturnToFlightArea()
    {
        Vector3 position = owner.transform.position;
        if (position.x < FlightMinX ||
            position.x > FlightMaxX ||
            position.y < FlightMinY ||
            position.y > FlightMaxY)
        {
            return true;
        }

        if (returnPoint == null)
        {
            return false;
        }

        return Vector2.Distance(position, returnPoint.position) > ReturnDistanceThreshold;
    }

    private void MoveToReturn()
    {
        if (returnPoint == null)
        {
            owner.transform.position = ClampFlightPosition(owner.transform.position);
            return;
        }

        if (currentTween != null && currentTween.IsActive())
        {
            return;
        }

        float distance = Vector3.Distance(owner.transform.position, returnPoint.position);
        float moveTime = Mathf.Max(0.2f, distance / Mathf.Max(owner.GetMoveSpeed(data.speed), 0.1f));
        currentTween = owner.transform.DOMove(ClampFlightPosition(returnPoint.position), moveTime)
            .SetEase(Ease.OutSine)
            .OnComplete(() => currentTween = null);
    }

    private Vector3 GetFlightCenter(float radius)
    {
        Vector3 basePosition = returnPoint != null ? returnPoint.position : owner.transform.position;
        float randomX = Random.Range(-2f, 2f);
        float minCenterY = FlightMinY + radius;
        float maxCenterY = FlightMaxY - radius;
        float centerY = Mathf.Clamp(basePosition.y + Random.Range(-0.75f, 0.75f), minCenterY, maxCenterY);
        float centerX = Mathf.Clamp(basePosition.x + randomX, FlightMinX + radius, FlightMaxX - radius);
        return new Vector3(centerX, centerY, owner.transform.position.z);
    }

    private Vector3 ClampFlightPosition(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, FlightMinX, FlightMaxX);
        position.y = Mathf.Clamp(position.y, FlightMinY, FlightMaxY);
        position.z = owner != null ? owner.transform.position.z : position.z;
        return position;
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
        attackSequence?.Kill();
        currentTween?.Kill();
    }
}
