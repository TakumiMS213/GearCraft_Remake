using UnityEngine;
using UnityEngine.EventSystems;

namespace GearCraft.Scripts.Craft
{
    public sealed class CraftResultTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CraftResultDescriptionSO resultDescription;

        public void Configure(CraftResultDescriptionSO descriptionData)
        {
            resultDescription = descriptionData;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (resultDescription == null)
            {
                return;
            }

            TooltipSystem tooltipSystem = TooltipSystem.FindInstance();
            if (tooltipSystem == null)
            {
                return;
            }

            tooltipSystem.Show(resultDescription.BuildTooltipText(), transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip();
        }

        private void OnDisable()
        {
            HideTooltip();
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
