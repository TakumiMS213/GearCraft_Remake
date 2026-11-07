using GearCraft.Gallery;
using UnityEngine;

/// <summary>Explicit player-requested reset, separate from starting a new run.</summary>
public static class PlayerProgressReset
{
    public static void Reset()
    {
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
