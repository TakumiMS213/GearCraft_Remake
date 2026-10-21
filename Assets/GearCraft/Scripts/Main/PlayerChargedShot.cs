using System;

/// <summary>Release-to-fire charge. Reload time and charge speed are independent.</summary>
public sealed class PlayerChargedShot
{
    private bool armed;
    private bool charging;
    private double charge;
    private double remaining;
    public bool IsCharging => charging;
    public float ChargeFraction => (float)charge;
    public float RemainingSeconds => (float)remaining;

    public void Cancel()
    {
        armed = charging = false;
        charge = 0;
    }

    public bool Advance(bool pressed, bool held, bool allowed, float deltaTime,
        float chargeDuration, float reloadDuration, out float releasedCharge)
    {
        releasedCharge = 0;
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0 ||
            float.IsNaN(chargeDuration) || float.IsInfinity(chargeDuration) ||
            float.IsNaN(reloadDuration) || float.IsInfinity(reloadDuration))
        {
            Cancel();
            return false;
        }

        double previousRemaining = remaining;
        remaining = Math.Max(0, remaining - deltaTime);
        if (!allowed)
        {
            Cancel();
            return false;
        }

        // Only a fresh press arms the weapon; a menu/switch cannot resume a held charge.
        if (pressed) armed = true;
        if (!armed) return false;
        if (remaining > 0.0000001)
        {
            if (!held) Cancel();
            return false;
        }

        charging = true;
        // A newly observed press has no known hold duration. Later frames include
        // the elapsed time up to release, excluding time spent reloading.
        double elapsed = pressed ? 0 : Math.Max(0, deltaTime - previousRemaining);
        charge = Math.Min(1, charge + elapsed / Math.Max(1.0 / 60.0, chargeDuration));
        if (held) return false;

        releasedCharge = (float)charge;
        remaining = Math.Max(0, reloadDuration);
        Cancel();
        return true;
    }
}
