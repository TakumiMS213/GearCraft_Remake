using UnityEngine;

[CreateAssetMenu(menuName = "GearCraft/Stage Generator Config", fileName = "StageGeneratorConfig")]
public class StageGeneratorSO : ScriptableObject
{
    [Header("ステージ設定")]
    public int totalStages = 31;
    public int[] bossStages = { 10, 20, 30, 31 };
    public int stagesPerRest = 3;        // 宿舎に戻るまでのステージ数

    [Header("難易度カーブ")]
    [Tooltip("X軸=ステージ番号(0-31), Y軸=難易度(0.0-1.0)")]
    public AnimationCurve difficultyCurve = AnimationCurve.Linear(0, 0, 31, 1);

    [Header("敵プール")]
    public EnemyDataSO[] normalEnemyPool;
    [Tooltip("bossStages配列に対応するボスデータ（同じインデックス）")]
    public EnemyDataSO[] bossPool;

    [Header("スポーン設定")]
    public int baseEnemyCount = 3;
    public int maxEnemyCount = 12;
    public float baseSpawnInterval = 3f;
    public float minSpawnInterval = 0.8f;

    [Header("ドロップ乗算")]
    [Tooltip("ステージ番号に応じたドロップ量の倍率カーブ")]
    public AnimationCurve dropMultiplierCurve = AnimationCurve.Linear(0, 1, 31, 3);

    /// <summary>
    /// 指定ステージがボスステージかどうか
    /// </summary>
    public bool IsBossStage(int stageNum)
    {
        if (bossStages == null) return false;
        foreach (int bs in bossStages)
            if (bs == stageNum) return true;
        return false;
    }

    /// <summary>
    /// ボスステージのインデックス（何体目のボスか）を返す。ボスでなければ-1
    /// </summary>
    public int GetBossIndex(int stageNum)
    {
        if (bossStages == null) return -1;
        for (int i = 0; i < bossStages.Length; i++)
            if (bossStages[i] == stageNum) return i;
        return -1;
    }

    /// <summary>
    /// 指定ステージの難易度を0-1で返す
    /// </summary>
    public float GetDifficulty(int stageNum)
    {
        return difficultyCurve.Evaluate(stageNum);
    }

    /// <summary>
    /// 指定ステージの敵数を返す
    /// </summary>
    public int GetEnemyCount(int stageNum)
    {
        float difficulty = GetDifficulty(stageNum);
        return Mathf.RoundToInt(Mathf.Lerp(baseEnemyCount, maxEnemyCount, difficulty));
    }

    /// <summary>
    /// 指定ステージのスポーン間隔を返す
    /// </summary>
    public float GetSpawnInterval(int stageNum)
    {
        float difficulty = GetDifficulty(stageNum);
        return Mathf.Lerp(baseSpawnInterval, minSpawnInterval, difficulty);
    }

    /// <summary>
    /// 指定ステージの宿舎帰還判定
    /// </summary>
    public bool IsRestStage(int stageNum)
    {
        return stageNum > 0 && stageNum % stagesPerRest == 0 && !IsBossStage(stageNum);
    }
}
