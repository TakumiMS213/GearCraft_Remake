using UnityEngine;

public class EnemyBulletController : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float bulletDamage = 10f;
    public float bulletLifeTime = 2f;
    public bool destroyOnHit = true; // falseなら貫通
    public bool DoSmoke = false;

    [Header("Effects (Optional)")]
    public GameObject hitEffect; // ヒットエフェクトPrefab
    public GameObject smoke;
    public GameObject bombEffect; // 爆発エフェクトPrefab（任意）

    [Header("WhichBullet")]
    public bool bombBullet = false;

    [Header("Bomb Settings")]
    public float bombRadius = 3f; // 爆発範囲
    public LayerMask playerLayer; // Playerのレイヤーを指定

    public AudioSource hitSound;

    private void Start()
    {
        // 寿命タイマー
        Destroy(gameObject, bulletLifeTime);
    }

    private void Update()
    {
        if (smoke == null) return;

        if (DoSmoke)
        {
            GameObject effect = Instantiate(smoke, transform.position, Quaternion.identity);
            effect.SetActive(true); // 念のため
            var ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Simulate(0, true, true);
                ps.Play();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 敵や壁などには反応しない
        if (!collision.CompareTag("Player") && !collision.CompareTag("Ground")) return;

        // 爆弾弾なら爆発処理を優先
        if (bombBullet)
        {
            BombDamage(); // 範囲ダメージを与える
        }
        else
        {
            // 通常弾のダメージ処理
            PlayerController player = collision.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamageByBullet(bulletDamage);
            }

            GateManager gate = collision.GetComponent<GateManager>();
            if (gate != null)
            {
                gate.TakeDamage(bulletDamage);
            }

            SpawnHitEffect();
        }

        // 弾を消す（必要に応じて）
        if (destroyOnHit)
            Destroy(gameObject);
    }

    private void SpawnHitEffect()
    {
        if (hitEffect != null)
        {
            GameObject effect = Instantiate(hitEffect, transform.position, Quaternion.identity);
            effect.SetActive(true);
            var ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Simulate(0, true, true);
                ps.Play();
            }
        }
    }

    private void BombDamage()
    {
        Debug.Log("爆弾弾が爆発しました！");

        // 爆発エフェクト生成
        if (bombEffect != null)
        {
            GameObject effect = Instantiate(bombEffect, transform.position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * Mathf.Max(0.1f, bombRadius);
        }

        // 範囲内のプレイヤー検出
        Collider2D[] hitPlayers = playerLayer.value != 0
            ? Physics2D.OverlapCircleAll(transform.position, bombRadius, playerLayer)
            : Physics2D.OverlapCircleAll(transform.position, bombRadius);

        foreach (Collider2D col in hitPlayers)
        {
            if (!col.CompareTag("Player"))
            {
                continue;
            }

            PlayerController player = col.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamageByBullet(bulletDamage);
                Debug.Log($"→ {player.name} に範囲ダメージ {bulletDamage}");
            }
        }
    }

    // Scene上で爆発範囲を可視化（デバッグ用）
    private void OnDrawGizmosSelected()
    {
        if (bombBullet)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, bombRadius);
        }
    }
}
