using UnityEngine;
using DG.Tweening;

/// <summary>
/// 円軌道で動きつつ射撃するAI
/// </summary>
public class EnemyAI_Circle : MonoBehaviour, IEnemyAI
{
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform playerTarget;
    private Transform returnPoint;
    private Tween currentTween;
    private float attackTimer;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;
        attackTimer = data.attackInterval;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTarget = playerObj.transform;

        GameObject returnGO = GameObject.FindGameObjectWithTag("AR_returnPoint");
        if (returnGO != null)
            returnPoint = returnGO.transform;
    }

    public void UpdateAI()
    {
        if (owner == null) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            OnAttack();
            attackTimer = data.attackInterval;
        }
    }

    public void OnAttack()
    {
        if (owner.transform.position.y <= -5 || owner.transform.position.y >= 5 ||
            owner.transform.position.x >= 20 || owner.transform.position.x <= -20)
        {
            MoveToReturn();
            return;
        }

        // 円軌道移動しながら射撃
        MoveInArc();
        ShootBurst();
    }

    private void ShootBurst()
    {
        if (data.bulletPrefabs == null || data.bulletPrefabs.Length == 0 || playerTarget == null) return;

        Vector3 spawnPos = owner.transform.position + Vector3.down;
        Vector2 direction = (playerTarget.position - spawnPos).normalized;

        // 連射
        int bulletIndex = Mathf.Min(0, data.bulletPrefabs.Length - 1);
        GameObject bullet = Instantiate(data.bulletPrefabs[bulletIndex], spawnPos, Quaternion.identity);
        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = direction * data.bulletSpeed * Random.Range(2f, 6f);
    }

    private void MoveInArc()
    {
        if (currentTween != null) currentTween.Kill();

        bool reverse = Random.value > 0.5f;
        float radius = data.circleRadius;
        float duration = data.circleDuration;
        Vector3 center = owner.transform.localPosition + Vector3.down * radius;
        float startAngle = reverse ? 180f : 0f;
        float endAngle = reverse ? 0f : 180f;

        currentTween = DOVirtual.Float(startAngle, endAngle, duration, angle =>
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius, 0f);
            owner.transform.localPosition = center + offset;
        }).SetEase(Ease.InOutSine);
    }

    private void MoveToReturn()
    {
        if (returnPoint == null || currentTween != null) return;
        currentTween?.Kill();

        float distance = Vector3.Distance(owner.transform.position, returnPoint.position);
        float moveTime = distance / data.speed;

        currentTween = owner.transform.DOMove(returnPoint.position, moveTime)
            .SetEase(Ease.OutSine);
    }

    public void OnDeath()
    {
        currentTween?.Kill();
    }
}
