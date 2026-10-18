using UnityEngine;

/// <summary>
/// IC-408: 接地したまま右から一定速度で迫る、序盤のDPSチェッカー。
/// ランダムな後退やHPによるバリア・弾幕の切り替えは行わない。
/// </summary>
public class EnemyAI_BossTank : MonoBehaviour, IEnemyAI
{
    private const float EngineVibrationAmplitude = 0.012f;
    private const float EngineVibrationFrequency = 12f;
    private const float GateAttackDamage = 10f;
    private const float GateAttackInterval = 2.5f;
    private const float GateAttackDelay = 0.85f;
    private const float GateClearance = 1.6f;
    private const float EntryClearance = 0.15f;
    private const float EntryDuration = 1.2f;
    private EnemyController owner;
    private EnemyDataSO data;
    private BossArena arena;
    private GateManager gate;
    private Collider2D gateBody;
    private Rigidbody2D bodyRigidbody;
    private Collider2D playerBody;
    private bool hasEnteredArena;
    private float entrySpeed;
    private float bodyLeftOffset;
    private float gateAttackTimer;
    private Transform visual;
    private Vector3 visualRestPosition;
    private float vibrationTime;
    private bool isDead;

    public void Initialize(EnemyController owner, EnemyDataSO data)
    {
        if (visual != null) visual.localPosition = visualRestPosition;
        this.owner = owner;
        this.data = data;
        visual = owner.transform.Find("Visual");
        visualRestPosition = visual != null ? visual.localPosition : Vector3.zero;
        vibrationTime = 0f;
        isDead = false;
        gate = GateManager.Instance;
        gateBody = gate != null ? gate.GetComponent<Collider2D>() : null;
        Collider2D body = owner.GetComponent<Collider2D>();
        bodyRigidbody = owner.GetComponent<Rigidbody2D>();
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        playerBody = playerObject != null ? playerObject.GetComponent<Collider2D>() : null;
        bodyLeftOffset = body != null ? body.bounds.min.x - owner.transform.position.x : 0f;
        gateAttackTimer = GateAttackDelay;
        arena = new BossArena(owner);
        Vector3 entryPosition = arena.GetPosition(1f, 0f);
        // 車体をプレイヤーの上へ出現させず、必ず右側から進入する。
        if (body != null && playerBody != null && playerBody.enabled && playerBody.gameObject.activeInHierarchy)
            entryPosition.x = Mathf.Max(entryPosition.x,
                playerBody.bounds.max.x + EntryClearance - bodyLeftOffset);
        hasEnteredArena = false;
        entrySpeed = Mathf.Max(1f, (entryPosition.x - arena.GetMovementRect().xMax) / EntryDuration);
        owner.transform.position = arena.Ground(entryPosition);
        // 同フレームの当たり判定にも安全な出現位置を反映する。
        Physics2D.SyncTransforms();
    }

    public void UpdateAI()
    {
        if (owner == null || data == null || owner.HP <= 0f || isDead) return;
        float delta = Time.deltaTime;
        if (delta <= 0f) return;
        // 進入中に通常移動の画面端Clampを通すと、安全な出現位置から瞬間移動してしまう。
        if (!hasEnteredArena)
        {
            gateAttackTimer = GateAttackDelay;
            return;
        }

        Rect rect = arena.GetMovementRect();
        bool hasGate = gateBody != null && gateBody.enabled && gateBody.gameObject.activeInHierarchy;
        // 長い砲身ではなく本体で停止位置を決め、プレイヤーが挟まれない幅を残す。
        float stopX = hasGate ? gateBody.bounds.max.x - bodyLeftOffset + GateClearance : rect.xMin;
        stopX = Mathf.Min(stopX, rect.xMax);
        Vector3 position = owner.transform.position;
        // 武器のノックバックでも右端や門の奥へ出ないよう、移動前に補正する。
        position.x = Mathf.Clamp(position.x, stopX, rect.xMax);
        position.x = Mathf.MoveTowards(position.x, stopX,
            Mathf.Max(0f, owner.GetMoveSpeed(data.speed)) * 0.3f * delta);
        owner.transform.position = arena.Ground(position);

        if (!hasGate || gate == null || gate.statusManager == null || position.x > stopX + 0.025f)
        {
            gateAttackTimer = GateAttackDelay;
            return;
        }

        gateAttackTimer -= delta;
        if (gateAttackTimer > 0f) return;
        gateAttackTimer = GateAttackInterval;
        if (MainDebugEnemySpawnWindow.IsDebugModeActive) return;

        gate.TakeDamage(GateAttackDamage);
    }

    private void FixedUpdate()
    {
        if (owner == null || !owner.isActiveAndEnabled || data == null || owner.HP <= 0f ||
            isDead || hasEnteredArena || Time.timeScale <= 0f) return;

        float targetX = arena.GetMovementRect().xMax;
        Vector3 position = owner.transform.position;
        if (position.x <= targetX + 0.001f)
        {
            hasEnteredArena = true;
            // MovePositionで生じた進入速度を通常前進へ持ち越さない。
            if (bodyRigidbody != null) bodyRigidbody.linearVelocity = Vector2.zero;
            return;
        }

        // ノックバック後も現位置から戻る。物理tickの移動をプレイヤー幅より十分小さくする。
        float maxStep = playerBody != null ? Mathf.Max(0.05f, playerBody.bounds.size.x * 0.25f) : 0.25f;
        float step = Mathf.Min(Mathf.Max(0f, owner.GetMoveSpeed(entrySpeed)) * Time.fixedDeltaTime, maxStep);
        position.x = Mathf.MoveTowards(position.x, targetX, step);
        position = arena.Ground(position);
        if (bodyRigidbody != null) bodyRigidbody.MovePosition(position);
        else owner.transform.position = position;
    }

    private void LateUpdate()
    {
        if (visual == null || owner == null || !owner.isActiveAndEnabled || owner.HP <= 0f || isDead || Time.deltaTime <= 0f) return;
        vibrationTime += Time.deltaTime;
        float offset = Mathf.Sin(vibrationTime * EngineVibrationFrequency * Mathf.PI * 2f) * EngineVibrationAmplitude;
        float parentScale = Mathf.Max(0.001f, Mathf.Abs(visual.parent.lossyScale.y));
        // 画像だけを揺らし、接地と前部の当たり判定は固定する。
        visual.localPosition = visualRestPosition + Vector3.up * (offset / parentScale);
    }

    public void OnAttack() { }

    public void OnDeath()
    {
        isDead = true;
        if (!hasEnteredArena && bodyRigidbody != null) bodyRigidbody.linearVelocity = Vector2.zero;
        if (visual != null) visual.localPosition = visualRestPosition;
    }

    private void OnDisable()
    {
        OnDeath();
    }
}
