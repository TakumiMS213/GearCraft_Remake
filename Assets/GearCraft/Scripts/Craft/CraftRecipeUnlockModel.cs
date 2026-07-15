using System;
using System.Collections.Generic;

namespace GearCraft.Scripts.Craft
{
    public sealed class CraftRecipeUnlockModel
    {
        private readonly IReadOnlyList<CraftRecipeSO> recipes;
        private readonly Dictionary<CraftRecipeSO, int> recipeOrder = new Dictionary<CraftRecipeSO, int>();

        public CraftRecipeUnlockModel(IReadOnlyList<CraftRecipeSO> recipes)
        {
            this.recipes = recipes ?? Array.Empty<CraftRecipeSO>();
            for (int i = 0; i < this.recipes.Count; i++)
            {
                CraftRecipeSO recipe = this.recipes[i];
                if (recipe != null && !recipeOrder.ContainsKey(recipe))
                {
                    recipeOrder.Add(recipe, i);
                }
            }
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

        private int CompareRecipes(CraftRecipeSO left, CraftRecipeSO right)
        {
            int categoryCompare = GetCategoryOrder(left).CompareTo(GetCategoryOrder(right));
            if (categoryCompare != 0)
            {
                return categoryCompare;
            }

            int leftOrder = GetRecipeOrder(left);
            int rightOrder = GetRecipeOrder(right);
            if (leftOrder != rightOrder)
            {
                return leftOrder.CompareTo(rightOrder);
            }

            return left.UnlockSortOrder.CompareTo(right.UnlockSortOrder);
        }

        private int GetRecipeOrder(CraftRecipeSO recipe)
        {
            int order;
            return recipe != null && recipeOrder.TryGetValue(recipe, out order) ? order : int.MaxValue;
        }

        private static int GetCategoryOrder(CraftRecipeSO recipe)
        {
            if (recipe == null)
            {
                return int.MaxValue;
            }

            switch (recipe.resultType)
            {
                case CraftResultType.Weapon:
                    return 0;
                case CraftResultType.PunkDrive:
                    return 1;
                case CraftResultType.Module:
                    return 2;
                default:
                    return 3;
            }
        }
    }
}
