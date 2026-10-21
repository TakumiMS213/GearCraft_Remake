using UnityEngine;
using DG.Tweening;

/// <summary>
/// AR-227: 画面内で円弧移動・突進・復帰を繰り返す変則移動型。
/// 連射・ミサイルのフェーズに定期的な爆弾投下を組み合わせ、登場と突進後の復帰を保証する。
/// </summary>
public class EnemyAI_BossSpeed : MonoBehaviour, IEnemyAI, IBossStunHandler
{
    private const int ArBulletBurstCount = 10;
    private const float ArBulletBurstInterval = 0.06f;
    private const float ArMissileSpeedMultiplier = 1.5f;
    private const float ChargeDurationMultiplier = 2f;
    private const float EntryDuration = 1.25f;
    private const float ReturnDuration = 0.8f;
    private const float FirstBombDelay = 4f;
    private const float BombDropClearance = 0.15f;
    private const float GateClearance = 0.35f;
    private enum MovementState { Entering, Combat, Charging, Returning }

    private EnemyController owner;
    private EnemyDataSO data;
    private Transform playerTarget;
    private BossArena arena;
    private Collider2D gateCollider;
    private float bodyLeftOffset;
    private MovementState movementState;
    private Vector3 entryStart;
    private float entryTimer;
    private float attackTimer;
    private float bombTimer;
    private int phase = 0;
    private Tween currentTween;
    private float chargeTimer;
    private float summonTimer;
    private int summonedCount;
    private float runtimeMaxHp;
    private Sequence attackSequence;
    private bool resumeMovementAfterStun;
    private bool resumeAttackAfterStun;
    private bool stunPaused;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        StopTweens();
        this.owner = owner;
        this.data = data;
        runtimeMaxHp = owner != null ? owner.HP : data.baseHP;
        attackTimer = data.attackInterval;
        bombTimer = FirstBombDelay;
        chargeTimer = data.attackInterval * 3f;
        summonTimer = data.summonInterval;
        summonedCount = 0;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTarget = playerObj.transform;

        arena = new BossArena(owner, containSprite: true);
        Collider2D body = owner.GetComponent<Collider2D>();
        bodyLeftOffset = body != null ? body.bounds.min.x - owner.transform.position.x : 0f;
        gateCollider = GateManager.Instance != null ? GateManager.Instance.GetComponent<Collider2D>() : null;
        owner.transform.position = GetCombatPosition(1f, 0.68f);
        entryStart = owner.transform.position;
        movementState = MovementState.Entering;
        entryTimer = 0f;
    }

    public void UpdateAI()
    {
        if (owner == null || owner.HP <= 0f || owner.IsStunned) return;

        // 移動だけに減速を反映し、飛行中の付与・解除でも現在の経路を再作成しない。
        float movementSpeedMultiplier = owner.GetMoveSpeed(1f);
        if (currentTween != null && currentTween.IsActive())
            currentTween.timeScale = movementSpeedMultiplier;

        Vector3 position = owner.transform.position;
        Vector3 boundedPosition = ClampCombatPosition(position);
        // カメラ変更や外部のノックバックで範囲外に出た場合も、実行中のTweenを中断して復帰する。
        if ((position - boundedPosition).sqrMagnitude > 0.01f)
        {
            currentTween?.Kill();
            owner.transform.position = boundedPosition;
            StartReturn();
            return;
        }

        if (movementState == MovementState.Entering)
        {
            entryTimer += Time.deltaTime * movementSpeedMultiplier;
            owner.transform.position = ClampCombatPosition(Vector3.Lerp(entryStart, GetHomePosition(), entryTimer / EntryDuration));
            if (entryTimer >= EntryDuration) movementState = MovementState.Combat;
            return;
        }
        if (movementState != MovementState.Combat) return;

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

        // 爆弾はHPフェーズや通常射撃から独立して投下する。
        // 突進・復帰中はタイマーを進めず、飛行に戻ってから再開する。
        bombTimer -= Time.deltaTime;
        if (bombTimer <= 0f)
        {
            DropArBomb();
            bombTimer = phase == 2 ? 4f : phase == 1 ? 5f : 6f;
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
        if (owner == null || owner.HP <= 0f || owner.IsStunned || playerTarget == null || movementState != MovementState.Combat) return;

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
        if (owner == null || owner.HP <= 0f || owner.IsStunned || playerTarget == null || bulletPrefab == null)
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
        if (owner == null || owner.HP <= 0f || owner.IsStunned) return;
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
        if (owner == null || owner.HP <= 0f || owner.IsStunned) return;
        GameObject bombPrefab = GetArProjectilePrefab("Bomb_AR-221", 2);
        if (bombPrefab == null)
        {
            return;
        }

        GameObject bomb = Instantiate(bombPrefab, owner.transform.position, Quaternion.identity);
        Collider2D ownerBody = owner.GetComponent<Collider2D>();
        Collider2D bombBody = bomb.GetComponent<Collider2D>();
        if (ownerBody != null && bombBody != null)
        {
            // 本体の中心に重ねず、腹部の当たり判定より下から落とす。
            float bombTopOffset = bombBody.bounds.max.y - bomb.transform.position.y;
            bomb.transform.position = new Vector3(ownerBody.bounds.center.x,
                ownerBody.bounds.min.y - bombTopOffset - BombDropClearance, owner.transform.position.z);
        }
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
        movementState = MovementState.Charging;
        currentTween?.Kill();

        Vector3 chargeTarget = ClampCombatPosition(playerTarget.position);
        Vector2 pullDir = (owner.transform.position - chargeTarget).normalized;
        Vector3 pullPosition = ClampCombatPosition(owner.transform.position + (Vector3)pullDir * 1.5f);
        float duration = (phase == 2 ? 0.3f : phase == 1 ? 0.5f : 0.7f) * ChargeDurationMultiplier;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(CreateBoundedMove(pullPosition, 0.3f, Ease.OutQuad));
        sequence.AppendInterval(0.2f);
        sequence.Append(CreateBoundedMove(chargeTarget, duration, Ease.InQuad));
        sequence.OnComplete(StartReturn);
        SetMovementTween(sequence);
    }

    private void StartReturn()
    {
        movementState = MovementState.Returning;
        SetMovementTween(CreateBoundedMove(GetHomePosition(), ReturnDuration, Ease.OutSine)
            .OnComplete(() =>
            {
                movementState = MovementState.Combat;
                currentTween = null;
            }));
    }

    private void SetMovementTween(Tween tween)
    {
        currentTween = tween;
        tween.timeScale = owner.GetMoveSpeed(1f);
        // 突進完了で新しい復帰Tweenへ切り替わった場合、その参照を消さない。
        tween.OnKill(() =>
        {
            if (currentTween == tween) currentTween = null;
        });
    }

    private Tween CreateBoundedMove(Vector3 target, float duration, Ease ease)
    {
        Vector3 start = owner.transform.position;
        // Sequenceの後半では、直前の移動が終わった位置を開始点にする。
        return DOVirtual.Float(0f, 1f, duration, t =>
        {
            if (owner == null || owner.HP <= 0f || owner.IsStunned) return;
            owner.transform.position = ClampCombatPosition(Vector3.Lerp(start, target, t));
        }).SetEase(ease).OnStart(() => start = owner.transform.position);
    }

    private void MoveInArc()
    {
        if (currentTween != null && currentTween.IsActive()) return;

        Rect rect = GetCombatRect();
        float radius = Mathf.Max(0f, Mathf.Min(data.circleRadius, 2.5f, rect.width * 0.5f, rect.height * 0.5f));
        if (radius < 0.05f) return;
        Vector3 center = GetHomePosition();
        center.x = Mathf.Clamp(center.x + Random.Range(-1.5f, 1.5f), rect.xMin + radius, rect.xMax - radius);
        center.y = Mathf.Clamp(center.y + Random.Range(-0.75f, 0.75f), rect.yMin + radius, rect.yMax - radius);
        Vector3 relative = owner.transform.position - center;
        float startRadius = relative.magnitude;
        float startAngle = Mathf.Atan2(relative.y, relative.x) * Mathf.Rad2Deg;
        float turn = Random.value > 0.5f ? -180f : 180f;
        float duration = Mathf.Max(0.25f, data.circleDuration * (phase == 2 ? 0.4f : phase == 1 ? 0.6f : 1f));

        SetMovementTween(DOVirtual.Float(0f, 1f, duration, t =>
        {
            if (owner == null || owner.HP <= 0f || owner.IsStunned) return;
            float angle = (startAngle + turn * t) * Mathf.Deg2Rad;
            // 初回の更新で円周へ瞬間移動しないよう、現在の半径から滑らかに接続する。
            float currentRadius = Mathf.Lerp(startRadius, radius, t);
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * currentRadius;
            owner.transform.position = ClampCombatPosition(center + offset);
        }).SetEase(Ease.InOutSine));
    }

    private Vector3 GetHomePosition()
    {
        return GetCombatPosition(0.7f, 0.68f);
    }

    private Rect GetCombatRect()
    {
        Rect rect = arena.GetMovementRect();
        if (gateCollider == null && GateManager.Instance != null)
            gateCollider = GateManager.Instance.GetComponent<Collider2D>();
        if (gateCollider == null || !gateCollider.enabled || !gateCollider.gameObject.activeInHierarchy)
            return rect;

        // 門付近のプレイヤーへ突進しても、本体の左端が門へ入らない範囲で折り返す。
        // 画面内の制約だけでは、門のTrigger領域を安全な移動先として扱ってしまう。
        float gateRight = gateCollider.bounds.max.x;
        if (gateCollider is BoxCollider2D box)
        {
            Transform gateTransform = box.transform;
            float center = gateTransform.TransformPoint(box.offset).x;
            float extentX = Mathf.Abs(gateTransform.TransformVector(Vector3.right * box.size.x * 0.5f).x);
            float extentY = Mathf.Abs(gateTransform.TransformVector(Vector3.up * box.size.y * 0.5f).x);
            gateRight = center + extentX + extentY;
        }
        float left = Mathf.Max(rect.xMin, gateRight + GateClearance - bodyLeftOffset);
        // 極端に狭い画面でも範囲を逆転させず、門との余白を優先する。
        return Rect.MinMaxRect(left, rect.yMin, Mathf.Max(left, rect.xMax), rect.yMax);
    }

    private Vector3 ClampCombatPosition(Vector3 position)
    {
        Rect rect = GetCombatRect();
        position.x = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
        position.y = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
        position.z = owner.transform.position.z;
        return position;
    }

    private Vector3 GetCombatPosition(float horizontal, float vertical)
    {
        Rect rect = GetCombatRect();
        return new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, horizontal),
            Mathf.Lerp(rect.yMin, rect.yMax, vertical), owner.transform.position.z);
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
        StopTweens();
    }

    public void SetStunned(bool stunned)
    {
        if (stunPaused == stunned) return;
        stunPaused = stunned;
        if (stunned)
        {
            // UpdateAIの外で進む移動と連射も、残りの経路・発射間隔を保って停止する。
            resumeMovementAfterStun = currentTween != null && currentTween.IsActive() && currentTween.IsPlaying();
            resumeAttackAfterStun = attackSequence != null && attackSequence.IsActive() && attackSequence.IsPlaying();
            if (currentTween != null && currentTween.IsActive()) currentTween.Pause();
            if (attackSequence != null && attackSequence.IsActive()) attackSequence.Pause();
            return;
        }

        if (owner != null && owner.HP > 0f && owner.isActiveAndEnabled)
        {
            if (resumeMovementAfterStun && currentTween != null && currentTween.IsActive()) currentTween.Play();
            if (resumeAttackAfterStun && attackSequence != null && attackSequence.IsActive()) attackSequence.Play();
        }
        resumeMovementAfterStun = false;
        resumeAttackAfterStun = false;
        stunPaused = false;
    }

    private void OnDisable()
    {
        StopTweens();
    }

    private void OnDestroy()
    {
        StopTweens();
    }

    private void StopTweens()
    {
        attackSequence?.Kill();
        currentTween?.Kill();
        attackSequence = null;
        currentTween = null;
        resumeMovementAfterStun = false;
        resumeAttackAfterStun = false;
        stunPaused = false;
    }
}
