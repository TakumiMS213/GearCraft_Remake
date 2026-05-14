using UnityEngine;
using DG.Tweening;

/// <summary>
/// 突進＆体当たりAI。プレイヤーに向かって高速突進する。
/// </summary>
public class EnemyAI_Charger : MonoBehaviour, IEnemyAI
{
    private EnemyController owner;
    private EnemyDataSO data;
    private Transform playerTarget;
    private float chargeTimer;
    private bool isCharging = false;
    private Tween chargeTween;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        this.owner = owner;
        this.data = data;
        chargeTimer = data.attackInterval;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTarget = playerObj.transform;
    }

    public void UpdateAI()
    {
        if (owner == null || isCharging) return;

        // プレイヤー方向にゆっくり接近
        if (playerTarget != null)
        {
            Vector2 dir = (playerTarget.position - owner.transform.position).normalized;
            owner.transform.Translate(dir * data.speed * 0.3f * Time.deltaTime);
        }

        chargeTimer -= Time.deltaTime;
        if (chargeTimer <= 0f)
        {
            OnAttack();
            chargeTimer = data.attackInterval;
        }
    }

    public void OnAttack()
    {
        if (playerTarget == null) return;

        isCharging = true;
        Vector3 targetPos = playerTarget.position;

        chargeTween = owner.transform.DOMove(targetPos, data.chargeTime)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                isCharging = false;
            });
    }

    public void OnDeath()
    {
        chargeTween?.Kill();
    }
}
