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
            DeleteKey(OpeningNoticeKey);
            PlayerPrefs.Save();

            EditorUtility.DisplayDialog(
                "PlayerPrefs Reset",
                "初回チュートリアルと初回手紙表示のフラグをリセットしました。",
                "OK");
        }

        [MenuItem("GearCraft/PlayerPrefs/Reset Craft Tutorial")]
        private static void ResetCraftTutorial()
        {
            DeleteKey(CraftTutorialKey);
            PlayerPrefs.Save();
        }

        [MenuItem("GearCraft/PlayerPrefs/Reset WeaponCustom Tutorial")]
        private static void ResetWeaponCustomTutorial()
        {
            DeleteKey(WeaponCustomTutorialKey);
            PlayerPrefs.Save();
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
