using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GearCraft.Scripts.Craft
{
    public sealed class CraftRequiredWeaponTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CraftRecipeSO recipe;

        public void Configure(CraftRecipeSO recipeData)
        {
            recipe = recipeData;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (recipe == null || recipe.requiredWeapon == null)
            {
                return;
            }

            TooltipSystem tooltipSystem = TooltipSystem.FindInstance();
            if (tooltipSystem != null)
            {
                tooltipSystem.Show(BuildTooltipText(), transform as RectTransform);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip();
        }

        private void OnDisable()
        {
            HideTooltip();
        }

        private string BuildTooltipText()
        {
            WeaponDataSO weapon = recipe != null ? recipe.requiredWeapon : null;
            if (weapon == null)
            {
                return string.Empty;
            }

            string weaponName = string.IsNullOrWhiteSpace(weapon.weaponName) ? weapon.name : weapon.weaponName;
            var text = new StringBuilder(weaponName);
            if (!string.IsNullOrWhiteSpace(weapon.description))
            {
                text.Append('\n').Append(weapon.description);
            }

            string acquisition = GetAcquisitionMethod(weaponName);
            if (!string.IsNullOrEmpty(acquisition))
            {
                text.Append("\n入手方法: ").Append(acquisition);
            }

            text.Append("\nクラフト条件: この武器を装備していること。");
            StatusManager status = StatusManager.Instance;
            if (status != null)
            {
                string state = CraftManager.MeetsRequiredWeapon(recipe) ? "装備中"
                    : IsOwned(status, weapon) ? "所持済み・未装備" : "未入手";
                text.Append("\n現在: ").Append(state);
            }

            return text.ToString();
        }

        private static string GetAcquisitionMethod(string weaponName)
        {
            if (string.Equals(weaponName, "Katana", StringComparison.OrdinalIgnoreCase))
            {
                return "ゲーム開始時の初期装備。";
            }

            if (string.Equals(weaponName, "AR", StringComparison.OrdinalIgnoreCase))
            {
                return "ステージ報酬の「AR獲得」カードを選ぶ（報酬はランダム）。";
            }

            return string.Empty;
        }

        private static bool IsOwned(StatusManager status, WeaponDataSO weapon)
        {
            if (status.ownedWeapons == null)
            {
                return false;
            }

            foreach (WeaponDataSO owned in status.ownedWeapons)
            {
                if (owned == weapon || owned != null && !string.IsNullOrEmpty(weapon.weaponName) &&
                    owned.weaponName == weapon.weaponName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void HideTooltip()
        {
            TooltipSystem tooltipSystem = TooltipSystem.FindInstance();
            if (tooltipSystem != null)
            {
                tooltipSystem.Hide();
            }
        }
    }
}
