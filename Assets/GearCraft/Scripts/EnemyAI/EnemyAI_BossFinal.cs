using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EK-9: 巨体を接地させたまま門へ迫る地上決戦兵器。
/// 機銃掃射・重ミサイル・回転刃の押し込みを繰り返し、門への到達で防衛を失敗させる。
/// </summary>
public sealed class EnemyAI_BossFinal : MonoBehaviour, IEnemyAI, IBossStunHandler
{
    private const float EntryDuration = 1.2f;
    private const float EntryVisibleWidth = 1.2f;
    private const float EntryAdvanceDistance = 1f;
    private const float GunWindup = 0.7f;
    private const float MissileWindup = 1f;
    private const float CrusherWindup = 1f;
    private const float CrusherDuration = 1.8f;
    private const float GunShotInterval = 0.12f;
    private const float MissileShotInterval = 0.55f;
    private const float EngineVibrationAmplitude = 0.012f;
    private const float EngineVibrationFrequency = 12f;
    private const float CrossfireWindup = 1f;
    private const float CombinedWindup = 1.2f;
    private const float EntryClearance = 0.15f;

    [Header("Approach")]
    [SerializeField, Min(0f)] private float advanceSpeedMultiplier = 0.1f;

    [Header("Armor change at two-thirds and one-third HP")]
    [SerializeField, Min(0.1f)] private float armorTransitionDuration = 3f;
    [SerializeField, Min(0f)] private float armorMissileWindup = 0.45f;
    [SerializeField, Min(0.01f)] private float armorMissileInterval = 0.55f;
    [SerializeField] private Color armorColor = new Color(0.32f, 0.62f, 0.82f, 1f);

    private enum ActionState
    {
        Entering, Recovering, GunWindup, GunBurst, MissileWindup, MissileVolley,
        CrusherWindup, CrusherSurge, CrossfireWindup, CrossfireBurst,
        CombinedWindup, CombinedVolley, ArmorTransition, Dead
    }

    public bool IsArmored => state == ActionState.ArmorTransition;
    public int CurrentPhase => phase + 1;

    private readonly List<GameObject> projectiles = new List<GameObject>();
    private EnemyController owner;
    private EnemyDataSO data;
    private PlayerController player;
    private Collider2D playerBody;
    private Collider2D body;
    private Rigidbody2D bodyRigidbody;
    private SpriteRenderer sprite;
    private Animator animator;
    private FinalBossWeaponPorts weaponPorts;
    private BossArena groundArena;
    private BossArena visualArena;
    private GateManager gate;
    private Collider2D gateBody;
    private Color originalColor;
    private float originalAnimationSpeed = 1f;
    private float runtimeMaxHp;
    private ActionState state;
    private float stateElapsed;
    private float frontOffset;
    private float entryRightLimit;
    private bool hasEnteredArena;
    private float entrySpeed;
    private float entryTargetX;
    private float shotTimer;
    private int shotsFired;
    private int shotCount;
    private float secondaryShotTimer;
    private int secondaryShotsFired;
    private int phase;
    private int nextAttack;
    private Vector3 aimedTarget;
    private bool bladeHit;
    private bool atGate;
    private Vector3 armorPosition;
    private float vibrationTime;
    private Transform visual;
    private Vector3 visualRestPosition;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        ClearProjectiles();
        if (this.owner != null) this.owner.Damaged -= OnDamaged;
        if (visual != null) visual.localPosition = visualRestPosition;
        this.owner = owner;
        this.data = data;
        runtimeMaxHp = Mathf.Max(1f, owner.HP);
        body = owner.GetComponent<Collider2D>();
        bodyRigidbody = owner.GetComponent<Rigidbody2D>();
        sprite = owner.GetComponentInChildren<SpriteRenderer>();
        animator = owner.GetComponentInChildren<Animator>();
        weaponPorts = owner.GetComponent<FinalBossWeaponPorts>();
        visual = sprite != null && sprite.transform != owner.transform ? sprite.transform : null;
        visualRestPosition = visual != null ? visual.localPosition : Vector3.zero;
        vibrationTime = 0f;
        owner.Damaged += OnDamaged;
        originalColor = sprite != null ? sprite.color : Color.white;
        originalAnimationSpeed = animator != null ? animator.speed : 1f;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.GetComponent<PlayerController>() : null;
        playerBody = playerObject != null ? playerObject.GetComponent<Collider2D>() : null;

        phase = 0;
        nextAttack = 0;
        shotTimer = 0f;
        shotsFired = 0;
        shotCount = 0;
        bladeHit = false;
        atGate = false;
        gate = GateManager.Instance;
        gateBody = gate != null ? gate.GetComponent<Collider2D>() : null;
        groundArena = new BossArena(owner);
        visualArena = new BossArena(owner, containSprite: true);
        Bounds visualBounds = sprite != null ? sprite.bounds : body != null ? body.bounds : new Bounds(owner.transform.position, Vector3.one);
        frontOffset = visualBounds.min.x - owner.transform.position.x;
        // 巨体を一度に画面内へ収めると、右側にいるプレイヤーを車体に埋め込み、
        // 物理の押し出しで床下へ落としてしまう。車体は必ずプレイヤーの右から進入する。
        Vector3 entryPosition = visualArena.GetPosition(1f, 0f);
        Bounds entryBounds = body != null ? body.bounds : visualBounds;
        entryBounds.Encapsulate(visualBounds);
        // まず前端だけを見せ、全身を画面内へ収めるまでの距離を通常前進に残す。
        entryPosition.x += Mathf.Max(0f, entryBounds.size.x - EntryVisibleWidth);
        if (body != null && playerBody != null && playerBody.enabled && playerBody.gameObject.activeInHierarchy)
        {
            float bodyLeftOffset = body.bounds.min.x - owner.transform.position.x;
            entryPosition.x = Mathf.Max(entryPosition.x,
                playerBody.bounds.max.x + EntryClearance - bodyLeftOffset);
        }
        entryRightLimit = entryPosition.x;
        hasEnteredArena = false;
        entryTargetX = entryPosition.x - EntryAdvanceDistance;
        entrySpeed = EntryAdvanceDistance / EntryDuration;
        // Spriteの透明余白は接地判定に含めない。
        owner.transform.position = groundArena.Ground(entryPosition);
        // autoSyncTransformsが無効でも、同フレームの当たり判定は出現位置を参照する。
        Physics2D.SyncTransforms();
        SetState(ActionState.Entering);
    }

    /// <summary>
    /// EnemyControllerがHP・命中点・被弾イベントを更新する前に呼ぶ、EK専用の装甲判定。
    /// 一発の超過ダメージを次の段階へ持ち越さず、必ず二回の装甲切替を通す。
    /// </summary>
    public float FilterIncomingDamage(float amount)
    {
        if (owner == null || owner.HP <= 0f || state == ActionState.Dead || IsArmored) return 0f;
        float damage = Mathf.Max(0f, amount);
        return phase < 2 ? Mathf.Min(damage, Mathf.Max(0f, owner.HP - GetNextPhaseHp())) : damage;
    }

    private float GetNextPhaseHp()
    {
        return runtimeMaxHp * (2 - phase) / 3f;
    }

    private void BeginArmorTransition()
    {
        phase++;
        nextAttack = 0;
        armorPosition = groundArena.Ground(owner.transform.position);
        owner.transform.position = armorPosition;
        atGate = false;
        bladeHit = false;
        shotsFired = 0;
        shotTimer = armorMissileWindup;
        SetState(ActionState.ArmorTransition);
        UpdateVisuals();
    }

    private void UpdateArmorTransition(float delta)
    {
        // 装甲を閉じている間は前進・押し込み・門攻撃を止め、上部四門からだけ反撃する。
        owner.transform.position = groundArena.Ground(armorPosition);
        shotTimer -= delta;
        if (shotsFired < 4 && shotTimer <= 0f)
        {
            FireMissile(shotsFired);
            shotsFired++;
            shotTimer = Mathf.Max(0.01f, armorMissileInterval);
        }
        if (stateElapsed < armorTransitionDuration || shotsFired < 4) return;
        SetState(ActionState.Recovering);
    }

    public void UpdateAI()
    {
        if (owner == null || data == null || owner.HP <= 0f || owner.IsStunned || state == ActionState.Dead) return;

        float delta = Time.deltaTime;
        if (delta <= 0f) return;
        stateElapsed += delta;
        if (IsArmored)
        {
            UpdateArmorTransition(delta);
            UpdateVisuals();
            return;
        }
        if (!hasEnteredArena)
        {
            UpdateVisuals();
            return;
        }
        Bounds previousBounds = GetBladeBounds();
        AdvanceOnGround(delta);
        if (atGate && gate != null)
        {
            gate.OnBossReachedGate(owner);
            if (EndingManager.IsLoadingEnding) return;
        }

        switch (state)
        {
            case ActionState.Entering:
                if (stateElapsed >= EntryDuration) SetState(ActionState.Recovering);
                break;
            case ActionState.Recovering:
                if (stateElapsed >= Mathf.Max(0.75f, data.attackInterval * (phase == 2 ? 0.5f : phase == 1 ? 0.65f : 0.8f)))
                    OnAttack();
                break;
            case ActionState.GunWindup:
                if (stateElapsed >= GunWindup)
                {
                    shotCount = 16 + phase * 4;
                    StartVolley(ActionState.GunBurst);
                }
                break;
            case ActionState.GunBurst:
                UpdateVolley(delta, false);
                break;
            case ActionState.MissileWindup:
                if (stateElapsed >= MissileWindup)
                {
                    shotCount = (weaponPorts != null ? weaponPorts.MissileMuzzleCount : 4) + phase;
                    StartVolley(ActionState.MissileVolley);
                }
                break;
            case ActionState.MissileVolley:
                UpdateVolley(delta, true);
                break;
            case ActionState.CrossfireWindup:
                if (stateElapsed >= CrossfireWindup)
                {
                    shotCount = 6;
                    StartVolley(ActionState.CrossfireBurst);
                }
                break;
            case ActionState.CrossfireBurst:
                UpdateCrossfire(delta);
                break;
            case ActionState.CombinedWindup:
                if (stateElapsed >= CombinedWindup)
                {
                    shotCount = 18;
                    secondaryShotsFired = 0;
                    secondaryShotTimer = 0.3f;
                    StartVolley(ActionState.CombinedVolley);
                }
                break;
            case ActionState.CombinedVolley:
                UpdateCombinedVolley(delta);
                break;
            case ActionState.CrusherWindup:
                if (stateElapsed >= CrusherWindup)
                {
                    bladeHit = false;
                    SetState(ActionState.CrusherSurge);
                }
                break;
            case ActionState.CrusherSurge:
                DamageWithBlades(previousBounds);
                if (stateElapsed >= CrusherDuration) SetState(ActionState.Recovering);
                break;
        }

        UpdateVisuals();
    }

    private void OnDamaged(float amount)
    {
        if (owner == null || owner.HP <= 0f || state == ActionState.Dead || IsArmored) return;
        if (phase < 2 && owner.HP <= GetNextPhaseHp() + 0.001f)
        {
            BeginArmorTransition();
        }
    }

    private void FixedUpdate()
    {
        if (owner == null || owner.HP <= 0f || owner.IsStunned || hasEnteredArena || IsArmored ||
            state == ActionState.Dead || Time.timeScale <= 0f) return;

        float targetX = entryTargetX;
        Vector3 position = owner.transform.position;
        if (position.x <= targetX + 0.001f)
        {
            hasEnteredArena = true;
            if (bodyRigidbody != null) bodyRigidbody.linearVelocity = Vector2.zero;
            return;
        }

        // 物理tickごとに側面から押す。描画frameの移動を積み重ねて深く重ねない。
        float maxStep = playerBody != null ? Mathf.Max(0.05f, playerBody.bounds.size.x * 0.25f) : 0.25f;
        float step = Mathf.Min(owner.GetMoveSpeed(entrySpeed) * Time.fixedDeltaTime, maxStep);
        position.x = Mathf.MoveTowards(position.x, targetX, step);
        position = groundArena.Ground(position);
        if (bodyRigidbody != null) bodyRigidbody.MovePosition(position);
        else owner.transform.position = position;
    }

    private void LateUpdate()
    {
        // スタン中の位置・微振動はEnemyControllerに任せる。
        if (owner != null && owner.IsStunned) return;
        // 直前の被弾点滅より装甲表示を優先する。振動は従来どおりVisualだけに加える。
        if (IsArmored && owner != null && owner.HP > 0f)
        {
            // 武器側のノックバックが同じフレームに走っても、装甲中の車体は固定する。
            owner.transform.position = groundArena.Ground(armorPosition);
            UpdateVisuals();
        }
        if (visual == null || owner == null || !owner.isActiveAndEnabled || owner.HP <= 0f || state == ActionState.Dead || Time.deltaTime <= 0f) return;
        vibrationTime += Time.deltaTime;
        float offset = Mathf.Sin(vibrationTime * EngineVibrationFrequency * Mathf.PI * 2f) * EngineVibrationAmplitude;
        float parentScale = Mathf.Max(0.001f, Mathf.Abs(visual.parent.lossyScale.y));
        visual.localPosition = visualRestPosition + Vector3.up * (offset / parentScale);
    }

    public void SetStunned(bool stunned)
    {
        if (!stunned || owner == null || owner.HP <= 0f || state == ActionState.Dead || IsArmored)
            return;

        // スタンで現在の連射・押し込みを中断し、復帰後は次の予告から再開する。
        // 装甲切替はHP段階の必須処理なので、進行中ならその状態を維持する。
        shotsFired = 0;
        secondaryShotsFired = 0;
        shotTimer = 0f;
        secondaryShotTimer = 0f;
        bladeHit = false;
        SetState(ActionState.Recovering);
    }

    public void OnAttack()
    {
        if (owner == null || owner.HP <= 0f || owner.IsStunned || state != ActionState.Recovering) return;
        projectiles.RemoveAll(projectile => projectile == null);
        // 射撃は予告時点の位置を狙う。車体はプレイヤーの上下移動を追わない。
        aimedTarget = player != null ? player.transform.position : gate != null ? gate.transform.position : owner.transform.position + Vector3.left * 10f;
        if (phase == 0)
            SetState(nextAttack == 0 ? ActionState.GunWindup : nextAttack == 1 ? ActionState.MissileWindup : ActionState.CrusherWindup);
        else if (phase == 1)
            SetState(nextAttack == 0 ? ActionState.CrossfireWindup : nextAttack == 1 ? ActionState.MissileWindup : nextAttack == 2 ? ActionState.GunWindup : ActionState.CrusherWindup);
        else
            SetState(nextAttack == 0 ? ActionState.CombinedWindup : nextAttack == 1 ? ActionState.CrossfireWindup : ActionState.CrusherWindup);
        nextAttack = (nextAttack + 1) % (phase == 1 ? 4 : 3);
    }

    private void StartVolley(ActionState volleyState)
    {
        shotsFired = 0;
        shotTimer = 0f;
        SetState(volleyState);
    }

    private void UpdateVolley(float delta, bool missiles)
    {
        shotTimer -= delta;
        // フレーム落ちで弾を一度に全発出さず、一発ずつ射撃する。
        if (shotTimer > 0f) return;
        if (missiles) FireMissile();
        else FireGun();
        shotsFired++;
        shotTimer = missiles ? MissileShotInterval : GunShotInterval;
        if (shotsFired >= shotCount) SetState(ActionState.Recovering);
    }

    private void UpdateCrossfire(float delta)
    {
        shotTimer -= delta;
        if (shotTimer > 0f) return;
        // 前側と上側の別々の筒から対になる掃射。各発射口の向き±18度は守る。
        float sweep = Mathf.Lerp(-12f, 12f, shotsFired / (float)Mathf.Max(1, shotCount - 1));
        FireGunFromPort(shotsFired % 3, sweep);
        FireGunFromPort(shotsFired % 3 + 3, -sweep);
        shotsFired++;
        shotTimer = GunShotInterval * 2f;
        if (shotsFired >= shotCount) SetState(ActionState.Recovering);
    }

    private void UpdateCombinedVolley(float delta)
    {
        shotTimer -= delta;
        secondaryShotTimer -= delta;
        if (shotsFired < shotCount && shotTimer <= 0f)
        {
            FireGun();
            shotsFired++;
            shotTimer = GunShotInterval;
        }
        if (secondaryShotsFired < 4 && secondaryShotTimer <= 0f)
        {
            FireMissile(secondaryShotsFired);
            secondaryShotsFired++;
            secondaryShotTimer = MissileShotInterval;
        }
        if (shotsFired >= shotCount && secondaryShotsFired >= 4) SetState(ActionState.Recovering);
    }

    private void FireGun()
    {
        float sweep = Mathf.Lerp(-6f, 6f, shotCount <= 1 ? 0.5f : shotsFired / (float)(shotCount - 1));
        FireGunFromPort(shotsFired, sweep);
    }

    private void FireGunFromPort(int portIndex, float sweep)
    {
        GameObject prefab = GetProjectilePrefab(0);
        Transform muzzle = weaponPorts != null ? weaponPorts.GetGunMuzzle(portIndex) : null;
        if (prefab == null || muzzle == null) return;
        // 筒の先端から、その筒の向きに沿って掃射。背面へ折れ曲がる射撃を避ける。
        Vector2 toTarget = aimedTarget - muzzle.position;
        float axisAngle = Mathf.Atan2(muzzle.right.y, muzzle.right.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float offset = Mathf.Clamp(Mathf.DeltaAngle(axisAngle, targetAngle) + sweep, -18f, 18f);
        Vector2 direction = Quaternion.Euler(0f, 0f, offset) * muzzle.right;
        GameObject projectile = SpawnProjectile(prefab, muzzle.position, direction);
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearVelocity = direction * Mathf.Max(6f, data.bulletSpeed);
        }
        Destroy(projectile, 7f);
    }

    private void FireMissile()
    {
        FireMissile(shotsFired);
    }

    private void FireMissile(int portIndex)
    {
        GameObject prefab = GetProjectilePrefab(1);
        Transform muzzle = weaponPorts != null ? weaponPorts.GetMissileMuzzle(portIndex) : null;
        if (prefab == null || muzzle == null) return;
        // 上部の4連発射穴を順に使用。まず穴から外へ射出し、その後に既存の追尾を開始。
        Vector2 direction = muzzle.right;
        GameObject projectile = SpawnProjectile(prefab, muzzle.position, direction);
        FinalBossMissile missile = projectile.GetComponent<FinalBossMissile>();
        if (missile != null)
        {
            missile.Initialize(player != null ? player.transform : null, direction, Mathf.Max(1f, data.bulletSpeed * 0.42f));
        }
        else
        {
            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = direction * Mathf.Max(1f, data.bulletSpeed * 0.42f);
            }
            Destroy(projectile, 7f);
        }
    }

    private GameObject SpawnProjectile(GameObject prefab, Vector3 position, Vector2 direction)
    {
        // 既存の弾画像は左向き。
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f;
        GameObject projectile = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
        projectiles.Add(projectile);
        return projectile;
    }

    private GameObject GetProjectilePrefab(int index)
    {
        return data.bulletPrefabs != null && index < data.bulletPrefabs.Length ? data.bulletPrefabs[index] : null;
    }

    private Bounds GetBladeBounds()
    {
        Bounds bounds = body != null ? body.bounds : new Bounds(owner.transform.position, Vector3.one);
        bounds.Expand(new Vector3(0.5f, 0.35f, 0f));
        return bounds;
    }

    private void DamageWithBlades(Bounds previousBounds)
    {
        if (bladeHit || player == null || playerBody == null || !playerBody.enabled) return;
        Bounds sweptBounds = GetBladeBounds();
        sweptBounds.Encapsulate(previousBounds);
        // 通常スポナーの描画用ZとプレイヤーのZが違っても、2Dの刃は命中させる。
        Bounds targetBounds = playerBody.bounds;
        if (sweptBounds.min.x > targetBounds.max.x || sweptBounds.max.x < targetBounds.min.x ||
            sweptBounds.min.y > targetBounds.max.y || sweptBounds.max.y < targetBounds.min.y) return;
        bladeHit = true;
        player.TakeDamageByBullet(Mathf.Max(25f, data.explosionDamage));
    }

    private void AdvanceOnGround(float delta)
    {
        Rect visibleRange = visualArena.GetMovementRect();
        bool hasGate = gateBody != null && gateBody.enabled && gateBody.gameObject.activeInHierarchy;
        float advanceLimit = hasGate
            ? gateBody.bounds.max.x - frontOffset + 0.1f
            : visibleRange.xMin;
        float multiplier = state == ActionState.CrusherSurge ? 1.6f : 1f;
        float speed = owner.GetMoveSpeed(Mathf.Max(0f, data.speed) * Mathf.Max(0f, advanceSpeedMultiplier) * multiplier);
        Vector3 position = owner.transform.position;
        // 右側から進入中の車体を画面内へ瞬間移動させない。
        position.x = Mathf.Clamp(position.x, advanceLimit, Mathf.Max(advanceLimit, GetRightMovementLimit()));
        // 通常攻撃中は左進を続け、スタン中の停止はEnemyControllerが管理する。
        if (position.x > advanceLimit) position.x = Mathf.Max(advanceLimit, position.x - speed * delta);
        owner.transform.position = groundArena.Ground(position);
        atGate = hasGate && position.x <= advanceLimit + 0.025f;
    }

    private float GetRightMovementLimit()
    {
        float visibleRight = visualArena.GetMovementRect().xMax;
        return Mathf.Max(visibleRight, entryRightLimit);
    }

    private void SetState(ActionState nextState)
    {
        state = nextState;
        stateElapsed = 0f;
    }

    private void UpdateVisuals()
    {
        if (IsArmored)
        {
            if (sprite != null)
            {
                Color shield = Color.Lerp(armorColor, new Color(0.65f, 0.85f, 1f, 1f),
                    Mathf.PingPong(stateElapsed * 1.5f, 0.35f));
                shield.a = originalColor.a;
                sprite.color = shield;
            }
            if (animator != null) animator.speed = originalAnimationSpeed * 0.65f;
            return;
        }
        bool blade = state == ActionState.CrusherWindup || state == ActionState.CrusherSurge;
        bool crossfire = state == ActionState.CrossfireWindup;
        bool combined = state == ActionState.CombinedWindup;
        bool warning = state == ActionState.GunWindup || state == ActionState.MissileWindup || state == ActionState.CrusherWindup || crossfire || combined;
        // 共通の被弾点滅が終わるまでは、通常色・攻撃予告色で上書きしない。
        if (sprite != null && (owner == null || !owner.IsHitFlashing))
        {
            Color warningColor = crossfire ? new Color(0.8f, 0.45f, 1f, originalColor.a)
                : blade || combined ? new Color(1f, 0.28f, 0.16f, originalColor.a)
                : new Color(1f, 0.7f, 0.25f, originalColor.a);
            sprite.color = warning ? Color.Lerp(originalColor, warningColor, 0.55f + Mathf.PingPong(stateElapsed * 3f, 0.45f)) : originalColor;
        }
        if (animator != null) animator.speed = originalAnimationSpeed * (blade ? 2.5f : phase == 2 ? 1.5f : 1f);
    }

    public void OnDeath()
    {
        if (owner != null) owner.Damaged -= OnDamaged;
        SetState(ActionState.Dead);
        ClearProjectiles();
        if (visual != null) visual.localPosition = visualRestPosition;
        if (sprite != null) sprite.color = originalColor;
        if (animator != null) animator.speed = originalAnimationSpeed;
    }

    private void OnDisable()
    {
        OnDeath();
    }

    private void OnDestroy()
    {
        ClearProjectiles();
    }

    private void ClearProjectiles()
    {
        foreach (GameObject projectile in projectiles)
            if (projectile != null) Destroy(projectile);
        projectiles.Clear();
    }
}
