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
        EnemyController ec = collision.gameObject.GetComponent<EnemyController>();
        Debug.Log("衝突検出: " + collision.name);
        if (collision.CompareTag("Enemy") && ec != null)
        {
            // 円軌道型(旧AR)はゲートに衝突しない
            bool isCircleType = ec.enemyData != null && ec.enemyData.aiType == EnemyAIType.CircleMove;
            if (!isCircleType)
            {
                TakeDamage(10f);
                await cameraShake.ShakeAsync(0.1f, 0.3f, 1, 9f);
                ec.Die().Forget();
                statusManager.killAllEnemies = false;

                // StageFlowManagerに通知（PERFECT判定用）
                if (StageFlowManager.Instance != null)
                    StageFlowManager.Instance.OnGateHit();
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (isInvincible) return;

        statusManager.GATE -= damage;
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
        Debug.Log("ゲートがダメージを受けました。点滅エフェクトを開始します。");
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
