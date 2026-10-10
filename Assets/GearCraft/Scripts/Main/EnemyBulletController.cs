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

    private GameObject smokeInstance;
    private ParticleSystem[] smokeParticles;

    private void Start()
    {
        // 寿命タイマー
        Destroy(gameObject, bulletLifeTime);
    }

    private void Update()
    {
        if (!DoSmoke || smoke == null)
        {
            StopSmoke();
            return;
        }

        if (smokeInstance == null)
        {
            // 発生源は弾ごとに一つ。弾と一緒に破棄されないよう親子にはしない。
            // Prefab本来のスケールを保ち、既に出た煙はワールド空間に残す。
            smokeInstance = Instantiate(smoke, transform.position, Quaternion.identity);
            smokeInstance.SetActive(true);
            smokeParticles = smokeInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in smokeParticles)
            {
                var main = particle.main;
                main.stopAction = ParticleSystemStopAction.None;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(false);
            }
        }
        smokeInstance.transform.position = transform.position;
    }

    private void StopSmoke()
    {
        GameObject effect = smokeInstance;
        ParticleSystem[] particles = smokeParticles;
        smokeInstance = null;
        smokeParticles = null;
        if (effect == null) return;

        float remainingLifetime = 0f;
        if (particles != null)
        {
            foreach (ParticleSystem particle in particles)
            {
                if (particle == null) continue;
                var main = particle.main;
                // 自動破棄に頼らず、子の粒子も消えるまで残煙を保持する。
                main.stopAction = ParticleSystemStopAction.None;
                remainingLifetime = Mathf.Max(remainingLifetime,
                    main.startLifetime.constantMax / Mathf.Max(0.01f, main.simulationSpeed));
                particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        Destroy(effect, remainingLifetime + 0.1f);
    }

    private void OnDisable()
    {
        StopSmoke();
    }

    private void OnDestroy()
    {
        StopSmoke();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null) return;

        // 迎撃できる通常弾は、接触ダメージと消滅演出を同じコンポーネントで一度だけ処理する。
        // Player/Gateへの接触はEnemyProjectileDamageable側で受け、地面接触だけここから委譲する。
        EnemyProjectileDamageable damageable = GetComponent<EnemyProjectileDamageable>();
        if (damageable != null && !bombBullet)
        {
            if (destroyOnHit && collision.CompareTag("Ground"))
            {
                damageable.DestroyProjectile();
            }
            return;
        }

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
