using GearCraft.Gallery;
using UnityEngine;
using System.Threading.Tasks;

/// <summary>Explicit player-requested reset, separate from starting a new run.</summary>
public static class PlayerProgressReset
{
    public static async Task Reset()
    {
        if (RunSaveService.Instance != null) await RunSaveService.Instance.DeleteAllAsync();
        else await new RunSaveStore(System.IO.Path.Combine(Application.persistentDataPath, "AutoSave")).DeleteAllAsync();
        PlayerPrefs.DeleteKey(RunSaveService.EndedRunKey);
        UpgradeGridManager.Instance?.DiscardRuntimeState();
        StoryPlaybackRequest.ClearRequest();
        StatusManager.Instance?.ResetRunProgress();
        if (StageCounter.Instance != null) StageCounter.Instance.StageCount = 1;
        MaterialManager.Instance?.ClearAllMaterials();

        StartingWeaponProgress.ResetProgress();
        CraftTutorialMetrics.ResetProgress();
        PlayerPrefs.DeleteKey(StageFlowManager.OpeningNoticeSeenKey);

        // Run initialization can record the default weapon as discovered.
        // Clear the gallery last, including its in-memory cache and visible unlock state.
        GalleryProgress.ResetProgress();
        PlayerPrefs.Save();
    }
}
