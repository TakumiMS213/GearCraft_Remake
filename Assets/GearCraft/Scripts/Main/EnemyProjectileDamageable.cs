using UnityEngine;

public sealed class EnemyProjectileDamageable : MonoBehaviour
{
    [SerializeField] private float maxHP = 20f;
    [SerializeField] private float contactDamage = 15f;
    [SerializeField] private GameObject destroyEffect;

    private float hp;

    private void Awake()
    {
        hp = Mathf.Max(1f, maxHP);
    }

    public void TakeDamage(float damage)
    {
        hp -= Mathf.Max(0f, damage);
        if (hp > 0f)
        {
            return;
        }

        DestroyProjectile();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamageByBullet(contactDamage);
            DestroyProjectile();
            return;
        }

        GateManager gate = other.GetComponent<GateManager>();
        if (gate != null)
        {
            gate.TakeDamage(contactDamage);
            if (StageFlowManager.Instance != null && !MainDebugEnemySpawnWindow.IsDebugModeActive)
            {
                StageFlowManager.Instance.OnGateHit();
            }

            DestroyProjectile();
        }
    }

    private void DestroyProjectile()
    {
        if (destroyEffect != null)
        {
            Instantiate(destroyEffect, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
