using System.Collections.Generic;
using GearCraft.Scripts.Items;
using UnityEngine;

namespace GearCraft.Scripts.Main
{
    public sealed class SteamThrowerBulletController : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField] private float damage = 4f;
        [SerializeField] private float hitInterval = 0.18f;
        [SerializeField] private float lifeTime = 1.6f;

        [Header("Overheat")]
        [SerializeField] private float overheatDuration = 2.5f;
        [SerializeField, Range(0.05f, 1f)] private float overheatSpeedMultiplier = 0.55f;
        [SerializeField] private GameObject overheatParticlePrefab;

        [Header("Visual")]
        [SerializeField] private GameObject impactParticlePrefab;

        private readonly Dictionary<EnemyController, float> lastHitTimes = new Dictionary<EnemyController, float>();
        private float overheatSlipDamage;
        private bool noGravity;
        private bool burnDrops;

        public void Configure(float bulletDamage, float duration, GameObject overheatEffect)
        {
            damage = Mathf.Max(0f, bulletDamage);
            lifeTime = Mathf.Max(0.1f, duration);
            if (overheatEffect != null)
            {
                overheatParticlePrefab = overheatEffect;
            }
        }

        public void ConfigureBonus(float slipDamage, bool disableGravity, bool canBurnDrops)
        {
            overheatSlipDamage = Mathf.Max(0f, slipDamage);
            noGravity = disableGravity;
            burnDrops = canBurnDrops;

            if (noGravity)
            {
                Rigidbody2D body = GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.gravityScale = 0f;
                }
            }
        }

        private void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            TryHit(collision);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            TryHit(collision);
        }

        private void TryHit(Collider2D collision)
        {
            if (collision == null)
            {
                return;
            }

            DroppedMaterialItem droppedMaterial = collision.GetComponent<DroppedMaterialItem>();
            if (droppedMaterial != null)
            {
                if (burnDrops)
                {
                    droppedMaterial.Burn();
                }

                return;
            }

            EnemyController enemy = collision.GetComponent<EnemyController>();
            if (enemy == null)
            {
                return;
            }

            float now = Time.time;
            if (lastHitTimes.TryGetValue(enemy, out float lastHitTime) && now - lastHitTime < hitInterval)
            {
                return;
            }

            lastHitTimes[enemy] = now;
            enemy.TakeDamage(damage);
            enemy.ApplyOverheat(overheatDuration, overheatSpeedMultiplier, overheatParticlePrefab);
            if (overheatSlipDamage > 0f)
            {
                enemy.ApplyOverheatSlipDamage(overheatSlipDamage);
            }

            if (impactParticlePrefab != null)
            {
                Instantiate(impactParticlePrefab, collision.ClosestPoint(transform.position), Quaternion.identity);
            }
        }
    }
}
