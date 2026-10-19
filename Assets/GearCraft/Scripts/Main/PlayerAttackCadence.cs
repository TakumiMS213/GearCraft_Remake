using System;

/// <summary>
/// 入力と経過時間から攻撃回数を決める。描画fpsで速射性能を変えない。
/// 射撃は従来の60fpsでの発射間隔を基準にし、低fps時だけ不足分を補う。
/// </summary>
public sealed class PlayerAttackCadence
{
    private const double ReferenceRate = 60.0;
    private const double ReadyTolerance = 0.000000001;
    private const int MaximumAttacksPerFrame = 6;
    private double remaining;
    private bool wasHeld;

    public double RemainingSeconds => Math.Max(0.0, remaining);

    public static double GetAutomaticInterval(float nominalInterval)
    {
        if (float.IsNaN(nominalInterval) || float.IsInfinity(nominalInterval))
            return double.PositiveInfinity;

        // floatの0.1秒等を余分な1tickへ切り上げない。
        double roundingTolerance = Math.Max(0.00001, Math.Abs((double)nominalInterval) * 0.00001);
        double ticks = Math.Max(1.0, Math.Ceiling(nominalInterval * ReferenceRate - roundingTolerance));
        return ticks / ReferenceRate;
    }

    public void Interrupt()
    {
        remaining = Math.Max(0.0, remaining);
        wasHeld = false;
    }

    public int Advance(bool pressed, bool held, bool automatic, bool allowed,
        float deltaTime, float nominalInterval)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f ||
            float.IsNaN(nominalInterval) || float.IsInfinity(nominalInterval))
        {
            Interrupt();
            return 0;
        }

        remaining -= deltaTime;
        if (!allowed || (automatic ? !held : !pressed))
        {
            Interrupt();
            return 0;
        }

        if (!automatic)
        {
            Interrupt();
            if (remaining > ReadyTolerance) return 0;
            remaining = Math.Max(0.0, nominalInterval);
            return 1;
        }

        bool continuing = wasHeld;
        wasHeld = true;
        if (!continuing) remaining = Math.Max(0.0, remaining);

        double interval = GetAutomaticInterval(nominalInterval);
        int attacks = 0;
        while (remaining <= ReadyTolerance && attacks < MaximumAttacksPerFrame)
        {
            attacks++;
            remaining += interval;
            // 押し始めより前の経過時間は射撃回数へ変換しない。
            if (!continuing) break;
        }

        // 長い停止後に、過去の射撃を何フレームも遅れて連射し続けない。
        if (attacks == MaximumAttacksPerFrame && remaining <= ReadyTolerance)
            remaining = interval;
        return attacks;
    }
}
