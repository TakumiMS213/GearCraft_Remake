using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeGridUI : MonoBehaviour
{
    [Header("Grid UI")]
    public RectTransform gridParent;
    public GameObject cellPrefab;
    public float cellSize = 60f;
    public float maxCellSize = 150f;
    public float minCellSize = 64f;
    public float cellSpacing = 4f;

    [Header("Part List")]
    public RectTransform partListParent;
    public GameObject partSlotPrefab;

    [Header("Operation")]
    public TMP_Text infoText;
    public MaterialDisplay materialDisplay;

    private readonly List<GameObject> cellObjects = new List<GameObject>();
    private readonly List<GameObject> partSlotObjects = new List<GameObject>();
    private readonly List<GameObject> dragPreviewCells = new List<GameObject>();
    private UpgradePartSO selectedPart;
    private int selectedInventoryIndex = -1;
    private int selectedRotation;
    private int currentGridSize;
    private float currentCellSize;
    private int[,] currentGrid;
    private GameObject dragPreviewRoot;
    private RectTransform dragPreviewRect;
    private Canvas rootCanvas;
    private UpgradePartSO draggingPart;
    private UpgradeShopManager draggingShopManager;
    private int draggingRotation;
    private Vector2 lastDragScreenPosition;

    public void RefreshDisplay(
        int[,] grid,
        int gridSize,
        List<UpgradeGridManager.PlacedPart> placedParts,
        List<UpgradePartSO> ownedParts = null)
    {
        RefreshGrid(grid, gridSize, placedParts);
        RefreshPartList(ownedParts);
    }

    private void Update()
    {
        if (draggingPart == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            draggingRotation = (draggingRotation + 1) % 4;
            RebuildDragPreview();
            UpdateDragPreview(lastDragScreenPosition);
        }
    }

    public void SelectPart(UpgradePartSO part)
    {
        SelectPart(part, -1);
    }

    public void SelectPart(UpgradePartSO part, int inventoryIndex)
    {
        selectedPart = part;
        selectedInventoryIndex = inventoryIndex;
        selectedRotation = 0;

        if (infoText != null && part != null)
        {
            infoText.text = $"{part.partName}を選択中。グリッドをクリックして配置してください。";
        }
    }

    public void RotateSelected()
    {
        selectedRotation = (selectedRotation + 1) % 4;
        if (selectedPart != null && infoText != null)
        {
            infoText.text = $"{selectedPart.partName} 回転: {selectedRotation * 90}度";
        }
    }

    private void RefreshGrid(int[,] grid, int gridSize, List<UpgradeGridManager.PlacedPart> placedParts)
    {
        ClearObjects(cellObjects);
        ClearGeneratedChildren(gridParent, "GridCell");
        currentGrid = grid;
        currentGridSize = gridSize;

        if (grid == null || gridParent == null || cellPrefab == null)
        {
            return;
        }

        float resolvedCellSize = ResolveCellSize(gridSize);
        currentCellSize = resolvedCellSize;
        float totalSize = gridSize * (resolvedCellSize + cellSpacing) - cellSpacing;
        float startX = -totalSize / 2f + resolvedCellSize / 2f;
        float startY = totalSize / 2f - resolvedCellSize / 2f;

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                GameObject cell = Instantiate(cellPrefab, gridParent);
                RectTransform rectTransform = cell.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(resolvedCellSize, resolvedCellSize);
                    rectTransform.anchoredPosition = new Vector2(
                        startX + x * (resolvedCellSize + cellSpacing),
                        startY - y * (resolvedCellSize + cellSpacing));
                }

                UpgradePartSO cellPart = ResolvePart(grid[y, x], placedParts);
                ApplyCellVisual(cell, cellPart);
                ApplyTooltip(cell, cellPart);

                int gx = x;
                int gy = y;
                Button button = cell.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => OnCellClicked(gx, gy, grid));
                }

                cellObjects.Add(cell);
            }
        }
    }

    private float ResolveCellSize(int gridSize)
    {
        if (gridParent == null || gridSize <= 0)
        {
            return cellSize;
        }

        Rect rect = gridParent.rect;
        float availableSize = Mathf.Min(rect.width, rect.height);
        if (availableSize <= 0f)
        {
            return cellSize;
        }

        float spacingTotal = cellSpacing * (gridSize - 1);
        float fittedSize = (availableSize - spacingTotal) / gridSize;
        return Mathf.Clamp(fittedSize, minCellSize, maxCellSize);
    }

    private void RefreshPartList(List<UpgradePartSO> ownedParts)
    {
        ClearObjects(partSlotObjects);
        ClearGeneratedChildren(partListParent, null);

        if (ownedParts == null || partListParent == null || partSlotPrefab == null)
        {
            return;
        }

        for (int i = 0; i < ownedParts.Count; i++)
        {
            UpgradePartSO part = ownedParts[i];
            if (part == null)
            {
                continue;
            }

            GameObject slot = Instantiate(partSlotPrefab, partListParent);
            int inventoryIndex = i;
            bool isPlaced = UpgradeGridManager.Instance != null &&
                UpgradeGridManager.Instance.IsInventoryPartPlaced(inventoryIndex);

            ApplyPartSlot(slot, part, isPlaced);
            Button button = slot.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = !isPlaced;
                button.onClick.AddListener(() => SelectPart(part, inventoryIndex));
            }

            partSlotObjects.Add(slot);
        }
    }

    private void OnCellClicked(int x, int y, int[,] grid)
    {
        if (draggingPart != null)
        {
            return;
        }

        if (selectedPart != null && UpgradeGridManager.Instance != null)
        {
            bool placed = UpgradeGridManager.Instance.TryPlace(selectedPart, x, y, selectedRotation, selectedInventoryIndex);
            if (infoText != null)
            {
                infoText.text = placed ? $"{selectedPart.partName}を配置しました。" : "ここには配置できません。";
            }

            if (placed)
            {
                selectedPart = null;
                selectedInventoryIndex = -1;
                selectedRotation = 0;
            }

            return;
        }

        if (grid != null && grid[y, x] >= 0)
        {
            UpgradeGridManager.Instance?.RemovePart(grid[y, x]);
            RefreshMaterialDisplay();

            if (infoText != null)
            {
                infoText.text = "パーツを外しました。素材を返還しました。";
            }
        }
    }

    public bool BeginShopDrag(UpgradePartSO part, UpgradeShopManager shopManager, PointerEventData eventData)
    {
        if (part == null)
        {
            return false;
        }

        if (shopManager != null && !shopManager.CanAfford(part))
        {
            shopManager.ShowCannotAfford(part);
            return false;
        }

        draggingPart = part;
        draggingShopManager = shopManager;
        draggingRotation = 0;
        selectedPart = null;
        selectedInventoryIndex = -1;
        selectedRotation = 0;
        EnsureRootCanvas();
        EnsureDragPreviewRoot();
        RebuildDragPreview();
        UpdateDragPreview(eventData.position);

        if (infoText != null)
        {
            infoText.text = $"{part.partName}: グリッドへドラッグ。右クリックで回転。";
        }

        return true;
    }

    public void UpdateShopDrag(PointerEventData eventData)
    {
        UpdateDragPreview(eventData.position);
    }

    public void EndShopDrag(PointerEventData eventData)
    {
        if (draggingPart == null)
        {
            ClearDragPreview();
            return;
        }

        int gridX;
        int gridY;
        bool hasCell = TryGetGridPosition(eventData.position, out gridX, out gridY);
        bool placed = hasCell &&
            UpgradeGridManager.Instance != null &&
            UpgradeGridManager.Instance.TryPurchaseAndPlace(draggingPart, gridX, gridY, draggingRotation);

        if (placed)
        {
            if (draggingShopManager != null)
            {
                draggingShopManager.OnPartPlaced(draggingPart);
            }

            RefreshMaterialDisplay();
        }
        else if (draggingShopManager != null)
        {
            draggingShopManager.OnPartPlacementFailed(draggingPart);
        }

        draggingPart = null;
        draggingShopManager = null;
        draggingRotation = 0;
        ClearDragPreview();
    }

    private void EnsureRootCanvas()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }
    }

    private void EnsureDragPreviewRoot()
    {
        if (dragPreviewRoot != null)
        {
            dragPreviewRoot.SetActive(true);
            return;
        }

        Transform parent = rootCanvas != null ? rootCanvas.transform : transform;
        dragPreviewRoot = new GameObject("UpgradePartDragPreview", typeof(RectTransform), typeof(CanvasGroup));
        dragPreviewRoot.transform.SetParent(parent, false);
        dragPreviewRect = dragPreviewRoot.GetComponent<RectTransform>();
        dragPreviewRect.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup group = dragPreviewRoot.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0.88f;
    }

    private void RebuildDragPreview()
    {
        ClearObjects(dragPreviewCells);
        if (draggingPart == null || dragPreviewRect == null)
        {
            return;
        }

        bool[,] shape = draggingPart.GetRotatedShape(draggingRotation);
        int height = shape.GetLength(0);
        int width = shape.GetLength(1);
        float size = currentCellSize > 0f ? currentCellSize : cellSize;
        float totalWidth = width * (size + cellSpacing) - cellSpacing;
        float totalHeight = height * (size + cellSpacing) - cellSpacing;
        dragPreviewRect.sizeDelta = new Vector2(totalWidth, totalHeight);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!shape[y, x])
                {
                    continue;
                }

                GameObject cell = new GameObject("PreviewCell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cell.transform.SetParent(dragPreviewRect, false);
                RectTransform rect = cell.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = new Vector2(
                    -totalWidth / 2f + size / 2f + x * (size + cellSpacing),
                    totalHeight / 2f - size / 2f - y * (size + cellSpacing));

                Image image = cell.GetComponent<Image>();
                image.color = new Color(draggingPart.partColor.r, draggingPart.partColor.g, draggingPart.partColor.b, 0.82f);
                image.raycastTarget = false;
                dragPreviewCells.Add(cell);
            }
        }
    }

    private void UpdateDragPreview(Vector2 screenPosition)
    {
        lastDragScreenPosition = screenPosition;
        if (dragPreviewRect == null)
        {
            return;
        }

        RectTransform canvasRect = rootCanvas != null
            ? rootCanvas.transform as RectTransform
            : transform as RectTransform;
        Camera camera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        Vector2 localPoint;
        if (canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out localPoint))
        {
            dragPreviewRect.anchoredPosition = localPoint;
        }

        ApplyDragPreviewValidity(TryGetGridPosition(screenPosition, out int gridX, out int gridY) &&
            UpgradeGridManager.Instance != null &&
            UpgradeGridManager.Instance.CanPlace(draggingPart, gridX, gridY, draggingRotation));
    }

    private void ApplyDragPreviewValidity(bool canPlace)
    {
        for (int i = 0; i < dragPreviewCells.Count; i++)
        {
            Image image = dragPreviewCells[i] != null ? dragPreviewCells[i].GetComponent<Image>() : null;
            if (image == null)
            {
                continue;
            }

            Color color = draggingPart != null ? draggingPart.partColor : Color.white;
            if (!canPlace)
            {
                color = new Color(1f, 0.25f, 0.25f, 1f);
            }

            color.a = 0.82f;
            image.color = color;
        }
    }

    private bool TryGetGridPosition(Vector2 screenPosition, out int gridX, out int gridY)
    {
        gridX = -1;
        gridY = -1;
        if (gridParent == null || currentGrid == null || currentGridSize <= 0)
        {
            return false;
        }

        Camera camera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridParent, screenPosition, camera, out localPoint))
        {
            return false;
        }

        float size = currentCellSize > 0f ? currentCellSize : ResolveCellSize(currentGridSize);
        float totalSize = currentGridSize * (size + cellSpacing) - cellSpacing;
        float localX = localPoint.x + totalSize / 2f;
        float localY = totalSize / 2f - localPoint.y;
        gridX = Mathf.FloorToInt(localX / (size + cellSpacing));
        gridY = Mathf.FloorToInt(localY / (size + cellSpacing));
        return gridX >= 0 && gridX < currentGridSize && gridY >= 0 && gridY < currentGridSize;
    }

    private void ClearDragPreview()
    {
        ClearObjects(dragPreviewCells);
        if (dragPreviewRoot != null)
        {
            dragPreviewRoot.SetActive(false);
        }
    }

    private void RefreshMaterialDisplay()
    {
        if (materialDisplay != null)
        {
            materialDisplay.UpdateMaterialAmount();
        }
    }

    private static UpgradePartSO ResolvePart(int cellValue, List<UpgradeGridManager.PlacedPart> placedParts)
    {
        if (placedParts == null || cellValue < 0 || cellValue >= placedParts.Count)
        {
            return null;
        }

        UpgradeGridManager.PlacedPart placedPart = placedParts[cellValue];
        return placedPart != null ? placedPart.partData : null;
    }

    private static void ApplyCellVisual(GameObject cell, UpgradePartSO part)
    {
        Image image = cell != null ? cell.GetComponent<Image>() : null;
        if (image == null)
        {
            return;
        }

        image.color = part != null ? part.partColor : new Color(1f, 1f, 1f, 0.8f);
    }

    private static void ApplyPartSlot(GameObject slot, UpgradePartSO part, bool isPlaced)
    {
        ApplyTooltip(slot, part);

        Image image = slot.GetComponent<Image>();
        if (image != null)
        {
            image.color = isPlaced ? new Color(0.25f, 0.25f, 0.25f, 0.7f) : part.partColor;
        }

        Transform iconTransform = slot.transform.Find("Icon");
        if (iconTransform != null)
        {
            Image icon = iconTransform.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = part.icon;
                icon.enabled = part.icon != null;
            }
        }

        TMP_Text[] texts = slot.GetComponentsInChildren<TMP_Text>();
        if (texts.Length > 0)
        {
            texts[0].text = isPlaced ? $"{part.partName}（装備中）" : part.partName;
        }
    }

    private static void ApplyTooltip(GameObject target, UpgradePartSO part)
    {
        if (target == null)
        {
            return;
        }

        TooltipTrigger trigger = target.GetComponent<TooltipTrigger>();
        if (part == null)
        {
            if (trigger != null)
            {
                trigger.tooltipText = string.Empty;
            }

            return;
        }

        if (trigger == null)
        {
            trigger = target.AddComponent<TooltipTrigger>();
        }

        trigger.tooltipText = part.BuildTooltipText();
    }

    private static void ClearObjects(List<GameObject> objects)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] != null)
            {
                DestroyObject(objects[i]);
            }
        }

        objects.Clear();
    }

    private static void DestroyObject(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static void ClearGeneratedChildren(RectTransform parent, string namePrefix)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (!string.IsNullOrEmpty(namePrefix) && !child.name.StartsWith(namePrefix))
            {
                continue;
            }

            DestroyObject(child);
        }
    }
}
