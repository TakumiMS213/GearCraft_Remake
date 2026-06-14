using UnityEngine;
using UnityEngine.EventSystems;

namespace GearCraft.Scripts.Craft
{
    public sealed class CraftMaterialTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private MaterialManager.MaterialType materialType;

        public void Configure(MaterialManager.MaterialType type)
        {
            materialType = type;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            TooltipSystem tooltipSystem = TooltipSystem.FindInstance();
            if (tooltipSystem == null)
            {
                return;
            }

            tooltipSystem.Show(BuildTooltipText(), transform as RectTransform);
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
            int current = MaterialManager.Instance != null ? MaterialManager.Instance.GetMaterial(materialType) : 0;
            return $"{GetDisplayName(materialType)}\n入手方法: {GetDropMethod(materialType)}\n現在の個数: {current}";
        }

        private static void HideTooltip()
        {
            TooltipSystem tooltipSystem = TooltipSystem.FindInstance();
            if (tooltipSystem != null)
            {
                tooltipSystem.Hide();
            }
        }

        private static string GetDisplayName(MaterialManager.MaterialType type)
        {
            switch (type)
            {
                case MaterialManager.MaterialType.Scrap: return "Scrap";
                case MaterialManager.MaterialType.Gear: return "Gear";
                case MaterialManager.MaterialType.UpgradeCore: return "Upgrade Core";
                case MaterialManager.MaterialType.ModuleCore_lv1: return "Module Core Lv1";
                case MaterialManager.MaterialType.ModuleCore_lv2: return "Module Core Lv2";
                case MaterialManager.MaterialType.ModuleCore_lv3: return "Module Core Lv3";
                default: return type.ToString();
            }
        }

        private static string GetDropMethod(MaterialManager.MaterialType type)
        {
            switch (type)
            {
                case MaterialManager.MaterialType.Scrap:
                    return "敵撃破時にドロップ";
                case MaterialManager.MaterialType.Gear:
                    return "敵撃破時、またはステージ報酬で入手";
                case MaterialManager.MaterialType.UpgradeCore:
                    return "強敵やボス撃破時にドロップ";
                case MaterialManager.MaterialType.ModuleCore_lv1:
                    return "序盤エリアの敵やボス撃破時にドロップ";
                case MaterialManager.MaterialType.ModuleCore_lv2:
                    return "中盤エリアの敵やボス撃破時にドロップ";
                case MaterialManager.MaterialType.ModuleCore_lv3:
                    return "終盤エリアの敵やボス撃破時にドロップ";
                default:
                    return "探索中に入手";
            }
        }
    }
}
