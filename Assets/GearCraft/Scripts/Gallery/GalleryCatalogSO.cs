using System;
using System.Collections.Generic;
using UnityEngine;

namespace GearCraft.Gallery
{
    public enum GalleryCategory { Level1, Level2, Level3, WorkshopWeapons }
    public enum GalleryMaterial { None, Scrap, Gear }

    [Serializable]
    public sealed class GalleryEntry
    {
        public GalleryCategory category;
        public EnemyDataSO enemy;
        public WeaponDataSO weapon;
        public CraftRecipeSO recipe;
        public GalleryMaterial material;
        public Sprite artwork;
        public string displayName;

        [Header("Entry Details")]
        public string subtitle;
        public string designation;
        [TextArea(3, 8)] public string description;

        public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName
            : enemy != null ? enemy.enemyName : weapon != null ? weapon.weaponName
            : recipe != null ? recipe.DisplayName : material == GalleryMaterial.Scrap ? "スクラップ"
            : material == GalleryMaterial.Gear ? "歯車" : "？";
        public bool HasContent => enemy != null || weapon != null || recipe != null || material != GalleryMaterial.None;
        public bool IsDiscovered(GalleryProgressData progress) => progress != null && progress.hasReachedEnding
            && (enemy != null ? progress.HasEnemy(enemy.name) : weapon != null ? progress.HasWeapon(weapon.name)
                : recipe != null ? progress.HasRecipe(recipe.name) : progress.HasMaterial(material));

        public Sprite Artwork
        {
            get
            {
                if (artwork != null) return artwork;
                if (weapon != null) return weapon.icon;
                if (recipe != null) return recipe.icon != null ? recipe.icon : recipe.completedImage;
                if (enemy == null || enemy.prefab == null) return null;
                SpriteRenderer renderer = enemy.prefab.GetComponentInChildren<SpriteRenderer>(true);
                return renderer != null ? renderer.sprite : null;
            }
        }

        public static string CategoryName(GalleryCategory category)
        {
            switch (category)
            {
                case GalleryCategory.Level1: return "レベル1";
                case GalleryCategory.Level2: return "レベル2";
                case GalleryCategory.Level3: return "レベル3";
                default: return "工房武装";
            }
        }
    }

    [CreateAssetMenu(menuName = "GearCraft/Gallery Catalog", fileName = "GalleryCatalog")]
    public sealed class GalleryCatalogSO : ScriptableObject
    {
        public List<GalleryEntry> entries = new List<GalleryEntry>();

        public List<GalleryEntry> GetOrderedEntries()
        {
            var ordered = new List<GalleryEntry>();
            // Keep the catalogue order inside each section, including undiscovered slots.
            for (int category = 0; category <= (int)GalleryCategory.WorkshopWeapons; category++)
                foreach (GalleryEntry entry in entries)
                    if (entry != null && (int)entry.category == category && entry.HasContent)
                        ordered.Add(entry);
            return ordered;
        }
    }
}
