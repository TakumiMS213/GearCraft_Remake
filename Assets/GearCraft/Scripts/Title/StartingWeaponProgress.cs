using UnityEngine;

/// <summary>AR-227's permanent reward is independent of the current run and gallery discoveries.</summary>
public static class StartingWeaponProgress
{
    public const string SaveKey = "GearCraft.StartingWeaponSelection.Unlocked.v1";
    public const string SelectionEnabledKey = "GearCraft.StartingWeaponSelection.Enabled.v1";
    public static bool IsUnlocked => PlayerPrefs.GetInt(SaveKey, 0) == 1;
    // Until the player chooses a setting, follow the normal AR-227 reward.
    // An explicit ON or OFF takes precedence without changing the defeat record.
    public static bool IsSelectionEnabled => PlayerPrefs.GetInt(SelectionEnabledKey, IsUnlocked ? 1 : 0) == 1;

    public static void SetSelectionEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(SelectionEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.DeleteKey(SelectionEnabledKey);
        PlayerPrefs.Save();
    }

    public static void RecordDefeat(EnemyDataSO enemy)
    {
        // Use the actual enemy asset ID; AI enum labels do not match boss order.
        if (enemy == null || enemy.name != "lv3_AR-227" ||
            !enemy.IsBossType || MainDebugEnemySpawnWindow.IsDebugModeActive || IsUnlocked) return;
        PlayerPrefs.SetInt(SaveKey, 1);
        PlayerPrefs.Save();
    }
}
