using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>課題の途中経過。習得フラグとは別に、現在のプレイ中だけ保持する。</summary>
[Serializable]
public sealed class CraftTutorialRunProgress
{
    public CraftTutorialTrackProgress craft = new CraftTutorialTrackProgress();
    public CraftTutorialTrackProgress weaponCustom = new CraftTutorialTrackProgress();

    public void Reset()
    {
        craft = new CraftTutorialTrackProgress();
        weaponCustom = new CraftTutorialTrackProgress();
    }
}

[Serializable]
public sealed class CraftTutorialTrackProgress
{
    public int stepIndex;
    public bool showingFeedback;
    public string feedback;
    public int hintLevel;
    public bool usedHint;
    public int hintRequests;
    public int invalidAttempts;
    public float elapsedSeconds;
    public bool baselineCaptured;
    public float baselineDamage;
    public float baselineAttackSpeed;
    public string practicePartName;
    public List<UpgradePartSO> baselineParts = new List<UpgradePartSO>();
    public List<string> craftedRecipeIds = new List<string>();
    public bool practiceSuppliesGranted;
    public bool challengeSuppliesGranted;
    public bool mastered;
}

[Serializable]
public sealed class CraftTutorialStats
{
    public int taskAttempts;
    public int completions;
    public int independentAttempts;
    public int independentSuccesses;
    public int hintRequests;
    public int invalidAttempts;
    public int pauses;
    public float elapsedSeconds;
}

/// <summary>ローカルの Console と PlayerPrefs だけに学習結果を記録する。</summary>
public static class CraftTutorialMetrics
{
    public const string CraftMasteryKey = "GearCraft.CraftTutorial.Mastery.Craft.v5";
    public const string WeaponMasteryKey = "GearCraft.CraftTutorial.Mastery.WeaponCustom.v5";

    [Serializable]
    private sealed class TaskRecord
    {
        public string tutorial;
        public string goal;
        public string outcome;
        public float seconds;
        public int hints;
        public int invalidAttempts;
        public bool independent;
    }

    public static void Record(int tab, string goal, string outcome, float seconds,
        int hints, int invalidAttempts, bool independent)
    {
        float duration = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0f : Mathf.Max(0f, seconds);
        CraftTutorialStats stats = GetStats(tab);
        switch (outcome)
        {
            case "started":
                stats.taskAttempts++;
                if (independent)
                    stats.independentAttempts++;
                break;
            case "success":
                stats.completions++;
                if (independent && hints == 0)
                    stats.independentSuccesses++;
                stats.elapsedSeconds += duration;
                break;
            case "hint":
                stats.hintRequests++;
                break;
            case "invalid":
                stats.invalidAttempts++;
                break;
            case "paused":
                stats.pauses++;
                stats.elapsedSeconds += duration;
                break;
        }

        PlayerPrefs.SetString(GetMetricsKey(tab), JsonUtility.ToJson(stats));
        PlayerPrefs.Save();
        Debug.Log("[CraftTutorial] " + JsonUtility.ToJson(new TaskRecord
        {
            tutorial = GetTutorialName(tab),
            goal = goal ?? string.Empty,
            outcome = outcome ?? string.Empty,
            seconds = duration,
            hints = Mathf.Max(0, hints),
            invalidAttempts = Mathf.Max(0, invalidAttempts),
            independent = independent
        }));
    }

    public static CraftTutorialStats GetStats(int tab)
    {
        string json = PlayerPrefs.GetString(GetMetricsKey(tab), string.Empty);
        if (string.IsNullOrEmpty(json))
            return new CraftTutorialStats();

        try
        {
            return JsonUtility.FromJson<CraftTutorialStats>(json) ?? new CraftTutorialStats();
        }
        catch (ArgumentException)
        {
            return new CraftTutorialStats();
        }
    }

    public static void Reset(int tab)
    {
        PlayerPrefs.DeleteKey(GetMetricsKey(tab));
        PlayerPrefs.Save();
    }

    private static string GetMetricsKey(int tab)
    {
        return "GearCraft.CraftTutorial.Metrics." + GetTutorialName(tab) + ".v5";
    }

    private static string GetTutorialName(int tab)
    {
        return tab == 1 ? "WeaponCustom" : "Craft";
    }
}
