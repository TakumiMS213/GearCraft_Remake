public readonly struct StageSpawnPlan
{
    public StageSpawnPlan(bool isBossStage, int minionCount, EnemyDataSO bossData, bool markLastMinionAsStageEnd)
    {
        IsBossStage = isBossStage;
        MinionCount = minionCount;
        BossData = bossData;
        MarkLastMinionAsStageEnd = markLastMinionAsStageEnd;
    }

    public bool IsBossStage { get; }
    public int MinionCount { get; }
    public EnemyDataSO BossData { get; }
    public bool HasBoss => BossData != null && BossData.prefab != null;
    public bool MarkLastMinionAsStageEnd { get; }
}
