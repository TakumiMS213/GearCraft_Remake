using UnityEngine;

/// <summary>
/// 遠距離から精密射撃するAI。定位置に留まり高精度で射撃。
/// </summary>
public class EnemyAI_Sniper : MonoBehaviour, IEnemyAI
{
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform playerTarget;
    private float attackTimer;
    private bool hasPositioned = false;
    private Vector3 sniperPosition;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;
        attackTimer = data.attackInterval * 1.5f; // 初弾は遅め

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTarget = playerObj.transform;

        // 初期位置から少し動いて狙撃ポジションを取る
        sniperPosition = owner.transform.position + new Vector3(Random.Range(-2f, 2f), 0, 0);
    }

    public void UpdateAI()
    {
        if (owner == null) return;

        // ポジショニング
        if (!hasPositioned)
        {
            owner.transform.position = Vector3.MoveTowards(
                owner.transform.position, sniperPosition, owner.GetMoveSpeed(data.speed) * Time.deltaTime);
            if (Vector3.Distance(owner.transform.position, sniperPosition) < 0.1f)
                hasPositioned = true;
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            OnAttack();
            attackTimer = data.attackInterval;
        }
    }

    public void OnAttack()
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0 || playerTarget == null) return;

        Vector3 spawnPos = owner.transform.position;
        Vector2 direction = (playerTarget.position - spawnPos).normalized;

        // 精密射撃（高速弾）
        int idx = Mathf.Min(data.bulletPrefabs.Length - 1, 0);
        GameObject bullet = Instantiate(data.bulletPrefabs[idx], spawnPos, Quaternion.identity);
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = direction * data.bulletSpeed * 3f;
    }

    public void OnDeath() { }
}
