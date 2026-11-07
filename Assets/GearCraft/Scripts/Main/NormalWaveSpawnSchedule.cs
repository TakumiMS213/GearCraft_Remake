using System;
using System.Collections.Generic;

/// <summary>総数を変えず、地上敵の待ち時間に後続の飛行敵を繰り上げる。</summary>
public sealed class NormalWaveSpawnSchedule
{
    public const float MinimumGroundInterval = 3f;
    public const float GroundClearance = 0.75f;
    public const float EmptyScreenInterval = 0.25f;

    public readonly struct Entry
    {
        public Entry(EnemyDataSO enemy, float interval, bool ground)
        {
            Enemy = enemy;
            Interval = interval;
            Ground = ground;
        }

        public EnemyDataSO Enemy { get; }
        public float Interval { get; }
        public bool Ground { get; }
    }

    private readonly List<Entry> pending = new List<Entry>();
    public int Count => pending.Count;
    public void Add(EnemyDataSO enemy, float interval, bool ground) => pending.Add(new Entry(enemy, interval, ground));

    public int FindNext(bool due, bool emptyScreenAdvance, Func<EnemyDataSO, bool> groundReady,
        Func<EnemyDataSO, float> entrySeconds)
    {
        if (!due && !emptyScreenAdvance) return -1;

        // 通常時は予定順。地上敵が詰まっている場合だけ後続を先に出す。
        if (due)
        {
            for (int i = 0; i < pending.Count; i++)
                if (!pending[i].Ground || groundReady(pending[i].Enemy)) return i;
            return -1;
        }

        // 空画面の穴埋めは、画面への到着が早い飛行敵を優先する。
        int fastest = -1;
        float earliest = float.PositiveInfinity;
        for (int i = 0; i < pending.Count; i++)
        {
            if (pending[i].Ground) continue;
            float seconds = entrySeconds(pending[i].Enemy);
            if (seconds < earliest)
            {
                fastest = i;
                earliest = seconds;
            }
        }
        if (fastest >= 0) return fastest;

        for (int i = 0; i < pending.Count; i++)
            if (groundReady(pending[i].Enemy)) return i;
        return -1;
    }

    public Entry Take(int index)
    {
        Entry entry = pending[index];
        pending.RemoveAt(index);
        return entry;
    }

    public static bool HasGroundClearance(float elapsed, float distance, float previousWidth, float nextWidth)
    {
        return elapsed >= MinimumGroundInterval &&
            distance >= (previousWidth + nextWidth) * 0.5f + GroundClearance;
    }
}
