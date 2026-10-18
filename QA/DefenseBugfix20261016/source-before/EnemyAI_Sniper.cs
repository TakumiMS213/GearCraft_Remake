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
    private BossArena arena;
    private float sniperHorizontal;
    private float spawnHeight;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;
        hasPositioned = false;
        attackTimer = data.attackInterval * 1.5f; // 初弾は遅め

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTarget = playerObj.transform;

        arena = new BossArena(owner, containSprite: true);
        sniperHorizontal = Random.Range(0.7f, 0.9f);
        spawnHeight = owner.transform.position.y;
        sniperPosition = GetSniperPosition();
    }

    public void UpdateAI()
    {
        if (owner == null) return;

        // カメラの移動やノックバック後も、画面・壁の内側の狙撃位置へ戻る。
        sniperPosition = GetSniperPosition();
        if ((owner.transform.position - sniperPosition).sqrMagnitude > 0.01f)
            hasPositioned = false;

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

    private Vector3 GetSniperPosition()
    {
        Rect rect = arena.GetMovementRect();
        return new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, sniperHorizontal),
            Mathf.Clamp(spawnHeight, rect.yMin, rect.yMax), owner.transform.position.z);
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
