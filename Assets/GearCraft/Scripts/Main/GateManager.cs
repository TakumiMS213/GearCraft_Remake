using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Threading.Tasks;
using Unity.VisualScripting;

public class GateManager : MonoBehaviour
{
    // ======== シングルトン部分 ========
    public static GateManager Instance { get; private set; }

    void Awake()
    {
        // シングルトン初期化
        if (Instance == null)
        {
            Instance = this;
            // シーンをまたいで保持したい場合は↓を有効化
            // DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    // ======== パラメータ ========
    public StatusManager statusManager;
    [Header("HP")]
    public float HP = 0;

    [Header("点滅の回数")]
    public int blinkCount = 3;

    [Header("点滅の間隔（秒）")]
    public float blinkInterval = 0.1f;

    [Header("無敵時間（秒）")]
    public float invincibleTime = 0.5f;

    private bool isInvincible = false;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    public CameraShake cameraShake;

    // ======== 初期化 ========
    void Start()
    {
    statusManager = StatusManager.Instance ?? FindAnyObjectByType<StatusManager>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRendererが見つかりません。このスクリプトはSpriteRendererを持つオブジェクトに付けてください。");
            enabled = false;
            return;
        }
        originalColor = spriteRenderer.color;
        if (statusManager != null)
            HP = statusManager.GATE;
        else
            Debug.LogWarning("StatusManager not found; GateManager couldn't initialize HP from StatusManager.");
    }

    // ======== ダメージ処理 ========
    private async Task OnTriggerEnter2D(Collider2D collision)
    {
        EnemyController ec = collision.GetComponentInParent<EnemyController>();
        if (ec != null && (collision.CompareTag("Enemy") || ec.CompareTag("Enemy")))
        {
            if (MainDebugEnemySpawnWindow.IsDebugModeActive)
            {
                return;
            }

            if (IsBoss(ec))
            {
                OnBossReachedGate(ec);
                return;
            }

            // 通常の円軌道型はゲートに衝突しない。
            bool isCircleType = ec.enemyData != null && ec.enemyData.aiType == EnemyAIType.CircleMove;
            if (!isCircleType)
            {
                TakeDamage(10f);
                // 無敵中に門へ到達した敵も撃ち漏らしとして扱う。
                if (statusManager != null) statusManager.killAllEnemies = false;
                if (cameraShake != null) await cameraShake.ShakeAsync(0.1f, 0.3f, 1, 9f);
                if (this != null && ec != null) ec.Die().Forget();
            }
        }
    }

    private static bool IsBoss(EnemyController enemy)
    {
        if (enemy == null || enemy.enemyData == null)
        {
            return false;
        }

        string enemyName = enemy.enemyData.enemyName;
        return enemy.enemyData.IsBossType ||
            enemyName == "AR-227" ||
            enemyName == "BB-413";
    }

    public void OnBossReachedGate(EnemyController boss)
    {
        if (!IsBoss(boss) || boss.HP <= 0f) return;
        if (MainDebugEnemySpawnWindow.IsDebugModeActive || EndingManager.IsLoadingEnding) return;
        if (statusManager == null) statusManager = StatusManager.Instance;
        if (statusManager == null) return;

        // ボスの到達は通常ダメージとは別の敗北条件。門の無敵時間も無視する。
        statusManager.GATE = 0f;
        HP = 0f;
        statusManager.killAllEnemies = false;
        StageFlowManager.Instance?.OnGateHit();
        GameOver();
    }

    public void TakeDamage(float damage)
    {
        if (MainDebugEnemySpawnWindow.IsDebugModeActive) return;
        if (isInvincible || EndingManager.IsLoadingEnding || damage <= 0f) return;
        if (statusManager == null) statusManager = StatusManager.Instance;
        if (statusManager == null) return;

        statusManager.GATE -= damage;
        statusManager.killAllEnemies = false;
        StageFlowManager.Instance?.OnGateHit();
        StartCoroutine(BlinkEffect());
        if(statusManager.GATE <= 0)
        {
            GameOver();
        }
    }

    // ======== 修理（外部から呼び出し可） ========
    public void RepairGate(float amount)
    {
        if (statusManager != null)
            statusManager.GATE += amount;
        Debug.Log($"ゲートを修理: +{amount}（現在GATE: {statusManager?.GATE ?? 0}）");
    }

    // ======== 点滅エフェクト ========
    IEnumerator BlinkEffect()
    {
        isInvincible = true;

        for (int i = 0; i < blinkCount; i++)
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(blinkInterval);
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(blinkInterval);
        }

        yield return new WaitForSeconds(invincibleTime);
        isInvincible = false;
    }

    void GameOver()
    {
        EndingManager.LoadEndingScene(1); //バッドエンド
    }
}
