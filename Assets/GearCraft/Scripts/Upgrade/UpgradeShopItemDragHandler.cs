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
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
        TooltipSystem tooltipSystem = TooltipSystem.FindInstance();
        if (tooltipSystem != null)
        {
            tooltipSystem.Hide();
        }

        dragging = gridUI != null && gridUI.BeginShopDrag(part, shopManager, eventData);
        if (dragging)
        {
            FindFirstObjectByType<GearCraft.Scripts.Craft.CraftTutorialController>()?.BeginPractice();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
        if (!dragging || gridUI == null)
        {
            return;
        }

        gridUI.UpdateShopDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
        if (!dragging || gridUI == null)
        {
            gridUI?.CancelShopDrag();
            return;
        }

        gridUI.EndShopDrag(eventData);
        dragging = false;
    }

    private void OnDisable()
    {
        if (dragging && gridUI != null)
        {
            gridUI.CancelShopDrag();
        }

        dragging = false;
    }
}
