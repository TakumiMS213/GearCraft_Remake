using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GearCraft.Scripts.Items;

public class EnemyController : MonoBehaviour
{
    private const string EnemyLayerName = "Enemy";
    private const float HitFlashDuration = 0.05f;
    private const float StunShakeAmplitude = 0.025f;
    private const float StunShakeFrequency = 22f;
    private const float BossSmokeStartHpRatio = 0.5f;
    private const float BossSmokeMinEmission = 12f;
    private const float BossSmokeMaxEmission = 60f;
    private const float BossSmokeMinSizeMultiplier = 0.8f;
    private const float BossSmokeMaxSizeMultiplier = 1.8f;
    private static readonly Vector3 BossSmokeLocalOffset = Vector3.up * 0.45f;

    private static bool enemyLayerCollisionConfigured;

    [Header("敵データ（ScriptableObject）")]
    public EnemyDataSO enemyData;

    [Header("ランタイムステータス")]
    [NonSerialized]
    public float HP;
    public bool isLastEnemy = false;

    public event Action<float> Damaged;
    public bool IsHitFlashing => Time.time < hitFlashEndTime;
    public bool IsStunned => bossStun != null && bossStun.IsActive;
    public int BossStunCount => bossStun != null ? bossStun.TriggerCount : 0;
    public float BossStunTimeRemaining => bossStun != null ? bossStun.RemainingTime : 0f;
    public Vector3 LastStunHitPosition { get; private set; }
    public Vector3 LastHitPosition => hasHitPosition
        ? transform.TransformPoint(lastHitLocalPosition)
        : transform.position;

    [Header("表示設定")]
    public GameObject deathEffect;
    public GameObject highlightMarker;     // 強調表示用マーカー（ボス/ラスト敵）
    [SerializeField] private GameObject bossSmokeEffectPrefab;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private float hitFlashEndTime;
    private CameraShake cameraShake;
    private IEnemyAI currentAI;
    private float overheatEndTime;
    private float overheatSpeedMultiplier = 1f;
    private GameObject overheatEffectInstance;
    private float overheatSlipDamagePerSecond;
    private float nextOverheatSlipTime;
    private float runtimeMaxHP;
    private GameObject bossSmokeEffectInstance;
    private ParticleSystem bossSmokeParticle;
    private bool galleryEncounterRecorded;
    private Camera galleryCamera;
    private Plane[] galleryCameraPlanes;
    private Vector3 lastHitLocalPosition;
    private bool hasHitPosition;
    private BossStunState bossStun;
    private Rigidbody2D stunBody;
    private RigidbodyConstraints2D constraintsBeforeStun;
    private Animator stunAnimator;
    private float animationSpeedBeforeStun;
    private Transform stunVisual;
    private Vector3 stunVisualRestPosition;
    private Vector3 stunPosition;
    private float stunElapsed;
    private bool stunPoseCaptured;

    private void Awake()
    {
        ConfigureEnemyPhysics();
    }

    void Start()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
        cameraShake = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;
        galleryEncounterRecorded = GearCraft.Gallery.GalleryProgress.HasEncountered(enemyData);

        // EnemyDataSOからパラメータを読み込み
        if (enemyData != null)
        {
            // ステージ補正を加えたHP
            int scalingStage = StageFlowManager.Instance != null
                ? StageFlowManager.Instance.EnemyScalingStage
                : StageCounter.Instance != null ? StageCounter.Instance.StageCount : 1;
            scalingStage = Mathf.Max(1, scalingStage);
            float stageBonus = scalingStage / 10f;
            HP = enemyData.baseHP + enemyData.baseHP * stageBonus;
            runtimeMaxHP = HP;
            if (enemyData.IsBossType) bossStun = new BossStunState(runtimeMaxHP);

            // AIアタッチ
            AttachAI(enemyData.aiType);

            // 強調表示
            if (enemyData.isHighlighted && highlightMarker != null)
                highlightMarker.SetActive(true);
        }
        else
        {
            HP = 50f;
            runtimeMaxHP = HP;
        }

        stunBody = GetComponent<Rigidbody2D>();
        stunAnimator = GetComponentInChildren<Animator>();
        if (spriteRenderer != null && spriteRenderer.transform != transform)
        {
            stunVisual = spriteRenderer.transform;
            stunVisualRestPosition = stunVisual.localPosition;
        }

        // StageFlowManagerに登録
        if (StageFlowManager.Instance != null)
            StageFlowManager.Instance.RegisterEnemy(this);
    }

    void Update()
    {
        UpdateBossStun();
        if (currentAI != null && !IsStunned)
            currentAI.UpdateAI();

        UpdateOverheatStatus();
        UpdateBossSmokeEffect();
        RecordGalleryEncounterIfVisible();
    }

    private void RecordGalleryEncounterIfVisible()
    {
        if (galleryEncounterRecorded || enemyData == null || spriteRenderer == null || !spriteRenderer.enabled) return;
        if (galleryCamera == null) galleryCamera = Camera.main;
        if (galleryCamera == null || !galleryCamera.isActiveAndEnabled
            || (galleryCamera.cullingMask & (1 << gameObject.layer)) == 0) return;

        if (galleryCameraPlanes == null) galleryCameraPlanes = new Plane[6];
        GeometryUtility.CalculateFrustumPlanes(galleryCamera, galleryCameraPlanes);
        if (!GeometryUtility.TestPlanesAABB(galleryCameraPlanes, spriteRenderer.bounds)) return;

        GearCraft.Gallery.GalleryProgress.RecordEnemy(enemyData);
        galleryEncounterRecorded = true;
    }

    public float GetMoveSpeed(float baseSpeed)
    {
        if (IsStunned) return 0f;
        return baseSpeed * (Time.time < overheatEndTime ? overheatSpeedMultiplier : 1f);
    }

    private void TryStartBossStun()
    {
        if (bossStun == null || enemyData == null) return;
        bool alreadyStunned = IsStunned;
        int triggered = bossStun.ApplyDamage(HP, Mathf.Max(0.1f, enemyData.bossStunDuration));
        if (triggered == 0) return;

        LastStunHitPosition = LastHitPosition;
        if (!alreadyStunned)
        {
            stunPosition = transform.position;
            stunElapsed = 0f;
            stunPoseCaptured = true;
            if (stunBody != null)
            {
                constraintsBeforeStun = stunBody.constraints;
                stunBody.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            if (stunAnimator != null)
            {
                animationSpeedBeforeStun = stunAnimator.speed;
                stunAnimator.speed = 0f;
            }
            (currentAI as IBossStunHandler)?.SetStunned(true);
        }
        HoldStunPosition();
        PlayStunExplosion(LastStunHitPosition);
        UpdateBossSmokeEffect();
    }

    private void UpdateBossStun()
    {
        if (!IsStunned) return;
        bossStun.Tick(Time.deltaTime);
        if (!IsStunned)
        {
            ReleaseStunPose(true);
            return;
        }
        // EKの通常色更新も停止するため、共通の被弾点滅だけは終了させる。
        if (spriteRenderer != null && !IsHitFlashing && !IsFinalBossArmored)
            spriteRenderer.color = originalColor;
    }

    private void HoldStunPosition()
    {
        if (!stunPoseCaptured) return;
        transform.position = stunPosition;
        if (stunBody != null)
        {
            stunBody.linearVelocity = Vector2.zero;
            stunBody.angularVelocity = 0f;
            stunBody.position = stunPosition;
        }
    }

    private void FixedUpdate()
    {
        if (IsStunned) HoldStunPosition();
    }

    private void LateUpdate()
    {
        if (!IsStunned || HP <= 0f) return;
        HoldStunPosition();
        if (stunAnimator != null) stunAnimator.speed = 0f;
        if (stunVisual == null) return;
        stunElapsed += Time.deltaTime;
        float phase = stunElapsed * StunShakeFrequency * Mathf.PI * 2f;
        Vector3 scale = stunVisual.parent.lossyScale;
        // 本体の当たり判定は固定し、画像だけをワールド座標で数cm揺らす。
        stunVisual.localPosition = stunVisualRestPosition + new Vector3(
            Mathf.Sin(phase) * StunShakeAmplitude / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            Mathf.Sin(phase * 0.7f) * StunShakeAmplitude * 0.5f / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 0f);
    }

    private void ReleaseStunPose(bool resumeAI)
    {
        if (!stunPoseCaptured) return;
        stunPoseCaptured = false;
        if (stunBody != null) stunBody.constraints = constraintsBeforeStun;
        if (stunAnimator != null) stunAnimator.speed = animationSpeedBeforeStun;
        if (stunVisual != null) stunVisual.localPosition = stunVisualRestPosition;
        if (resumeAI) (currentAI as IBossStunHandler)?.SetStunned(false);
    }

    private void CancelBossStun()
    {
        bossStun?.Cancel();
        ReleaseStunPose(false);
    }

    private void OnDisable()
    {
        CancelBossStun();
    }

    private void PlayStunExplosion(Vector3 position)
    {
        if (deathEffect == null) return;
        GameObject effect = Instantiate(deathEffect, position, Quaternion.identity);
        effect.name = "BossStunExplosion";
        // 撃破時より小さな局所爆発にし、停止・煙・回避すべき弾を隠さない。
        effect.transform.localScale *= 0.22f;
        foreach (ParticleSystem particle in effect.GetComponentsInChildren<ParticleSystem>())
        {
            var main = particle.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        foreach (ParticleSystemRenderer renderer in effect.GetComponentsInChildren<ParticleSystemRenderer>())
        {
            renderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
            renderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 2 : 3;
        }
        Destroy(effect, 6f);
    }

    public void ApplyOverheat(float duration, float speedMultiplier, GameObject effectPrefab)
    {
        if (duration <= 0f)
        {
            return;
        }

        overheatEndTime = Mathf.Max(overheatEndTime, Time.time + duration);
        overheatSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.05f, 1f);

        if (effectPrefab != null && overheatEffectInstance == null)
        {
            overheatEffectInstance = Instantiate(effectPrefab, transform);
            overheatEffectInstance.transform.localPosition = Vector3.zero;
        }
    }

    public void ApplyOverheatSlipDamage(float damagePerSecond)
    {
        overheatSlipDamagePerSecond = Mathf.Max(overheatSlipDamagePerSecond, damagePerSecond);
        if (nextOverheatSlipTime <= Time.time)
        {
            nextOverheatSlipTime = Time.time + 1f;
        }
    }

    private void ConfigureEnemyPhysics()
    {
        int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        if (enemyLayer >= 0 && !enemyLayerCollisionConfigured)
        {
            Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
            enemyLayerCollisionConfigured = true;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            bool usesGravity = IsGravityEnemy();
            rb.bodyType = usesGravity ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            rb.gravityScale = usesGravity ? 1f : 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearVelocity = Vector2.zero;
        }

        DroppedMaterialItem.IgnoreCollisionWithExistingDrops(GetComponent<Collider2D>());
    }

    private bool IsGravityEnemy()
    {
        string nameSource = enemyData != null ? enemyData.enemyName : gameObject.name;
        if (string.IsNullOrEmpty(nameSource))
        {
            return false;
        }

        return nameSource.StartsWith("G-", StringComparison.OrdinalIgnoreCase) ||
            nameSource.StartsWith("EG-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AIタイプに応じたコンポーネントをアタッチ
    /// </summary>
    private void AttachAI(EnemyAIType aiType)
    {
        switch (aiType)
        {
            case EnemyAIType.Straight:
                currentAI = gameObject.AddComponent<EnemyAI_Straight>();
                break;
            case EnemyAIType.CircleMove:
                currentAI = gameObject.AddComponent<EnemyAI_Circle>();
                break;
            case EnemyAIType.Sniper:
                currentAI = gameObject.AddComponent<EnemyAI_Sniper>();
                break;
            case EnemyAIType.Charger:
                currentAI = gameObject.AddComponent<EnemyAI_Charger>();
                break;
            case EnemyAIType.Bomber:
                currentAI = gameObject.AddComponent<EnemyAI_Bomber>();
                break;
            case EnemyAIType.Boss_Tank:
                currentAI = gameObject.AddComponent<EnemyAI_BossTank>();
                break;
            case EnemyAIType.Boss_Speed:
                currentAI = gameObject.AddComponent<EnemyAI_BossSpeed>();
                break;
            case EnemyAIType.Boss_Artillery:
                currentAI = gameObject.AddComponent<EnemyAI_BossArtillery>();
                break;
            case EnemyAIType.Boss_Final:
                currentAI = gameObject.AddComponent<EnemyAI_BossFinal>();
                break;
        }

        if (currentAI != null)
            currentAI.Initialize(this, enemyData);
    }

    // ---- ダメージ処理 ----

    public void TakeDamage(float amount, Vector2 hitPosition)
    {
        if (!PrepareFinalBossDamage(ref amount)) return;
        if (amount > 0f && HP > 0f)
        {
            // 2Dの命中点を本体のZへ合わせる。継続ダメージ中も同じ被弾箇所を保つ。
            Vector3 point = new Vector3(hitPosition.x, hitPosition.y, transform.position.z);
            lastHitLocalPosition = transform.InverseTransformPoint(point);
            hasHitPosition = true;
        }
        ApplyDamage(amount);
    }

    public void TakeDamage(float amount)
    {
        if (!PrepareFinalBossDamage(ref amount)) return;
        ApplyDamage(amount);
    }

    private bool PrepareFinalBossDamage(ref float amount)
    {
        // 他の敵のダメージ・被弾イベントはそのまま。EKだけ装甲と段階境界で制限する。
        EnemyAI_BossFinal finalBoss = currentAI as EnemyAI_BossFinal;
        if (finalBoss == null) return true;
        amount = finalBoss.FilterIncomingDamage(amount);
        return amount > 0f;
    }

    private bool IsFinalBossArmored => currentAI is EnemyAI_BossFinal finalBoss && finalBoss.IsArmored;

    private void ApplyDamage(float amount)
    {
        RecordGalleryEncounterIfVisible();
        HP -= amount;

        if (HP <= 0f)
        {
            Die().Forget();
        }
        else
        {
            if (amount > 0f)
            {
                TryStartBossStun();
                Damaged?.Invoke(amount);
            }
            // 段階境界のDamagedで装甲が閉じた場合も、装甲色を被弾点滅で上書きしない。
            if (spriteRenderer != null && !IsFinalBossArmored)
            {
                spriteRenderer.color = originalColor;
                StartCoroutine(FlashOnHit());
            }
        }
    }

    public async UniTaskVoid Die()
    {
        Vector3 deathPos = transform.position;
        var deathToken = this.GetCancellationTokenOnDestroy();
        StageFlowManager deathFlow = StageFlowManager.Instance;

        // 見た目と当たり判定を即座に消す
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null || !spriteRenderer.enabled) return;
        spriteRenderer.enabled = false;
        CancelBossStun();

        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;

        // AIの死亡処理
        if (currentAI != null)
            currentAI.OnDeath();

        if (overheatEffectInstance != null)
            Destroy(overheatEffectInstance);

        if (bossSmokeEffectInstance != null)
            Destroy(bossSmokeEffectInstance);

        // エフェクト再生
        if (deathEffect != null)
        {
            var effect = Instantiate(deathEffect, deathPos, Quaternion.identity);
            var ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Play();

            // カメラシェイク
            if (cameraShake != null)
            {
                try
                {
                    if (enemyData != null && enemyData.IsBossType)
                        await cameraShake.ShakeAsync(1f, 3f, 10, 90f).AttachExternalCancellation(deathToken);
                    else
                        await cameraShake.ShakeAsync(0.2f, 0.6f, 10, 90f).AttachExternalCancellation(deathToken);
                }
                catch (System.OperationCanceledException) when (deathToken.IsCancellationRequested) { return; }
            }
        }

        // 演出中にシーン移動・プレイヤー死亡で破棄された敵は、新しい日の状態を触らない。
        if (this == null || deathToken.IsCancellationRequested) return;

        // 素材ドロップ
        if (enemyData != null && enemyData.drops != null && MaterialDropper.Instance != null)
        {
            int stageNum = StageCounter.Instance != null ? StageCounter.Instance.StageCount : 1;
            var stageConfig = deathFlow != null ? deathFlow.stageConfig : null;
            if (stageConfig != null)
                MaterialDropper.Instance.DropMaterialsWithStageBonus(deathPos, enemyData.drops, stageConfig, stageNum);
            else
                MaterialDropper.Instance.DropMaterials(deathPos, enemyData.drops);
        }

        // StageFlowManagerに通知（ステージ進行を委譲）
        if (deathFlow != null)
        {
            deathFlow.OnEnemyKilled(this, isLastEnemy);
        }

        Destroy(gameObject);
    }

    private System.Collections.IEnumerator FlashOnHit()
    {
        if (IsFinalBossArmored) yield break;
        hitFlashEndTime = Time.time + HitFlashDuration;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(HitFlashDuration);
        // 連続被弾では、先に開始した点滅が最新の被弾表示を消さないようにする。
        // EKは自身のUpdateVisualsで現在の予告色・装甲色へ戻す。
        if (spriteRenderer != null && !IsHitFlashing && !(currentAI is EnemyAI_BossFinal))
            spriteRenderer.color = originalColor;
    }

    private void UpdateOverheatStatus()
    {
        if (Time.time < overheatEndTime && overheatSlipDamagePerSecond > 0f && Time.time >= nextOverheatSlipTime)
        {
            nextOverheatSlipTime = Time.time + 1f;
            TakeDamage(overheatSlipDamagePerSecond);
        }

        if (overheatEffectInstance == null || Time.time < overheatEndTime)
        {
            return;
        }

        Destroy(overheatEffectInstance);
        overheatEffectInstance = null;
        overheatSpeedMultiplier = 1f;
        overheatSlipDamagePerSecond = 0f;
    }

    private void UpdateBossSmokeEffect()
    {
        // 死亡演出の待機中もUpdateは続くため、煙を再生成させない。
        if (HP <= 0f || spriteRenderer == null || !spriteRenderer.enabled)
        {
            DestroyBossSmokeEffect();
            return;
        }

        if (enemyData == null || !enemyData.IsBossType || runtimeMaxHP <= 0f)
        {
            return;
        }

        float hpRatio = Mathf.Clamp01(HP / runtimeMaxHP);
        if (hpRatio > BossSmokeStartHpRatio && !IsStunned)
        {
            DestroyBossSmokeEffect();
            return;
        }

        if (bossSmokeEffectInstance == null)
        {
            bossSmokeEffectInstance = CreateBossSmokeEffect();
            bossSmokeParticle = bossSmokeEffectInstance != null
                ? bossSmokeEffectInstance.GetComponentInChildren<ParticleSystem>()
                : null;
        }

        UpdateBossSmokeFollow();
        ApplyBossSmokeIntensity(IsStunned ? 0f : hpRatio);
    }

    private void DestroyBossSmokeEffect()
    {
        if (bossSmokeEffectInstance == null)
        {
            bossSmokeParticle = null;
            return;
        }

        Destroy(bossSmokeEffectInstance);
        bossSmokeEffectInstance = null;
        bossSmokeParticle = null;
    }

    private void ApplyBossSmokeIntensity(float hpRatio)
    {
        if (bossSmokeParticle == null)
        {
            return;
        }

        float damageRatioBelowHalf = Mathf.InverseLerp(BossSmokeStartHpRatio, 0f, hpRatio);
        float emissionRate = Mathf.Lerp(BossSmokeMinEmission, BossSmokeMaxEmission, damageRatioBelowHalf);
        float sizeMultiplier = Mathf.Lerp(BossSmokeMinSizeMultiplier, BossSmokeMaxSizeMultiplier, damageRatioBelowHalf);

        ParticleSystem.EmissionModule emission = bossSmokeParticle.emission;
        emission.rateOverTime = emissionRate;

        ParticleSystem.MainModule main = bossSmokeParticle.main;
        main.maxParticles = Mathf.RoundToInt(Mathf.Lerp(50f, 180f, damageRatioBelowHalf));

        ParticleSystem.SizeOverLifetimeModule size = bossSmokeParticle.sizeOverLifetime;
        size.sizeMultiplier = sizeMultiplier;
    }

    private void UpdateBossSmokeFollow()
    {
        if (bossSmokeEffectInstance == null)
        {
            return;
        }

        Transform smokeTransform = bossSmokeEffectInstance.transform;
        if (smokeTransform.parent != transform)
        {
            smokeTransform.SetParent(transform, false);
        }

        if (IsStunned)
        {
            // 巨体の中心が画面外でも、爆発した被弾箇所から停止中の煙が見える。
            smokeTransform.position = LastStunHitPosition + Vector3.up * 0.45f;
        }
        else
        {
            smokeTransform.localPosition = BossSmokeLocalOffset;
        }
        smokeTransform.localRotation = Quaternion.identity;
    }

    private GameObject CreateBossSmokeEffect()
    {
        if (bossSmokeEffectPrefab == null)
        {
            return null;
        }

        GameObject smokeObject = Instantiate(bossSmokeEffectPrefab, transform);
        smokeObject.name = "BossHalfHpSmoke";
        smokeObject.transform.SetParent(transform, false);
        smokeObject.transform.localPosition = BossSmokeLocalOffset;

        ParticleSystem particle = smokeObject.GetComponentInChildren<ParticleSystem>();
        if (particle != null)
        {
            ParticleSystem.MainModule main = particle.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = particle.emission;
            emission.enabled = true;
            emission.rateOverTime = BossSmokeMinEmission;

            if (!particle.isPlaying)
            {
                particle.Play();
            }
        }

        return smokeObject;
    }

    /// <summary>
    /// 外部からLastEnemyフラグを設定する
    /// </summary>
    public void SetLastEnemy(bool value)
    {
        isLastEnemy = value;
    }

    /// <summary>
    /// 旧互換：WaveData設定
    /// </summary>
    public void SetWaveData(EnemyWaveData.SpawnData data)
    {
        if (data != null)
        {
            enemyData = data.enemyData;
            isLastEnemy = data.LastEnemy;
        }
    }
}
