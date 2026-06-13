using UnityEngine;

/// <summary>
/// 地面に落ちた素材アイテム。物理落下→プレイヤー接近で吸い寄せ→接触でインベントリ追加。
/// </summary>
public class DroppedMaterialItem : MonoBehaviour
{
    [Header("素材情報")]
    public MaterialManager.MaterialType materialType;
    public int amount = 1;

    [Header("吸い寄せ設定")]
    public float magnetSpeed = 12f;
    public float pickupDistance = 0.5f;

    [Header("初期散乱")]
    public float scatterForce = 5f;

    private Transform player;
    private Rigidbody2D rb;
    private bool isMagneting = false;
    private SpriteRenderer spriteRenderer;
    private float lifeTime = 30f;       // 30秒で自動消滅

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // プレイヤーを検索
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        // ランダム方向に散乱
        if (rb != null)
        {
            Vector2 randomDir = new Vector2(
                Random.Range(-1f, 1f),
                Random.Range(0.5f, 1.5f)
            ).normalized;
            rb.AddForce(randomDir * scatterForce, ForceMode2D.Impulse);
            rb.angularVelocity = Random.Range(-360f, 360f);
        }

        // 自動消滅タイマー
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        if (player == null) return;

        float magnetRange = 3f;
        // StatusManagerから吸収範囲を取得
        if (StatusManager.Instance != null)
            magnetRange = StatusManager.Instance.magnetRange;

        float dist = Vector2.Distance(transform.position, player.position);

        // 吸い寄せ判定
        if (dist <= magnetRange)
        {
            isMagneting = true;
        }

        // 吸い寄せ移動
        if (isMagneting)
        {
            // 物理を無効化して直接移動
            if (rb != null)
            {
                rb.gravityScale = 0;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            transform.position = Vector2.MoveTowards(
                transform.position,
                player.position,
                magnetSpeed * Time.deltaTime
            );

            // ピックアップ
            if (dist <= pickupDistance)
            {
                Pickup();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Pickup();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            Pickup();
        }
    }

    private void Pickup()
    {
        if (MaterialManager.Instance != null)
        {
            MaterialManager.Instance.AddMaterial(materialType, amount);
        }

        // ピックアップエフェクト（簡易的にスケールを0にしてDestroy）
        Destroy(gameObject);
    }
}
