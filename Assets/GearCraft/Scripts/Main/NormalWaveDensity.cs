using System.Collections.Generic;

/// <summary>通常ウェーブ内の各敵種を増員する。1.5倍の端数は同じ敵種の次の枠へ繰り越す。</summary>
public sealed class NormalWaveDensity
{
    private readonly Dictionary<EnemyDataSO, int> occurrences = new Dictionary<EnemyDataSO, int>();

    public int GetSpawnCount(EnemyDataSO enemy)
    {
        if (enemy == null || enemy.IsBossType) return 1;

        switch (enemy.enemyName)
        {
            case "A-1":
            case "EA-21":
                return 2;
            case "B-1":
            case "BB-12":
            case "G-1":
            case "EG-33":
                occurrences.TryGetValue(enemy, out int previous);
                occurrences[enemy] = previous + 1;
                // 2体、1体を交互に追加し、奇数枠では0.5体分だけ切り上げる。
                return previous % 2 == 0 ? 2 : 1;
            default:
                return 1;
        }
    }
}
