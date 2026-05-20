using UnityEngine;
using UnityEngine.EventSystems;

public class UpgradeShopItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private UpgradePartSO part;
    private UpgradeShopManager shopManager;
    private UpgradeGridUI gridUI;
    private bool dragging;

    public void Initialize(UpgradePartSO sourcePart, UpgradeShopManager sourceShopManager, UpgradeGridUI targetGridUI)
    {
        part = sourcePart;
        shopManager = sourceShopManager;
        gridUI = targetGridUI;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        TooltipSystem tooltipSystem = TooltipSystem.FindInstance();
        if (tooltipSystem != null)
        {
            tooltipSystem.Hide();
        }

        dragging = gridUI != null && gridUI.BeginShopDrag(part, shopManager, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || gridUI == null)
        {
            return;
        }

        gridUI.UpdateShopDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging || gridUI == null)
        {
            return;
        }

        gridUI.EndShopDrag(eventData);
        dragging = false;
    }
}
