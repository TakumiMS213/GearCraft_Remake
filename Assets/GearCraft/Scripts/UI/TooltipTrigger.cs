using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 各UIアイテムに付与。PointerEnter/Exitでツールチップを表示/非表示する。
/// </summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea]
    public string tooltipText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipSystem.Instance != null && !string.IsNullOrEmpty(tooltipText))
        {
            TooltipSystem.Instance.Show(tooltipText);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipSystem.Instance != null)
        {
            TooltipSystem.Instance.Hide();
        }
    }

    private void OnDisable()
    {
        if (TooltipSystem.Instance != null)
            TooltipSystem.Instance.Hide();
    }
}
