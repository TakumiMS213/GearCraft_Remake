using System;
using System.Collections.Generic;
using UnityEngine;

namespace GearCraft.Gallery
{
    /// <summary>Discovery is recorded before the first ending and survives run resets.</summary>
    [Serializable]
    public sealed class GalleryProgressData
    {
        public bool hasReachedEnding;
        public List<string> encounteredEnemies = new List<string>();
        public List<string> acquiredWeapons = new List<string>();
        public List<string> craftedRecipes = new List<string>();
        public List<GalleryMaterial> acquiredMaterials = new List<GalleryMaterial>();

        public bool HasEnemy(string id) => !string.IsNullOrEmpty(id) && encounteredEnemies != null && encounteredEnemies.Contains(id);
        public bool HasWeapon(string id) => !string.IsNullOrEmpty(id) && acquiredWeapons != null && acquiredWeapons.Contains(id);
        public bool HasRecipe(string id) => !string.IsNullOrEmpty(id) && craftedRecipes != null && craftedRecipes.Contains(id);
        public bool HasMaterial(GalleryMaterial material) => material != GalleryMaterial.None
            && acquiredMaterials != null && acquiredMaterials.Contains(material);

        public bool RecordEnemy(string id)
        {
            if (string.IsNullOrEmpty(id) || HasEnemy(id)) return false;
            if (encounteredEnemies == null) encounteredEnemies = new List<string>();
            encounteredEnemies.Add(id);
            return true;
        }

        public bool RecordWeapon(string id)
        {
            if (string.IsNullOrEmpty(id) || HasWeapon(id)) return false;
            if (acquiredWeapons == null) acquiredWeapons = new List<string>();
            acquiredWeapons.Add(id);
            return true;
        }

        public bool RecordRecipe(string id)
        {
            if (string.IsNullOrEmpty(id) || HasRecipe(id)) return false;
            if (craftedRecipes == null) craftedRecipes = new List<string>();
            craftedRecipes.Add(id);
            return true;
        }

        public bool RecordMaterial(GalleryMaterial material)
        {
            if (material == GalleryMaterial.None || HasMaterial(material)) return false;
            if (acquiredMaterials == null) acquiredMaterials = new List<GalleryMaterial>();
            acquiredMaterials.Add(material);
            return true;
        }

        public bool RecordEnding(int ending)
        {
            if (ending < 1 || ending > 3 || hasReachedEnding) return false;
            hasReachedEnding = true;
            return true;
        }
    }

    public static class GalleryProgress
    {
        public const string SaveKey = "GearCraft.Gallery.Progress.v1";
        private static GalleryProgressData data;
        public static event Action Changed;

        public static GalleryProgressData Data
        {
            get
            {
                if (data != null) return data;
                string json = PlayerPrefs.GetString(SaveKey, string.Empty);
                try
                {
                    data = string.IsNullOrEmpty(json) ? new GalleryProgressData() : JsonUtility.FromJson<GalleryProgressData>(json);
                }
                catch (ArgumentException)
                {
                    Debug.LogWarning("Gallery progress could not be read. Starting an empty discovery record.");
                }
                return data ?? (data = new GalleryProgressData());
            }
        }

        public static bool IsUnlocked => Data.hasReachedEnding;

        // Asset names are stable record IDs; translated display names never affect discovery.
        public static bool HasEncountered(EnemyDataSO enemy) => enemy != null && Data.HasEnemy(enemy.name);

        public static void RecordEnemy(EnemyDataSO enemy)
        {
            if (enemy != null && Data.RecordEnemy(enemy.name)) Save();
        }

        public static void RecordWeapon(WeaponDataSO weapon)
        {
            if (weapon != null && Data.RecordWeapon(weapon.name)) Save();
        }

        public static void RecordCraft(CraftRecipeSO recipe)
        {
            if (recipe != null && recipe.resultType != CraftResultType.Weapon && Data.RecordRecipe(recipe.name)) Save();
        }

        public static void RecordMaterials(int scrap, int gear)
        {
            bool changed = false;
            if (scrap > 0) changed |= Data.RecordMaterial(GalleryMaterial.Scrap);
            if (gear > 0) changed |= Data.RecordMaterial(GalleryMaterial.Gear);
            if (changed) Save();
        }

        public static void RecordEnding(int ending)
        {
            if (Data.RecordEnding(ending)) Save();
        }

        private static void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            data = null;
            Changed = null;
        }
    }
}
