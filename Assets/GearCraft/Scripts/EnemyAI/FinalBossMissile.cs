using UnityEngine;

/// <summary>
/// EK-9ミサイルの飛行制御。発射直後だけ緩く追尾し、その後は直進する。
/// ダメージと迎撃は既存のEnemyProjectileDamageableに委譲する。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(EnemyProjectileDamageable))]
public sealed class FinalBossMissile : MonoBehaviour
{
    [SerializeField] private float lifetime = 7f;
    [SerializeField] private float homingDelay = 0.25f;
    [SerializeField] private float homingDuration = 1.1f;
    [SerializeField] private float turnSpeed = 65f;

    private Rigidbody2D body;
    private EnemyProjectileDamageable damageable;
    private Transform target;
    private Vector2 direction;
    private float speed;
    private float elapsed;
    private bool initialized;
    private bool removed;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        damageable = GetComponent<EnemyProjectileDamageable>();
    }

    private void OnEnable()
    {
        elapsed = 0f;
        initialized = false;
        removed = false;
    }

    public void Initialize(Transform target, Vector2 initialDirection, float speed)
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        this.target = target;
        this.speed = Mathf.Max(1f, speed);
        direction = initialDirection.sqrMagnitude > 0.001f ? initialDirection.normalized : Vector2.left;
        elapsed = 0f;
        removed = false;
        initialized = true;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.angularVelocity = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        UpdateVelocity();
    }

    private void FixedUpdate()
    {
        if (removed) return;
        elapsed += Time.fixedDeltaTime;
        if (elapsed >= Mathf.Max(0.1f, lifetime))
        {
            Remove(false);
            return;
        }
        if (!initialized) return;

        if (target != null && elapsed >= homingDelay && elapsed <= homingDelay + homingDuration)
        {
            Vector2 toTarget = (Vector2)target.position - body.position;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                angle = Mathf.MoveTowardsAngle(angle, targetAngle, Mathf.Max(0f, turnSpeed) * Time.fixedDeltaTime);
                direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            }
        }
        UpdateVelocity();
    }

    private void UpdateVelocity()
    {
        body.linearVelocity = direction * speed;
        body.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || other.isTrigger) return;
        if (other.GetComponent<GateManager>() != null) return;
        if (other.CompareTag("Ground") || other.CompareTag("Wall")) Remove(true);
    }

    private void Remove(bool showEffect)
    {
        if (removed) return;
        removed = true;
        // 迎撃・接触・地形衝突で同じ終了処理を共有し、同フレーム中の重複判定を止める。
        if (damageable != null) damageable.DestroyProjectile(showEffect);
        else Destroy(gameObject);
    }

    private void OnDisable()
    {
        target = null;
        initialized = false;
        if (body != null) body.linearVelocity = Vector2.zero;
    }
}
