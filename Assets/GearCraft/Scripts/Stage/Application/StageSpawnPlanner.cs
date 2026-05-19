using UnityEngine;

public sealed class StageSpawnPlanner
{
    private readonly StageGeneratorSO stageConfig;

    public StageSpawnPlanner(StageGeneratorSO stageConfig)
    {
        this.stageConfig = stageConfig;
    }

    public StageSpawnPlan CreatePlan(int stageNum)
    {
        if (stageConfig == null || !stageConfig.IsBossStage(stageNum))
        {
            return new StageSpawnPlan(false, 0, null, false);
        }

        int minionCount = Mathf.Max(2, stageConfig.baseEnemyCount);
        EnemyDataSO bossData = ResolveBossData(stageNum);
        bool markLastMinionAsStageEnd = bossData == null || bossData.prefab == null;
        return new StageSpawnPlan(true, minionCount, bossData, markLastMinionAsStageEnd);
    }

    private EnemyDataSO ResolveBossData(int stageNum)
    {
        int bossIndex = stageConfig.GetBossIndex(stageNum);
        if (stageConfig.bossPool == null || bossIndex < 0 || bossIndex >= stageConfig.bossPool.Length)
        {
            return null;
        }

        return stageConfig.bossPool[bossIndex];
    }
}
