using UnityEngine;

namespace GearCraft.Scripts.Items
{
    public class DroppedMaterialItem : MonoBehaviour
    {
        [Header("Material")]
        [SerializeField] private MaterialManager.MaterialType materialType;
        [SerializeField] private int amount = 1;

        [Header("Pickup")]
        [SerializeField] private float magnetSpeed = 12f;
        [SerializeField] private float pickupDistance = 0.5f;

        [Header("Scatter")]
        [SerializeField] private float scatterForce = 5f;

        [Header("Lifetime")]
        [SerializeField] private float lifeTime = 10f;
        [SerializeField] private float blinkStartTime = 5f;
        [SerializeField] private float maxBlinkInterval = 0.35f;
        [SerializeField] private float minBlinkInterval = 0.06f;

        private Transform player;
        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private bool isMagneting;
        private float elapsedTime;
        private float blinkTimer;

        public void Configure(MaterialManager.MaterialType type, int value)
        {
            materialType = type;
            amount = value;
        }

        private void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            IgnoreEnemyCollisions();

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }

            if (rb != null)
            {
                Vector2 randomDir = new Vector2(
                    Random.Range(-1f, 1f),
                    Random.Range(0.5f, 1.5f)
                ).normalized;
                rb.AddForce(randomDir * scatterForce, ForceMode2D.Impulse);
                rb.angularVelocity = Random.Range(-360f, 360f);
            }
        }

        public static void IgnoreCollisionWithExistingDrops(Collider2D targetCollider)
        {
            if (targetCollider == null)
            {
                return;
            }

            DroppedMaterialItem[] drops = FindObjectsByType<DroppedMaterialItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < drops.Length; i++)
            {
                Collider2D dropCollider = drops[i] != null ? drops[i].GetComponent<Collider2D>() : null;
                if (dropCollider != null && dropCollider != targetCollider)
                {
                    Physics2D.IgnoreCollision(targetCollider, dropCollider, true);
                }
            }
        }

        private void IgnoreEnemyCollisions()
        {
            Collider2D dropCollider = GetComponent<Collider2D>();
            if (dropCollider == null)
            {
                return;
            }

            global::EnemyController[] enemies = FindObjectsByType<global::EnemyController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < enemies.Length; i++)
            {
                Collider2D enemyCollider = enemies[i] != null ? enemies[i].GetComponent<Collider2D>() : null;
                if (enemyCollider != null)
                {
                    Physics2D.IgnoreCollision(dropCollider, enemyCollider, true);
                }
            }
        }

        private void Update()
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= lifeTime)
            {
                Destroy(gameObject);
                return;
            }

            UpdateBlink();
            UpdateMagnetPickup();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision != null && collision.CompareTag("Player"))
            {
                Pickup();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision != null && collision.collider != null && collision.collider.CompareTag("Player"))
            {
                Pickup();
            }
        }

        private void UpdateBlink()
        {
            if (spriteRenderer == null || blinkStartTime <= 0f) return;

            float remainingTime = lifeTime - elapsedTime;
            if (remainingTime > blinkStartTime)
            {
                spriteRenderer.enabled = true;
                return;
            }

            float blinkProgress = 1f - Mathf.Clamp01(remainingTime / blinkStartTime);
            float interval = Mathf.Lerp(maxBlinkInterval, minBlinkInterval, blinkProgress);
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= interval)
            {
                blinkTimer = 0f;
                spriteRenderer.enabled = !spriteRenderer.enabled;
            }
        }

        private void UpdateMagnetPickup()
        {
            if (player == null) return;

            float magnetRange = 3f;
            if (StatusManager.Instance != null)
            {
                magnetRange = StatusManager.Instance.magnetRange;
            }

            float dist = Vector2.Distance(transform.position, player.position);
            if (dist <= magnetRange)
            {
                isMagneting = true;
            }

            if (!isMagneting) return;

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            transform.position = Vector2.MoveTowards(
                transform.position,
                player.position,
                magnetSpeed * Time.deltaTime
            );

            if (dist <= pickupDistance)
            {
                Pickup();
            }
        }

        private void Pickup()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }

            if (MaterialManager.Instance != null)
            {
                MaterialManager.Instance.AddMaterial(materialType, amount);
            }

            Destroy(gameObject);
        }
    }
}
