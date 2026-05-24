using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 各UIアイテムに付与。PointerEnter/Exitでツールチップを表示/非表示する。
/// </summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UpgradePartSO part;

    [TextArea]
    public string tooltipText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        TooltipSystem tooltipSystem = TooltipSystem.FindInstance();
        if (tooltipSystem == null)
        {
            return;
        }

        if (part != null)
        {
            tooltipSystem.ShowPart(part, transform as RectTransform);
        }
        else if (!string.IsNullOrEmpty(tooltipText))
        {
            tooltipSystem.Show(tooltipText, transform as RectTransform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem tooltipSystem = TooltipSystem.FindInstance();
        if (tooltipSystem != null)
        {
            tooltipSystem.Hide();
        }
    }

    private void OnDisable()
    {
        TooltipSystem tooltipSystem = TooltipSystem.FindInstance();
        if (tooltipSystem != null)
            tooltipSystem.Hide();
    }
}
