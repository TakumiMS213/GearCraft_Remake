using System;
using System.Collections.Generic;

namespace GearCraft.Scripts.Craft
{
    public sealed class CraftRecipeUnlockModel
    {
        private readonly IReadOnlyList<CraftRecipeSO> recipes;

        public CraftRecipeUnlockModel(IReadOnlyList<CraftRecipeSO> recipes)
        {
            this.recipes = recipes ?? Array.Empty<CraftRecipeSO>();
        }

        public void CollectUnlockedRecipes(int bossKillCount, List<CraftRecipeSO> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (int i = 0; i < recipes.Count; i++)
            {
                CraftRecipeSO recipe = recipes[i];
                if (IsUnlocked(recipe, bossKillCount))
                {
                    results.Add(recipe);
                }
            }

            results.Sort(CompareRecipes);
        }

        public void CollectNewUnlocks(
            int bossKillCount,
            Predicate<string> isNotified,
            List<CraftRecipeSO> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (int i = 0; i < recipes.Count; i++)
            {
                CraftRecipeSO recipe = recipes[i];
                if (!IsUnlocked(recipe, bossKillCount) || recipe.UnlockBossKills <= 0)
                {
                    continue;
                }

                string unlockId = recipe.UnlockId;
                if (isNotified != null && isNotified(unlockId))
                {
                    continue;
                }

                results.Add(recipe);
            }

            results.Sort(CompareRecipes);
        }

        private static bool IsUnlocked(CraftRecipeSO recipe, int bossKillCount)
        {
            return recipe != null &&
                !recipe.HiddenFromUnlockList &&
                bossKillCount >= recipe.UnlockBossKills;
        }

        private static int CompareRecipes(CraftRecipeSO left, CraftRecipeSO right)
        {
            int bossCompare = left.UnlockBossKills.CompareTo(right.UnlockBossKills);
            return bossCompare != 0 ? bossCompare : left.UnlockSortOrder.CompareTo(right.UnlockSortOrder);
        }
    }
}
