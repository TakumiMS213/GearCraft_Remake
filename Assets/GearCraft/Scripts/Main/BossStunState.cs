using System;

/// <summary>
/// 出現時の最大HPに対して30%ずつ減った境界を、一度ずつスタンへ変換する。
/// スタン中の追加到達分は残り時間へ加算し、回復しても同じ境界は再使用しない。
/// </summary>
public sealed class BossStunState
{
    private readonly float maxHP;

    public int TriggerCount { get; private set; }
    public float RemainingTime { get; private set; }
    public bool IsActive => RemainingTime > 0f;

    public BossStunState(float maxHP)
    {
        this.maxHP = Math.Max(0f, maxHP);
    }

    public int ApplyDamage(float currentHP, float duration)
    {
        if (maxHP <= 0f || currentHP <= 0f || duration <= 0f) return 0;
        int previousCount = TriggerCount;
        while (TriggerCount < 3 &&
               currentHP <= maxHP * (100 - (TriggerCount + 1) * 30) / 100f)
        {
            TriggerCount++;
            RemainingTime += duration;
        }
        return TriggerCount - previousCount;
    }

    public void Tick(float deltaTime)
    {
        RemainingTime = Math.Max(0f, RemainingTime - Math.Max(0f, deltaTime));
    }

    public void Cancel()
    {
        RemainingTime = 0f;
    }
}
