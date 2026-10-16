using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BonusCardTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private BonusCardDataSO cardData;
    private TooltipSystem tooltip;
    private RectTransform anchor;
    private Button button;

    public void Configure(BonusCardDataSO data, TooltipSystem window)
    {
        cardData = data;
        tooltip = window;
        anchor = transform as RectTransform;
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button == null || !button.IsInteractable()) return;
        button.Select();
        ShowDescription();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != gameObject)
            HideOwnDescription();
    }

    public void OnSelect(BaseEventData eventData) => ShowDescription();
    public void OnDeselect(BaseEventData eventData) => HideOwnDescription();
    private void OnDisable() => HideOwnDescription();

    private void ShowDescription()
    {
        if (cardData == null || tooltip == null || button == null || !button.IsInteractable()) return;
        string text = cardData.BuildTooltipText();
        tooltip.Show(text, anchor);
        if (tooltip.tooltipPanel != null && tooltip.tooltipText != null)
        {
            float width = tooltip.tooltipPanel.rect.width - 36f;
            float height = tooltip.tooltipText.GetPreferredValues(text, width, Mathf.Infinity).y + 36f;
            tooltip.tooltipPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(120f, height));
        }
    }

    private void HideOwnDescription()
    {
        if (tooltip != null && tooltip.IsShowingFor(anchor)) tooltip.Hide();
    }
}
