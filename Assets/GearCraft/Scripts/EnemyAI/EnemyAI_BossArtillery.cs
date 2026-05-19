using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

/// <summary>
/// ボス3(Stage30): 遠距離ミサイル・爆撃型
/// 画面上部に陣取りミサイルを連射。Phaseが進むと爆撃エリア追加。
/// </summary>
public class EnemyAI_BossArtillery : MonoBehaviour, IEnemyAI
{
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform playerTarget;
    private float attackTimer;
    private float bombardTimer;
    private int phase = 0;
    private Tween moveTween;
    private bool hasPositioned = false;
    private Vector3 sniperPosition;
    private float runtimeMaxHp;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;
        runtimeMaxHp = owner != null ? owner.HP : data.baseHP;
        attackTimer = data.attackInterval * 1.5f;
        bombardTimer = 8f;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTarget = playerObj.transform;

        // 画面上部の狙撃位置を決定
        sniperPosition = owner.transform.position + new Vector3(Random.Range(-3f, 3f), 2f, 0);
    }

    public void UpdateAI()
    {
        if (owner == null) return;

        // フェーズ判定
        float hpRatio = runtimeMaxHp > 0f ? owner.HP / runtimeMaxHp : 1f;
        if (hpRatio <= 0.3f) phase = 2;
        else if (hpRatio <= 0.6f) phase = 1;
        else phase = 0;

        // ポジショニング（初回のみ）
        if (!hasPositioned)
        {
            owner.transform.position = Vector3.MoveTowards(
                owner.transform.position, sniperPosition, data.speed * 0.5f * Time.deltaTime);
            if (Vector3.Distance(owner.transform.position, sniperPosition) < 0.1f)
                hasPositioned = true;
            return;
        }

        // ゆっくり左右移動
        if (moveTween == null || !moveTween.IsActive())
            StartDrift();

        // ミサイル攻撃
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            OnAttack();
            float intervalMult = phase == 2 ? 0.3f : phase == 1 ? 0.5f : 1f;
            attackTimer = data.attackInterval * intervalMult;
        }

        // 爆撃攻撃（Phase 1以降）
        if (phase >= 1)
        {
            bombardTimer -= Time.deltaTime;
            if (bombardTimer <= 0f)
            {
                PerformBombardment();
                bombardTimer = phase == 2 ? 4f : 6f;
            }
        }
    }

    public void OnAttack()
    {
        if (playerTarget == null) return;

        switch (phase)
        {
            case 0:
                // ミサイル2発
                LaunchMissiles(2);
                break;
            case 1:
                // ミサイル3発 + 直射1発
                LaunchMissiles(3);
                ShootDirect();
                break;
            case 2:
                // ミサイル5発 + 直射3発
                LaunchMissiles(5);
                ShootDirect();
                ShootDirect();
                ShootDirect();
                break;
        }
    }

    /// <summary>
    /// ミサイル発射（放物線を描いて着弾）
    /// </summary>
    private void LaunchMissiles(int count)
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0) return;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = owner.transform.position + new Vector3(Random.Range(-1f, 1f), 0, 0);

            // ミサイルプレハブ（あれば後半のインデックスを使用）
            int prefabIdx = Mathf.Min(data.bulletPrefabs.Length - 1, 1);
            GameObject missile = Instantiate(data.bulletPrefabs[prefabIdx], spawnPos, Quaternion.identity);

            Rigidbody2D rb = missile.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // 放物線：上に飛んでから落下
                Vector2 targetDir = new Vector2(
                    Random.Range(-0.5f, 0.5f),
                    -1f
                ).normalized;
                rb.linearVelocity = targetDir * data.bulletSpeed * 0.8f;
                rb.gravityScale = 0.5f;
            }
        }
    }

    /// <summary>
    /// プレイヤーに向けて直射
    /// </summary>
    private void ShootDirect()
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0 || playerTarget == null) return;

        Vector3 spawnPos = owner.transform.position;
        Vector2 dir = (playerTarget.position - spawnPos).normalized;
        // 若干のブレ
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + Random.Range(-5f, 5f);
        dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        GameObject bullet = Instantiate(data.bulletPrefabs[0], spawnPos, Quaternion.identity);
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = dir * data.bulletSpeed * 2f;
    }

    /// <summary>
    /// 爆撃：プレイヤー周囲に複数の弾を落とす
    /// </summary>
    private void PerformBombardment()
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0 || playerTarget == null) return;

        int bombCount = phase == 2 ? 8 : 4;

        for (int i = 0; i < bombCount; i++)
        {
            // プレイヤーの周囲にランダムに落下
            Vector3 targetPos = playerTarget.position + new Vector3(
                Random.Range(-5f, 5f),
                8f, // 画面上から落下
                0f
            );

            int prefabIdx = Mathf.Min(data.bulletPrefabs.Length - 1, 1);
            GameObject bomb = Instantiate(data.bulletPrefabs[prefabIdx], targetPos, Quaternion.identity);
            Rigidbody2D rb = bomb.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.down * data.bulletSpeed * 1.5f;
                rb.gravityScale = 0.3f;
            }

            // 着弾エリア（キャノンボール→爆発ダメージ）
            var bc = bomb.GetComponent<BulletController>();
            if (bc != null)
                bc.isCannonBullet = true;
        }
    }

    private void StartDrift()
    {
        float targetX = owner.transform.position.x + Random.Range(-3f, 3f);
        targetX = Mathf.Clamp(targetX, -8f, 8f);
        moveTween = owner.transform.DOMoveX(targetX, 2f)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => moveTween = null);
    }

    public void OnDeath()
    {
        moveTween?.Kill();
    }
}
