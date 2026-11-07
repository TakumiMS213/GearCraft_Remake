using UnityEngine;

/// <summary>Player and gate enemy hits always remove one health segment.</summary>
public static class HealthGaugeRules
{
    public const int PointsPerSegment = 10;
    public const int DefaultSegments = 10;

    // Keep a partially healed final segment visible until health reaches zero.
    public static int FilledSegments(float health) =>
        Mathf.CeilToInt(Mathf.Max(0f, health) / PointsPerSegment);
}
