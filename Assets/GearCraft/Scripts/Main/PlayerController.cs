using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using GearCraft.Scripts.Main;
using UnityEngine;


public class PlayerController : MonoBehaviour
{
    // --- Movement Settings ---
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    [Header("ダッシュ設定")]
    public float doubleTapWindow = 0.25f;
    public float dashSpeed = 12f;
    public float dashDuration = 0.18f;
    private bool isGrounded;
    private int lastTapDirection = 0;
    private float lastTapTime = -999f;
    private int dashDirection = 0;
    private float dashTimer = 0f;

    // --- Weapon (データ駆動) ---
    [Header("武器設定")]
    public WeaponDataSO currentWeapon;       // 現在の武器（StatusManagerから同期）
    public Transform FirePoint;

    // --- Module References ---
    public GameObject punkdrive;
    public GameObject barrier;
    public float barrierDuration = 5f;
    public float barrierCooldown = 10f;
    public bool CanBarrierUse = true;
    public bool CanUseGearCraft = true;

    // --- Player Stats ---
    private bool isInvincible;
    private int boostCount = 0;
    private float[] baseWeaponCT;
    private float boostRemainingTime = 0f;
    private bool isBoostActive = false;
    private CancellationTokenSource boostCts;
    private const float ENEMY_CONTACT_DAMAGE = 5f;

    public int HP;
    public int SAN;
    public int STR;
    public int ACC;

    // --- Internal weapon state ---
    private float coolTime;
    private float currentCoolTime;
    private float currentDamage;

    // --- Components ---
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator anim;
    public Animator armAnimator;
    public ArmRotation armRotation;
    public StatusManager runtimeStatus;
    private MeleeWeapon meleeWeapon;
    private WeaponDataSO appliedWeapon;
    private string lastArmTriggerName;

    // --- Particle ---
    public GameObject BurnPtPrehub;
    public GameObject boostEffect;
    public GameObject boostUseEffect;

    public GameObject BoostPanel;
    public AllSpritesToBlack AllBlack;

    // --- Wall ---
    private bool isTouchingWall = false;
    private Vector2 wallNormal = Vector2.zero;

    // --- Unity Events ---

    private void Start()
    {
        boostEffect = BurnPtPrehub;
        runtimeStatus = StatusManager.Instance ?? FindAnyObjectByType<StatusManager>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        meleeWeapon = armAnimator != null ? armAnimator.GetComponentInParent<MeleeWeapon>() : FindAnyObjectByType<MeleeWeapon>();
        if (runtimeStatus != null)
            runtimeStatus.UseCraftSpacebuff = false;
        isInvincible = false;

        if (runtimeStatus != null)
        {
            if (runtimeStatus.module_barrier) CanBarrierUse = true;
            HP = runtimeStatus.HP;
            SAN = runtimeStatus.SAN;
            STR = runtimeStatus.STR;
            ACC = runtimeStatus.ACC;
        }

        // 武器同期
        SyncWeaponFromStatus();

        if (FirePoint == null)
            Debug.LogError("FirePointが設定されていません", this);
    }

    private void Update()
    {
        // StatusManagerからステータス同期
        if (runtimeStatus != null)
        {
            HP = runtimeStatus.HP;
            SAN = runtimeStatus.SAN;
            STR = runtimeStatus.STR;
            ACC = runtimeStatus.ACC;
        }
        if (HP > 100) { HP = 100; }

        // 武器同期
        SyncWeaponFromStatus();

        HandleMovement();
        HandleAttack();
        CoolTimeUpdate();

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            Instantiate(BurnPtPrehub, transform.position, Quaternion.identity);
            if (currentWeapon != null && currentWeapon.weaponName == "GearCraft_Sword" && CanUseGearCraft)
            {
                ChangeGearCraft();
            }
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            UseBarrier();
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            BoostActive().Forget();
        }
        if (boostEffect != null && isBoostActive)
        {
            SpawnEffect(boostEffect, transform.position);
        }

        // Armアニメーションのトリガー
        UpdateArmAnimation();

        // PunkDrive起動/停止
        if (punkdrive != null)
            punkdrive.SetActive(runtimeStatus != null && runtimeStatus.punkDrive);
    }

    /// <summary>
    /// StatusManagerから武器情報を同期
    /// </summary>
    private void SyncWeaponFromStatus()
    {
        if (runtimeStatus != null && runtimeStatus.currentWeapon != null)
        {
            currentWeapon = runtimeStatus.currentWeapon;
            ApplyWeaponDataIfNeeded();
        }
    }

    private void ApplyWeaponDataIfNeeded()
    {
        if (currentWeapon == appliedWeapon) return;

        appliedWeapon = currentWeapon;
        lastArmTriggerName = null;

        if (meleeWeapon != null)
        {
            meleeWeapon.ApplyWeaponData(currentWeapon);
        }

    }

    /// <summary>
    /// 現在の武器がMeleeかどうか
    /// </summary>
    private bool IsMelee()
    {
        return currentWeapon != null && currentWeapon.weaponType == WeaponType.Melee;
    }

    /// <summary>
    /// 現在の武器がRangedかどうか
    /// </summary>
    private bool IsRanged()
    {
        return currentWeapon != null && currentWeapon.weaponType == WeaponType.Ranged;
    }

    // --- Arm Animation ---
    private void UpdateArmAnimation()
    {
        if (armAnimator == null || currentWeapon == null) return;

        if (!string.IsNullOrEmpty(currentWeapon.armTriggerName) && currentWeapon.armTriggerName != lastArmTriggerName)
        {
            armAnimator.SetTrigger(currentWeapon.armTriggerName);
            lastArmTriggerName = currentWeapon.armTriggerName;
        }
    }

    // --- Movement ---
    private void HandleMovement()
    {
        HandleDashInput();

        float moveInput = Input.GetAxisRaw("Horizontal");
        if (IsBlockedByWall(moveInput))
        {
            moveInput = 0;
        }

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
            if (IsBlockedByWall(dashDirection))
            {
                dashTimer = 0f;
            }
            else
            {
                rb.linearVelocity = new Vector2(dashDirection * dashSpeed, rb.linearVelocity.y);
            }
        }
        else
        {
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
        }

        anim.SetBool("run", dashTimer > 0f || moveInput != 0);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
            anim.SetBool("jump", true);
        }
        if (isGrounded) anim.SetBool("jump", false);
    }

    private void HandleDashInput()
    {
        int pressedDirection = GetHorizontalKeyDownDirection();
        if (pressedDirection == 0) return;

        if (pressedDirection == lastTapDirection && Time.time - lastTapTime <= doubleTapWindow)
        {
            StartDash(pressedDirection);
            lastTapDirection = 0;
            lastTapTime = -999f;
            return;
        }

        lastTapDirection = pressedDirection;
        lastTapTime = Time.time;
    }

    private int GetHorizontalKeyDownDirection()
    {
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            return -1;
        }

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            return 1;
        }

        return 0;
    }

    private void StartDash(int direction)
    {
        if (direction == 0 || IsBlockedByWall(direction)) return;

        dashDirection = direction;
        dashTimer = dashDuration;
    }

    private bool IsBlockedByWall(float direction)
    {
        if (!isTouchingWall || wallNormal == Vector2.zero || direction == 0f)
        {
            return false;
        }

        return Mathf.Sign(direction) == -Mathf.Sign(wallNormal.x);
    }

    // --- Attack ---
    private void HandleAttack()
    {
        if (currentWeapon == null) return;

        float weaponCoolTime = currentWeapon.coolTime;
        // 攻撃速度倍率を適用
        if (runtimeStatus != null)
            weaponCoolTime *= runtimeStatus.attackSpeedMult;

        float weaponDamage = currentWeapon.baseDamage;

        // 近接武器
        if (IsMelee() && Input.GetMouseButtonDown(0) && coolTime <= 0f)
        {
            coolTime = weaponCoolTime;
            currentDamage = weaponDamage;
            PerformMeleeAttack();
        }
        // 射撃武器
        if (IsRanged() && Input.GetMouseButton(0) && coolTime <= 0f)
        {
            coolTime = weaponCoolTime;
            currentDamage = weaponDamage;
            PerformShotAttack();
        }
    }

    private void PerformMeleeAttack()
    {
        if (armRotation != null && currentWeapon != null)
        {
            armRotation.RotateArmOnce(currentWeapon.meleeRotateDuration);
        }
    }

    private void PerformShotAttack()
    {
        if (FirePoint == null || currentWeapon == null) return;
        if (!currentWeapon.useLineHitbox && currentWeapon.bulletPrefab == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;
        Vector2 direction = (mouseWorldPos - FirePoint.position).normalized;
        float angle = Mathf.Clamp(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, -90f, 90f);

        float spread = currentWeapon.spreadAngle;
        if (runtimeStatus != null)
        {
            spread += runtimeStatus.spreadModifier;
        }

        float finalAngle = CalculateSpreadAngle(angle, spread);
        direction = new Vector2(Mathf.Cos(finalAngle * Mathf.Deg2Rad), Mathf.Sin(finalAngle * Mathf.Deg2Rad));
        Vector3 muzzlePosition = GetMuzzlePosition(direction);

        if (currentWeapon.useLineHitbox)
        {
            PerformLineHitboxAttack(direction, finalAngle, muzzlePosition);
            return;
        }

        GameObject bullet = Instantiate(currentWeapon.bulletPrefab, muzzlePosition, Quaternion.Euler(0, 0, finalAngle));
        if (bullet == null) return;

        Rigidbody2D rbBullet = bullet.GetComponent<Rigidbody2D>();
        if (rbBullet != null)
        {
            rbBullet.linearVelocity = direction * currentWeapon.bulletSpeed;
        }

        ConfigureSpawnedProjectile(bullet);

        if (runtimeStatus != null && runtimeStatus.hasBulletDouble)
        {
            float secondAngle = CalculateSpreadAngle(angle, spread);
            Vector2 dir2 = new Vector2(Mathf.Cos(secondAngle * Mathf.Deg2Rad), Mathf.Sin(secondAngle * Mathf.Deg2Rad));
            Vector3 secondMuzzlePosition = GetMuzzlePosition(dir2);
            GameObject bullet2 = Instantiate(currentWeapon.bulletPrefab, secondMuzzlePosition, Quaternion.Euler(0, 0, secondAngle));
            if (bullet2 == null) return;

            Rigidbody2D rb2 = bullet2.GetComponent<Rigidbody2D>();
            if (rb2 != null)
            {
                rb2.linearVelocity = dir2 * currentWeapon.bulletSpeed;
            }

            ConfigureSpawnedProjectile(bullet2);
        }
    }

    private void ConfigureSpawnedProjectile(GameObject bullet)
    {
        if (bullet == null || currentWeapon == null)
        {
            return;
        }

        float projectileDamage = currentDamage;
        if (runtimeStatus != null)
        {
            projectileDamage += currentWeapon.GetScaledFlatDamageBonus(runtimeStatus.ACC + runtimeStatus.bonusDamage);
            if (runtimeStatus.bulletSizeMult > 1f)
            {
                bullet.transform.localScale *= runtimeStatus.bulletSizeMult;
            }
        }

        SteamThrowerBulletController steamThrowerBullet = bullet.GetComponent<SteamThrowerBulletController>();
        if (steamThrowerBullet != null)
        {
            steamThrowerBullet.Configure(projectileDamage, currentWeapon.attackRange, null);
            return;
        }

        BulletController bulletController = bullet.GetComponent<BulletController>();
        if (bulletController == null)
        {
            return;
        }

        ApplyWeaponDataToBullet(bulletController);
        bulletController.bulletDamage = projectileDamage;

        if (runtimeStatus != null)
        {
            bulletController.ricochetCount = runtimeStatus.ricochetCount;
        }

        if (currentWeapon.isPiercing)
        {
            bulletController.destroyOnHit = false;
        }
    }


    private static float CalculateSpreadAngle(float baseAngle, float spread)
    {
        float clampedSpread = Mathf.Max(0f, spread);
        return baseAngle + UnityEngine.Random.Range(-clampedSpread, clampedSpread);
    }

    private Vector3 GetMuzzlePosition(Vector2 direction)
    {
        Vector2 normalizedDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        Vector2 perpendicular = new Vector2(-normalizedDirection.y, normalizedDirection.x);
        Vector2 offset = normalizedDirection * currentWeapon.muzzleOffset.x + perpendicular * currentWeapon.muzzleOffset.y;
        return FirePoint.position + (Vector3)offset;
    }

    private void PerformLineHitboxAttack(Vector2 direction, float angle, Vector3 muzzlePosition)
    {
        float range = Mathf.Max(0.1f, currentWeapon.attackRange);
        float width = Mathf.Max(0.01f, currentWeapon.lineHitboxWidth);
        float duration = Mathf.Max(0.01f, currentWeapon.lineHitboxDuration);
        float damage = currentDamage;

        if (runtimeStatus != null)
        {
            damage += currentWeapon.GetScaledFlatDamageBonus(runtimeStatus.ACC + runtimeStatus.bonusDamage);
        }

        Vector3 center = muzzlePosition + (Vector3)(direction.normalized * range * 0.5f);
        GameObject hitbox = new GameObject($"{currentWeapon.weaponName}_LineHitbox");
        hitbox.transform.position = center;
        hitbox.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        BoxCollider2D collider = hitbox.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(range, width);

        Rigidbody2D hitboxBody = hitbox.AddComponent<Rigidbody2D>();
        hitboxBody.bodyType = RigidbodyType2D.Kinematic;
        hitboxBody.gravityScale = 0f;

        LineHitboxController controller = hitbox.AddComponent<LineHitboxController>();
        controller.Initialize(damage, duration, collider.size, currentWeapon.lineHitboxVisualPrefab, currentWeapon.lineHitboxVisualSprite);
    }

    private void ApplyWeaponDataToBullet(BulletController bullet)
    {
        if (bullet == null || currentWeapon == null) return;

        bullet.bulletLifeTime = Mathf.Max(0.1f, currentWeapon.attackRange);
    }

    // --- CoolTime ---
    private void CoolTimeUpdate()
    {
        if (coolTime > 0f)
        {
            coolTime -= Time.deltaTime;
            if (coolTime < 0f) coolTime = 0f;
        }
    }

    // --- Damage & Invincibility ---
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Ground")) isGrounded = true;
        if (collision.collider.CompareTag("Enemy") && !isInvincible && collision.gameObject.activeSelf)
        {
            TakeDamageByBullet(ENEMY_CONTACT_DAMAGE);
            StartCoroutine(DamageCooldown());
        }
        if (collision.collider.CompareTag("Wall"))
        {
            isTouchingWall = true;
            if (collision.contacts.Length > 0)
                wallNormal = collision.contacts[0].normal;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Wall"))
        {
            isTouchingWall = false;
            wallNormal = Vector2.zero;
        }
    }

    public async void TakeDamageByBullet(float bulletDamage)
    {
        if (isInvincible) return;
        if (runtimeStatus != null)
        {
            runtimeStatus.HP -= (int)bulletDamage;
            if (runtimeStatus.HP < 0)
            {
                AllBlack.SetAllToBlack();
                runtimeStatus.punkDrive = false;
                runtimeStatus.module_barrier = false;
                runtimeStatus.module_repair = false;
                runtimeStatus.module_scrap = false;

                // 他の敵を破壊
                foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
                    if (enemy != null && enemy != this.gameObject) Destroy(enemy);
                foreach (var bullet in GameObject.FindGameObjectsWithTag("Bullet"))
                    if (bullet != null && bullet != this.gameObject) Destroy(bullet);

                var rb2d = GetComponent<Rigidbody2D>();
                if (rb2d != null)
                {
                    rb2d.linearVelocity = Vector2.zero;
                    rb2d.angularVelocity = 0f;
                    rb2d.constraints = RigidbodyConstraints2D.FreezeAll;
                }
                await UniTask.Delay(1000);
                EndingManager.LoadEndingScene(1);
            }
        }
        StartCoroutine(DamageCooldown());
    }

    private System.Collections.IEnumerator DamageCooldown()
    {
        isInvincible = true;
        if (runtimeStatus != null && runtimeStatus.module_barrier == false)
        {
            runtimeStatus.HP -= (int)ENEMY_CONTACT_DAMAGE;
        }
        float blinkDuration = 1f, blinkInterval = 0.1f, elapsedTime = 0f;
        while (elapsedTime < blinkDuration)
        {
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(blinkInterval / 2f);
            spriteRenderer.color = Color.clear;
            yield return new WaitForSeconds(blinkInterval / 2f);
            elapsedTime += blinkInterval;
        }
        spriteRenderer.color = Color.white;
        isInvincible = false;
    }

    private async void UseBarrier()
    {
        if (runtimeStatus != null && runtimeStatus.module_barrier && CanBarrierUse)
        {
            CanBarrierUse = false;
            isInvincible = true;
            barrier.SetActive(true);

            await UniTask.Delay(TimeSpan.FromSeconds(barrierDuration));
            isInvincible = false;
            barrier.SetActive(false);

            await UniTask.Delay(TimeSpan.FromSeconds(barrierCooldown));
            CanBarrierUse = true;
        }
    }

    private async UniTaskVoid BoostActive()
    {
        if (runtimeStatus == null)
        {
            runtimeStatus = StatusManager.Instance ?? FindAnyObjectByType<StatusManager>();
        }

        if (runtimeStatus == null || MaterialManager.Instance == null)
        {
            return;
        }

        if (currentWeapon == null)
        {
            return;
        }

        if (!MaterialManager.Instance.UseMaterial(MaterialManager.MaterialType.Gear, 1))
        {
            return;
        }

        if (boostUseEffect != null)
        {
            SpawnEffect(boostUseEffect, transform.position);
        }

        if (BoostPanel != null)
        {
            BoostPanel.SetActive(true);
        }

        boostRemainingTime = Mathf.Min(boostRemainingTime + 5f, 5f);
        boostCount++;

        if (isBoostActive)
        {
            return;
        }

        isBoostActive = true;
        boostCts = new CancellationTokenSource();
        float savedSpeedMult = runtimeStatus.attackSpeedMult;
        runtimeStatus.attackSpeedMult = Mathf.Max(savedSpeedMult * 0.5f, 0.05f);

        try
        {
            while (boostRemainingTime > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: boostCts.Token);
                boostRemainingTime -= 0.1f;
            }
        }
        catch (OperationCanceledException)
        {
        }

        boostCount = 0;
        isBoostActive = false;
        boostRemainingTime = 0f;
        runtimeStatus.attackSpeedMult = savedSpeedMult;

        if (BoostPanel != null)
        {
            BoostPanel.SetActive(false);
        }
    }

    async void ChangeGearCraft()
    {
        if (runtimeStatus == null || currentWeapon == null) return;

        // GearCraft_Sword→GearCraft_Axe切替（武器名で判定）
        CanUseGearCraft = false;
        if (armAnimator != null)
            armAnimator.SetFloat("speedNum", 1);

        // GearCraft_Axeに切替（StatusManagerの武器リストから検索）
        var swordWeapon = currentWeapon;
        var axeWeapon = runtimeStatus.FindOwnedWeaponByName("GearCraft_Axe");
        if (axeWeapon != null)
            runtimeStatus.EquipWeapon(axeWeapon);

        await UniTask.Delay(TimeSpan.FromSeconds(5f));
        if (armAnimator != null)
            armAnimator.SetFloat("speedNum", -1);

        // 元に戻す
        if (swordWeapon != null && runtimeStatus.ownedWeapons.Contains(swordWeapon))
            runtimeStatus.EquipWeapon(swordWeapon);

        await UniTask.Delay(TimeSpan.FromSeconds(5f));
        CanUseGearCraft = true;
    }

    /// <summary>
    /// エフェクトを生成するヘルパー
    /// </summary>
    private void SpawnEffect(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;
        GameObject effect = Instantiate(prefab, position, Quaternion.identity);
        effect.SetActive(true);
        var ps = effect.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Simulate(0, true, true);
            ps.Play();
        }
    }
}
