using UnityEngine;

[CreateAssetMenu(menuName = "GearCraft/Stage Generator Config", fileName = "StageGeneratorConfig")]
public class StageGeneratorSO : ScriptableObject
{
    [Header("ステージ設定")]
    public int totalStages = 31;
    public int[] bossStages = { 10, 20, 30, 31 };
    public int stagesPerRest = 3;        // 宿舎に戻るまでのステージ数

    [Header("難易度カーブ")]
    [Tooltip("X軸=進行度(0.0-1.0)またはステージ番号(0-31), Y軸=難易度(0.0-1.0)")]
    public AnimationCurve difficultyCurve = AnimationCurve.Linear(0, 0, 31, 1);

    [Header("敵プール")]
    public EnemyDataSO[] normalEnemyPool;
    [Tooltip("bossStages配列に対応するボスデータ（同じインデックス）")]
    public EnemyDataSO[] bossPool;

    [Header("スポーン設定")]
    public int baseEnemyCount = 3;
    public int maxEnemyCount = 12;
    [Tooltip("初日だけ敵出現数を抑える。2日目以降は通常カーブを使用する")]
    public int firstDayEnemyCount = 2;
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
        if (difficultyCurve == null || difficultyCurve.length == 0)
        {
            return 0f;
        }

        float lastKeyTime = difficultyCurve.keys[difficultyCurve.length - 1].time;
        float curveTime = lastKeyTime <= 1.01f
            ? GetNormalizedStageProgress(stageNum)
            : stageNum;

        return Mathf.Clamp01(difficultyCurve.Evaluate(curveTime));
    }

    private float GetNormalizedStageProgress(int stageNum)
    {
        int finalStage = Mathf.Max(1, totalStages);
        return Mathf.Clamp01((float)stageNum / finalStage);
    }

    /// <summary>
    /// 指定ステージの敵数を返す
    /// </summary>
    public int GetEnemyCount(int stageNum)
    {
        if (IsFirstDayStage(stageNum))
        {
            return Mathf.Max(1, firstDayEnemyCount);
        }

        float difficulty = GetDifficulty(stageNum);
        return Mathf.RoundToInt(Mathf.Lerp(baseEnemyCount, maxEnemyCount, difficulty));
    }

    private bool IsFirstDayStage(int stageNum)
    {
        int firstDayStageCount = Mathf.Max(1, stagesPerRest);
        return stageNum >= 1 && stageNum <= firstDayStageCount;
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
