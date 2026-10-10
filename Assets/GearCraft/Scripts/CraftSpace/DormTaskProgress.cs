using System;

public enum DormTaskPhase
{
    Preparation,
    FirstBossBuild,
    Defense
}

/// <summary>1回のプレイ中、シーンを移動しても保持する宿舎タスクの達成状況。</summary>
[Serializable]
public sealed class DormTaskProgress
{
    public bool hasStartedFirstDay;
    public bool hasCrafted;
    public bool hasUpgradedWeapon;
    public bool hasEncounteredFirstBoss;

    public DormTaskPhase GetPhase(int stageNumber, int bossKills, int firstBossStage)
    {
        if (hasEncounteredFirstBoss || bossKills > 0 || stageNumber > firstBossStage)
            return DormTaskPhase.Defense;

        if (!hasStartedFirstDay && stageNumber <= 1)
            return DormTaskPhase.Preparation;

        return DormTaskPhase.FirstBossBuild;
    }

    public void Reset()
    {
        hasStartedFirstDay = false;
        hasCrafted = false;
        hasUpgradedWeapon = false;
        hasEncounteredFirstBoss = false;
    }
}
