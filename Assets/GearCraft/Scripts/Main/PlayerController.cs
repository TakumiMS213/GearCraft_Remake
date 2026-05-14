using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;


public class PlayerController : MonoBehaviour
{
    // --- Movement Settings ---
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    private bool isGrounded;

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

        if (!string.IsNullOrEmpty(currentWeapon.armTriggerName))
        {
            armAnimator.SetTrigger(currentWeapon.armTriggerName);
        }
    }

    // --- Movement ---
    private void HandleMovement()
    {
        float moveInput = Input.GetAxisRaw("Horizontal");
        if (isTouchingWall && wallNormal != Vector2.zero && moveInput != 0)
        {
            if (Mathf.Sign(moveInput) == -Mathf.Sign(wallNormal.x))
                moveInput = 0;
        }
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
        anim.SetBool("run", moveInput != 0);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
            anim.SetBool("jump", true);
        }
        if (isGrounded) anim.SetBool("jump", false);
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
        if (FirePoint == null || currentWeapon == null || currentWeapon.bulletPrefab == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;
        Vector2 direction = (mouseWorldPos - FirePoint.position).normalized;
        float angle = Mathf.Clamp(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, -90f, 90f);

        // 拡散角度の適用
        float spread = currentWeapon.spreadAngle;
        if (runtimeStatus != null)
            spread += runtimeStatus.spreadModifier;
        float finalAngle = angle + UnityEngine.Random.Range(-spread, spread);

        direction = new Vector2(Mathf.Cos(finalAngle * Mathf.Deg2Rad), Mathf.Sin(finalAngle * Mathf.Deg2Rad));

        // 弾丸生成
        GameObject bullet = Instantiate(currentWeapon.bulletPrefab, FirePoint.position, Quaternion.Euler(0, 0, finalAngle));
        if (bullet == null) return;

        var rbBullet = bullet.GetComponent<Rigidbody2D>();
        if (rbBullet != null)
        {
            rbBullet.linearVelocity = direction * currentWeapon.bulletSpeed;
        }

        // 弾丸にダメージを設定
        var bc = bullet.GetComponent<BulletController>();
        if (bc != null && runtimeStatus != null)
        {
            bc.bulletDamage = currentDamage + runtimeStatus.ACC + runtimeStatus.bonusDamage;

            // 弾丸サイズ
            if (runtimeStatus.bulletSizeMult > 1f)
                bullet.transform.localScale *= runtimeStatus.bulletSizeMult;

            // 跳弾
            bc.ricochetCount = runtimeStatus.ricochetCount;

            // 貫通
            if (currentWeapon.isPiercing)
                bc.destroyOnHit = false;
        }

        // 弾丸倍化
        if (runtimeStatus != null && runtimeStatus.hasBulletDouble)
        {
            float secondAngle = finalAngle + UnityEngine.Random.Range(-5f, 5f);
            Vector2 dir2 = new Vector2(Mathf.Cos(secondAngle * Mathf.Deg2Rad), Mathf.Sin(secondAngle * Mathf.Deg2Rad));
            GameObject bullet2 = Instantiate(currentWeapon.bulletPrefab, FirePoint.position, Quaternion.Euler(0, 0, secondAngle));
            var rb2 = bullet2.GetComponent<Rigidbody2D>();
            if (rb2 != null) rb2.linearVelocity = dir2 * currentWeapon.bulletSpeed;
            var bc2 = bullet2.GetComponent<BulletController>();
            if (bc2 != null)
            {
                bc2.bulletDamage = currentDamage + runtimeStatus.ACC + runtimeStatus.bonusDamage;
                bc2.ricochetCount = runtimeStatus.ricochetCount;
                if (currentWeapon.isPiercing) bc2.destroyOnHit = false;
            }
        }
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
        if (MaterialManager.Instance == null) return;
        if (MaterialManager.Instance.GetMaterial(MaterialManager.MaterialType.Gear) < 1) return;
        if (currentWeapon == null) return;

        float currentCT = currentWeapon.coolTime * (runtimeStatus?.attackSpeedMult ?? 1f);
        if (currentCT <= 0.1f) return;

        if (boostUseEffect != null && isBoostActive)
            SpawnEffect(boostUseEffect, transform.position);

        BoostPanel.SetActive(true);
        MaterialManager.Instance.UseMaterial(MaterialManager.MaterialType.Gear, 1);

        boostRemainingTime = Mathf.Min(boostRemainingTime + 5f, 5f);
        boostCount++;

        // 攻撃速度バフを適用
        if (runtimeStatus != null)
            runtimeStatus.attackSpeedMult = Mathf.Max(runtimeStatus.attackSpeedMult * 0.5f, 0.1f);

        if (!isBoostActive)
        {
            isBoostActive = true;
            boostCts = new CancellationTokenSource();
            float savedSpeedMult = runtimeStatus?.attackSpeedMult ?? 1f;

            try
            {
                while (boostRemainingTime > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: boostCts.Token);
                    boostRemainingTime -= 0.1f;
                }
            }
            catch (OperationCanceledException) { }

            boostCount = 0;
            isBoostActive = false;
            boostRemainingTime = 0f;

            // 攻撃速度を元に戻す
            if (runtimeStatus != null)
                runtimeStatus.attackSpeedMult = 1f;

            BoostPanel.SetActive(false);
        }
    }

    async void ChangeGearCraft()
    {
        // GearCraft_Sword→GearCraft_Axe切替（武器名で判定）
        CanUseGearCraft = false;
        armAnimator.SetFloat("speedNum", 1);

        // GearCraft_Axeに切替（StatusManagerの武器リストから検索）
        var axeWeapon = runtimeStatus.ownedWeapons.Find(w => w.weaponName == "GearCraft_Axe");
        if (axeWeapon != null)
            runtimeStatus.EquipWeapon(axeWeapon);

        await UniTask.Delay(TimeSpan.FromSeconds(5f));
        armAnimator.SetFloat("speedNum", -1);

        // 元に戻す
        var swordWeapon = runtimeStatus.ownedWeapons.Find(w => w.weaponName == "GearCraft_Sword");
        if (swordWeapon != null)
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
