using UnityEngine;

/// <summary>
/// 自爆型AI。プレイヤーまたはゲートに接近すると爆発してダメージを与える。
/// </summary>
public class EnemyAI_Bomber : MonoBehaviour, IEnemyAI
{
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform target;
    private bool hasExploded = false;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;

        // 最も近いターゲット（プレイヤーかゲート）に向かう
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        GameObject gateObj = GameObject.FindGameObjectWithTag("gate");

        if (playerObj != null && gateObj != null)
        {
            float distPlayer = Vector2.Distance(owner.transform.position, playerObj.transform.position);
            float distGate = Vector2.Distance(owner.transform.position, gateObj.transform.position);
            target = (distPlayer < distGate) ? playerObj.transform : gateObj.transform;
        }
        else if (playerObj != null)
            target = playerObj.transform;
        else if (gateObj != null)
            target = gateObj.transform;
    }

    public void UpdateAI()
    {
        if (owner == null || target == null || hasExploded) return;

        // ターゲット方向へ突進
        Vector2 dir = (target.position - owner.transform.position).normalized;
        owner.transform.Translate(dir * owner.GetMoveSpeed(data.speed) * 1.5f * Time.deltaTime);

        // 近接で爆発
        float dist = Vector2.Distance(owner.transform.position, target.position);
        if (dist <= data.explosionRadius * 0.5f)
        {
            Explode();
        }
    }

    public void OnAttack()
    {
        // Bomberは直接攻撃しない（爆発のみ）
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // 範囲内のプレイヤー/ゲートにダメージ
        Collider2D[] hits = Physics2D.OverlapCircleAll(owner.transform.position, data.explosionRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                var player = hit.GetComponent<PlayerController>();
                if (player != null)
                    player.TakeDamageByBullet(data.explosionDamage);
            }
            if (hit.CompareTag("gate"))
            {
                var gate = hit.GetComponent<GateManager>();
                if (gate != null)
                    gate.TakeDamage(data.explosionDamage);
            }
        }

        // 自分を死亡処理
        owner.Die().Forget();
    }

    public void OnDeath()
    {
        // 死亡時にも爆発（撃破されても爆発する）
        if (!hasExploded)
            Explode();
    }
}
