using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public class EnemyController : MonoBehaviour
{
    [Header("敵データ（ScriptableObject）")]
    public EnemyDataSO enemyData;

    [Header("ランタイムステータス")]
    public float HP;
    public bool isLastEnemy = false;

    [Header("表示設定")]
    public GameObject deathEffect;
    public GameObject highlightMarker;     // 強調表示用マーカー（ボス/ラスト敵）

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private CameraShake cameraShake;
    private IEnemyAI currentAI;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
        cameraShake = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;

        // EnemyDataSOからパラメータを読み込み
        if (enemyData != null)
        {
            // ステージ補正を加えたHP
            float stageBonus = StageCounter.Instance != null ? StageCounter.Instance.StageCount / 10f : 0f;
            HP = enemyData.baseHP + enemyData.baseHP * stageBonus;

            // AIアタッチ
            AttachAI(enemyData.aiType);

            // 強調表示
            if (enemyData.isHighlighted && highlightMarker != null)
                highlightMarker.SetActive(true);
        }
        else
        {
            HP = 50f;
        }

        // StageFlowManagerに登録
        if (StageFlowManager.Instance != null)
            StageFlowManager.Instance.RegisterEnemy(this);
    }

    void Update()
    {
        if (currentAI != null)
            currentAI.UpdateAI();
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

    public void TakeDamage(float amount)
    {
        HP -= amount;

        if (HP <= 0f)
        {
            Die().Forget();
        }
        else
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
                StartCoroutine(FlashOnHit());
            }
        }
    }

    public async UniTaskVoid Die()
    {
        Vector3 deathPos = transform.position;

        // 見た目と当たり判定を即座に消す
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || !spriteRenderer.enabled) return;
        spriteRenderer.enabled = false;

        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;

        // AIの死亡処理
        if (currentAI != null)
            currentAI.OnDeath();

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
                if (enemyData != null && enemyData.IsBossType)
                    await cameraShake.ShakeAsync(1f, 3f, 10, 90f);
                else
                    await cameraShake.ShakeAsync(0.2f, 0.6f, 10, 90f);
            }
        }

        // 素材ドロップ
        if (enemyData != null && enemyData.drops != null && MaterialDropper.Instance != null)
        {
            int stageNum = StageCounter.Instance != null ? StageCounter.Instance.StageCount : 1;
            var stageConfig = StageFlowManager.Instance?.stageConfig;
            if (stageConfig != null)
                MaterialDropper.Instance.DropMaterialsWithStageBonus(deathPos, enemyData.drops, stageConfig, stageNum);
            else
                MaterialDropper.Instance.DropMaterials(deathPos, enemyData.drops);
        }

        // StageFlowManagerに通知（ステージ進行を委譲）
        if (StageFlowManager.Instance != null)
        {
            StageFlowManager.Instance.OnEnemyKilled(this, isLastEnemy);
        }

        Destroy(gameObject);
    }

    private System.Collections.IEnumerator FlashOnHit()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.05f);
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
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
            isLastEnemy = data.LastEnemy;
        }
    }
}
