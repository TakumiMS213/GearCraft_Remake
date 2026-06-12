using UnityEngine;

/// <summary>
/// 直進してゲートに向かうAI
/// </summary>
public class EnemyAI_Straight : MonoBehaviour, IEnemyAI
{
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform target;
    private float attackTimer;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;
        attackTimer = data.attackInterval;

        // ゲートまたはプレイヤーをターゲット
        GameObject gateObj = GameObject.FindGameObjectWithTag("gate");
        if (gateObj != null)
            target = gateObj.transform;
        else
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
        }
    }

    public void UpdateAI()
    {
        if (target == null || owner == null) return;

        // ゲート方向へ直進
        Vector2 dir = (target.position - owner.transform.position).normalized;
        Vector2 nextPosition = (Vector2)owner.transform.position + dir * data.speed * Time.deltaTime;
        owner.transform.position = nextPosition;

        // 攻撃タイマー
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            OnAttack();
            attackTimer = data.attackInterval;
        }
    }

    public void OnAttack()
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0) return;

        // プレイヤー方向に弾を発射
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        Vector3 spawnPos = owner.transform.position + Vector3.down;
        Vector2 direction = (playerObj.transform.position - spawnPos).normalized;

        GameObject bullet = Instantiate(data.bulletPrefabs[0], spawnPos, Quaternion.identity);
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = direction * data.bulletSpeed;
    }

    public void OnDeath()
    {
        Rigidbody2D rb = owner != null ? owner.GetComponent<Rigidbody2D>() : null;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}
