using UnityEngine;

public sealed class EnemyProjectileDamageable : MonoBehaviour
{
    [SerializeField] private float maxHP = 20f;
    [SerializeField] private float contactDamage = 15f;
    [SerializeField] private GameObject destroyEffect;

    private float hp;
    private bool destroyed;
    private Collider2D projectileCollider;
    private bool restoreColliderOnEnable;

    private void Awake()
    {
        projectileCollider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        hp = Mathf.Max(1f, maxHP);
        destroyed = false;
        if (restoreColliderOnEnable && projectileCollider != null)
        {
            projectileCollider.enabled = true;
        }
        restoreColliderOnEnable = false;
    }

    public void TakeDamage(float damage)
    {
        if (destroyed) return;

        hp -= Mathf.Max(0f, damage);
        if (hp > 0f)
        {
            return;
        }

        DestroyProjectile();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (destroyed || other == null)
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

    public void DestroyProjectile(bool showEffect = true)
    {
        if (destroyed) return;
        destroyed = true;

        // Destroy is deferred until the end of the frame. Stop later hit callbacks immediately.
        if (projectileCollider != null && projectileCollider.enabled)
        {
            projectileCollider.enabled = false;
            restoreColliderOnEnable = true;
        }

        if (showEffect && destroyEffect != null)
        {
            Instantiate(destroyEffect, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
