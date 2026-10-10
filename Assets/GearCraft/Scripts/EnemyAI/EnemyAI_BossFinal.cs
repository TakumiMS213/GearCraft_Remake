using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EK-9: 巨体を接地させたまま門へ迫る地上決戦兵器。
/// 機銃掃射・重ミサイル・回転刃の押し込みを繰り返し、門の手前で継続的に破砕する。
/// </summary>
public sealed class EnemyAI_BossFinal : MonoBehaviour, IEnemyAI
{
    private const float EntryDuration = 1.2f;
    private const float GunWindup = 0.7f;
    private const float MissileWindup = 1f;
    private const float CrusherWindup = 1f;
    private const float CrusherDuration = 1.8f;
    private const float GunShotInterval = 0.12f;
    private const float MissileShotInterval = 0.55f;
    private const float GateCrushInterval = 2.5f;
    private const float GateCrushWarning = 0.85f;
    private const float StaggerDamageRatio = 0.1f;
    private const float StaggerDistance = 1f;
    private const float StaggerDuration = 0.85f;
    private const float EngineVibrationAmplitude = 0.012f;
    private const float EngineVibrationFrequency = 12f;
    private enum ActionState { Entering, Recovering, GunWindup, GunBurst, MissileWindup, MissileVolley, CrusherWindup, CrusherSurge, Staggered, Dead }

    private readonly List<GameObject> projectiles = new List<GameObject>();
    private EnemyController owner;
    private EnemyDataSO data;
    private PlayerController player;
    private Collider2D playerBody;
    private Collider2D body;
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
    private float gateCrushTimer;
    private float frontOffset;
    private float shotTimer;
    private int shotsFired;
    private int shotCount;
    private int phase;
    private int nextAttack;
    private Vector3 aimedTarget;
    private bool bladeHit;
    private bool atGate;
    private float staggerDamage;
    private float staggerStartX;
    private float staggerTargetX;
    private float vibrationTime;
    private int lastStaggerFrame = -1;
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
        sprite = owner.GetComponentInChildren<SpriteRenderer>();
        animator = owner.GetComponentInChildren<Animator>();
        weaponPorts = owner.GetComponent<FinalBossWeaponPorts>();
        visual = sprite != null && sprite.transform != owner.transform ? sprite.transform : null;
        visualRestPosition = visual != null ? visual.localPosition : Vector3.zero;
        vibrationTime = 0f;
        staggerDamage = 0f;
        lastStaggerFrame = -1;
        owner.Damaged += OnDamaged;
        originalColor = sprite != null ? sprite.color : Color.white;
        originalAnimationSpeed = animator != null ? animator.speed : 1f;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.GetComponent<PlayerController>() : null;
        playerBody = playerObject != null ? playerObject.GetComponent<Collider2D>() : null;

        phase = 0;
        nextAttack = 0;
        gateCrushTimer = GateCrushWarning;
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
        // 右端に姿を見せ、コライダーの車輪下端を床に合わせる。
        // Spriteの透明余白は接地判定に含めない。
        owner.transform.position = groundArena.Ground(visualArena.GetPosition(1f, 0f));
        SetState(ActionState.Entering);
    }

    public void UpdateAI()
    {
        if (owner == null || data == null || owner.HP <= 0f || state == ActionState.Dead) return;

        float delta = Time.deltaTime;
        if (delta <= 0f) return;
        stateElapsed += delta;
        float hpRatio = owner.HP / runtimeMaxHp;
        phase = hpRatio <= 0.3f ? 2 : hpRatio <= 0.6f ? 1 : 0;
        if (state == ActionState.Staggered)
        {
            UpdateStagger();
            UpdateVisuals();
            return;
        }
        Bounds previousBounds = GetBladeBounds();
        AdvanceOnGround(delta);
        UpdateGateCrush(delta);

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
        if (owner == null || owner.HP <= 0f || state == ActionState.Dead) return;
        staggerDamage += Mathf.Max(0f, amount);
        float threshold = runtimeMaxHp * StaggerDamageRatio;
        if (staggerDamage < threshold) return;

        // 大きな一撃の演出は一度にまとめ、閾値未満の端数だけ次へ持ち越す。
        staggerDamage %= threshold;
        if (lastStaggerFrame == Time.frameCount) return;
        lastStaggerFrame = Time.frameCount;
        staggerStartX = owner.transform.position.x;
        staggerTargetX = Mathf.Min(staggerStartX + StaggerDistance, visualArena.GetMovementRect().xMax);
        atGate = false;
        bladeHit = false;
        gateCrushTimer = GateCrushWarning;
        SetState(ActionState.Staggered);

        if (owner.deathEffect != null)
        {
            GameObject effect = Instantiate(owner.deathEffect, owner.LastHitPosition, Quaternion.identity);
            effect.transform.localScale *= 0.45f;
            foreach (ParticleSystem particle in effect.GetComponentsInChildren<ParticleSystem>())
            {
                var main = particle.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            // 本体より手前、HUDより奥。爆発は被弾の演出のみで攻撃判定を持たない。
            foreach (ParticleSystemRenderer renderer in effect.GetComponentsInChildren<ParticleSystemRenderer>())
            {
                renderer.sortingLayerID = sprite != null ? sprite.sortingLayerID : 0;
                renderer.sortingOrder = sprite != null ? sprite.sortingOrder + 2 : 3;
            }
            Destroy(effect, 6f);
        }
    }

    private void UpdateStagger()
    {
        float progress = Mathf.Clamp01(stateElapsed / StaggerDuration);
        float eased = 1f - (1f - progress) * (1f - progress);
        Vector3 position = owner.transform.position;
        // 武器側で追加されたノックバックも保持し、後退中に左へ巻き戻さない。
        position.x = Mathf.Min(Mathf.Max(position.x, Mathf.Lerp(staggerStartX, staggerTargetX, eased)),
            visualArena.GetMovementRect().xMax);
        owner.transform.position = groundArena.Ground(position);
        if (progress >= 1f) SetState(ActionState.Recovering);
    }

    private void LateUpdate()
    {
        if (visual == null || owner == null || !owner.isActiveAndEnabled || owner.HP <= 0f || state == ActionState.Dead || Time.deltaTime <= 0f) return;
        vibrationTime += Time.deltaTime;
        float offset = Mathf.Sin(vibrationTime * EngineVibrationFrequency * Mathf.PI * 2f) * EngineVibrationAmplitude;
        float parentScale = Mathf.Max(0.001f, Mathf.Abs(visual.parent.lossyScale.y));
        visual.localPosition = visualRestPosition + Vector3.up * (offset / parentScale);
    }

    public void OnAttack()
    {
        if (owner == null || owner.HP <= 0f || state != ActionState.Recovering) return;
        projectiles.RemoveAll(projectile => projectile == null);
        // 射撃は予告時点の位置を狙う。車体はプレイヤーの上下移動を追わない。
        aimedTarget = player != null ? player.transform.position : gate != null ? gate.transform.position : owner.transform.position + Vector3.left * 10f;
        if (nextAttack == 0)
        {
            SetState(ActionState.GunWindup);
        }
        else if (nextAttack == 1)
        {
            SetState(ActionState.MissileWindup);
        }
        else
        {
            SetState(ActionState.CrusherWindup);
        }
        nextAttack = (nextAttack + 1) % 3;
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

    private void FireGun()
    {
        GameObject prefab = GetProjectilePrefab(0);
        Transform muzzle = weaponPorts != null ? weaponPorts.GetGunMuzzle(shotsFired) : null;
        if (prefab == null || muzzle == null) return;
        // 筒の先端から、その筒の向きに沿って掃射。背面へ折れ曲がる射撃を避ける。
        Vector2 toTarget = aimedTarget - muzzle.position;
        float axisAngle = Mathf.Atan2(muzzle.right.y, muzzle.right.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float sweep = Mathf.Lerp(-6f, 6f, shotCount <= 1 ? 0.5f : shotsFired / (float)(shotCount - 1));
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
        GameObject prefab = GetProjectilePrefab(1);
        Transform muzzle = weaponPorts != null ? weaponPorts.GetMissileMuzzle(shotsFired) : null;
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
        advanceLimit = Mathf.Min(advanceLimit, visibleRange.xMax);
        float multiplier = state == ActionState.CrusherSurge ? 1.6f : 1f;
        float speed = owner.GetMoveSpeed(Mathf.Max(0f, data.speed) * 0.3f * multiplier);
        Vector3 position = owner.transform.position;
        // 武器のノックバックで画面外や門の奥へ押されても、その場で有効範囲へ戻す。
        position.x = Mathf.Clamp(position.x, advanceLimit, visibleRange.xMax);
        // 通常攻撃中は左進を続け、後退は累積ダメージで怯んだ時だけ行う。
        if (position.x > advanceLimit) position.x = Mathf.Max(advanceLimit, position.x - speed * delta);
        owner.transform.position = groundArena.Ground(position);
        atGate = hasGate && position.x <= advanceLimit + 0.025f;
    }

    private void UpdateGateCrush(float delta)
    {
        if (!atGate || gate == null || gate.statusManager == null)
        {
            gateCrushTimer = GateCrushWarning;
            return;
        }
        gateCrushTimer -= delta;
        if (gateCrushTimer > 0f) return;
        gateCrushTimer = GateCrushInterval;
        if (MainDebugEnemySpawnWindow.IsDebugModeActive) return;
        gate.TakeDamage(40f);
        StageFlowManager.Instance?.OnGateHit();
    }

    private void SetState(ActionState nextState)
    {
        state = nextState;
        stateElapsed = 0f;
    }

    private void UpdateVisuals()
    {
        bool crushingGate = atGate && gateCrushTimer <= GateCrushWarning;
        bool blade = state == ActionState.CrusherWindup || state == ActionState.CrusherSurge || crushingGate;
        bool warning = state == ActionState.GunWindup || state == ActionState.MissileWindup || state == ActionState.CrusherWindup || crushingGate;
        if (sprite != null)
        {
            Color warningColor = blade ? new Color(1f, 0.28f, 0.16f, originalColor.a) : new Color(1f, 0.7f, 0.25f, originalColor.a);
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
