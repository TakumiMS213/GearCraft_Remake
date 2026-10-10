using UnityEditor;
using UnityEngine;

namespace GearCraft.Scripts.Editor
{
    public static class PlayerPrefsResetMenu
    {
        private const string CraftTutorialKey = "GearCraft.CraftTutorial.AutoPlayed.Craft.v4";
        private const string WeaponCustomTutorialKey = "GearCraft.CraftTutorial.AutoPlayed.WeaponCustom.v3";
        private const string OpeningNoticeKey = "GearCraft.Main.OpeningNotice.Seen.v1";

        [MenuItem("GearCraft/PlayerPrefs/Reset First-Time Flags")]
        private static void ResetFirstTimeFlags()
        {
            DeleteKey(CraftTutorialKey);
            DeleteKey(WeaponCustomTutorialKey);
            DeleteKey(CraftTutorialMetrics.CraftMasteryKey);
            DeleteKey(CraftTutorialMetrics.WeaponMasteryKey);
            DeleteKey(OpeningNoticeKey);
            PlayerPrefs.Save();

            EditorUtility.DisplayDialog(
                "PlayerPrefs Reset",
                "クラフトチュートリアルの習得済みフラグと初回手紙表示のフラグをリセットしました。",
                "OK");
        }

        [MenuItem("GearCraft/PlayerPrefs/Reset Craft Tutorial")]
        private static void ResetCraftTutorial()
        {
            DeleteKey(CraftTutorialKey);
            DeleteKey(CraftTutorialMetrics.CraftMasteryKey);
            PlayerPrefs.Save();
        }

        [MenuItem("GearCraft/PlayerPrefs/Reset WeaponCustom Tutorial")]
        private static void ResetWeaponCustomTutorial()
        {
            DeleteKey(WeaponCustomTutorialKey);
            DeleteKey(CraftTutorialMetrics.WeaponMasteryKey);
            PlayerPrefs.Save();
        }

        [MenuItem("GearCraft/PlayerPrefs/Log Tutorial Metrics")]
        private static void LogTutorialMetrics()
        {
            Debug.Log("[CraftTutorial Metrics] Craft\n" + JsonUtility.ToJson(CraftTutorialMetrics.GetStats(0), true));
            Debug.Log("[CraftTutorial Metrics] WeaponCustom\n" + JsonUtility.ToJson(CraftTutorialMetrics.GetStats(1), true));
        }

        [MenuItem("GearCraft/PlayerPrefs/Reset Tutorial Metrics")]
        private static void ResetTutorialMetrics()
        {
            CraftTutorialMetrics.Reset(0);
            CraftTutorialMetrics.Reset(1);
            Debug.Log("クラフトチュートリアルのローカル計測データをリセットしました。");
        }

        [MenuItem("GearCraft/PlayerPrefs/Reset Opening Notice")]
        private static void ResetOpeningNotice()
        {
            DeleteKey(OpeningNoticeKey);
            PlayerPrefs.Save();
        }

        private static void DeleteKey(string key)
        {
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
