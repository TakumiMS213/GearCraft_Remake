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
    public GameObject placementEffectPrefab;
    public float placementEffectLifetime = 3f;
    public int placementEffectSortingOrder = 5000;
    public Sprite ineffectiveWarningSprite;
    public float ineffectiveWarningIconSize = 38f;
    public string ineffectiveWarningText = "現在装備中の武器に効果がありません";

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

    private struct ShapeBounds
    {
        public int minX;
        public int minY;
        public int maxX;
        public int maxY;
        public int width;
        public int height;
        public bool hasCells;
    }

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

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.R))
        {
            draggingRotation = (draggingRotation + 1) % 4;
            RebuildDragPreview();
            UpdateDragPreview(lastDragScreenPosition);
        }
    }

    private void OnDisable()
    {
        CancelShopDrag();
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
                ApplyIneffectiveWarning(cell, cellPart, grid, x, y);

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
            infoText.text = $"{part.partName}: グリッドへドラッグ。右クリックまたはRキーで回転。";
        }

        return true;
    }

    public void UpdateShopDrag(PointerEventData eventData)
    {
        UpdateDragPreview(eventData.position);
    }

    public void CancelShopDrag()
    {
        draggingPart = null;
        draggingShopManager = null;
        draggingRotation = 0;
        ClearDragPreview();
    }

    public void EndShopDrag(PointerEventData eventData)
    {
        if (draggingPart == null)
        {
            ClearDragPreview();
            return;
        }

        UpgradePartSO partToPlace = draggingPart;
        UpgradeShopManager shopManager = draggingShopManager;
        int rotation = draggingRotation;
        int gridX;
        int gridY;
        bool hasCell = TryGetDragPlacementPosition(eventData.position, out gridX, out gridY);

        draggingPart = null;
        draggingShopManager = null;
        draggingRotation = 0;
        ClearDragPreview();

        int placedShopDay = shopManager != null ? shopManager.ShopDay : -1;
        bool placed = hasCell &&
            UpgradeGridManager.Instance != null &&
            UpgradeGridManager.Instance.TryPurchaseAndPlace(partToPlace, gridX, gridY, rotation, shopManager != null, placedShopDay);

        if (placed)
        {
            PlayPlacementEffect(eventData.position);

            if (shopManager != null)
            {
                shopManager.OnPartPlaced(partToPlace);
            }

            RefreshMaterialDisplay();
        }
        else if (shopManager != null)
        {
            shopManager.OnPartPlacementFailed(partToPlace);
        }
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
        ClearDragPreviewObjects();
        if (draggingPart == null || dragPreviewRect == null)
        {
            return;
        }

        bool[,] shape = draggingPart.GetRotatedShape(draggingRotation);
        ShapeBounds bounds = GetShapeBounds(shape);
        if (!bounds.hasCells)
        {
            return;
        }

        int height = shape.GetLength(0);
        int width = shape.GetLength(1);
        float size = currentCellSize > 0f ? currentCellSize : cellSize;
        float totalWidth = bounds.width * (size + cellSpacing) - cellSpacing;
        float totalHeight = bounds.height * (size + cellSpacing) - cellSpacing;
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
                    -totalWidth / 2f + size / 2f + (x - bounds.minX) * (size + cellSpacing),
                    totalHeight / 2f - size / 2f - (y - bounds.minY) * (size + cellSpacing));

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

        Vector2 localPoint = Vector2.zero;
        bool hasCanvasPoint = canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out localPoint);
        bool hasPlacement = TryGetDragPlacementPosition(screenPosition, out int gridX, out int gridY);
        Vector2 snappedPosition;

        if (hasPlacement && TryGetDragPreviewAnchoredPosition(gridX, gridY, out snappedPosition))
        {
            dragPreviewRect.anchoredPosition = snappedPosition;
        }
        else if (hasCanvasPoint)
        {
            dragPreviewRect.anchoredPosition = localPoint;
        }

        ApplyDragPreviewValidity(hasPlacement &&
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

    private bool TryGetDragPlacementPosition(Vector2 screenPosition, out int gridX, out int gridY)
    {
        gridX = -1;
        gridY = -1;
        if (draggingPart == null || gridParent == null || currentGrid == null || currentGridSize <= 0)
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

        bool[,] shape = draggingPart.GetRotatedShape(draggingRotation);
        ShapeBounds bounds = GetShapeBounds(shape);
        if (!bounds.hasCells)
        {
            return false;
        }

        float size = currentCellSize > 0f ? currentCellSize : ResolveCellSize(currentGridSize);
        float step = size + cellSpacing;
        float gridTotalSize = currentGridSize * step - cellSpacing;
        float shapeTotalWidth = bounds.width * step - cellSpacing;
        float shapeTotalHeight = bounds.height * step - cellSpacing;

        if (bounds.width > currentGridSize || bounds.height > currentGridSize)
        {
            return false;
        }

        float expandedHalfWidth = gridTotalSize / 2f + shapeTotalWidth / 2f;
        float expandedHalfHeight = gridTotalSize / 2f + shapeTotalHeight / 2f;
        if (localPoint.x < -expandedHalfWidth ||
            localPoint.x > expandedHalfWidth ||
            localPoint.y < -expandedHalfHeight ||
            localPoint.y > expandedHalfHeight)
        {
            return false;
        }

        float gridStartX = -gridTotalSize / 2f + size / 2f;
        float gridStartY = gridTotalSize / 2f - size / 2f;
        float previewTopLeftCenterX = localPoint.x - shapeTotalWidth / 2f + size / 2f;
        float previewTopLeftCenterY = localPoint.y + shapeTotalHeight / 2f - size / 2f;

        int activeGridX = Mathf.RoundToInt((previewTopLeftCenterX - gridStartX) / step);
        int activeGridY = Mathf.RoundToInt((gridStartY - previewTopLeftCenterY) / step);
        activeGridX = Mathf.Clamp(activeGridX, 0, currentGridSize - bounds.width);
        activeGridY = Mathf.Clamp(activeGridY, 0, currentGridSize - bounds.height);

        gridX = activeGridX - bounds.minX;
        gridY = activeGridY - bounds.minY;
        return true;
    }

    private bool TryGetDragPreviewAnchoredPosition(int gridX, int gridY, out Vector2 anchoredPosition)
    {
        anchoredPosition = Vector2.zero;
        if (draggingPart == null || gridParent == null || currentGridSize <= 0)
        {
            return false;
        }

        RectTransform canvasRect = rootCanvas != null
            ? rootCanvas.transform as RectTransform
            : transform as RectTransform;
        if (canvasRect == null)
        {
            return false;
        }

        bool[,] shape = draggingPart.GetRotatedShape(draggingRotation);
        ShapeBounds bounds = GetShapeBounds(shape);
        if (!bounds.hasCells)
        {
            return false;
        }

        float size = currentCellSize > 0f ? currentCellSize : ResolveCellSize(currentGridSize);
        float step = size + cellSpacing;
        float gridTotalSize = currentGridSize * step - cellSpacing;
        float gridStartX = -gridTotalSize / 2f + size / 2f;
        float gridStartY = gridTotalSize / 2f - size / 2f;
        float activeCenterX = gridStartX + (gridX + bounds.minX + (bounds.width - 1) * 0.5f) * step;
        float activeCenterY = gridStartY - (gridY + bounds.minY + (bounds.height - 1) * 0.5f) * step;

        Vector3 worldPosition = gridParent.TransformPoint(new Vector3(activeCenterX, activeCenterY, 0f));
        anchoredPosition = canvasRect.InverseTransformPoint(worldPosition);
        return true;
    }

    private static ShapeBounds GetShapeBounds(bool[,] shape)
    {
        ShapeBounds bounds = new ShapeBounds
        {
            minX = int.MaxValue,
            minY = int.MaxValue,
            maxX = int.MinValue,
            maxY = int.MinValue,
            hasCells = false
        };

        if (shape == null)
        {
            return bounds;
        }

        int height = shape.GetLength(0);
        int width = shape.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!shape[y, x])
                {
                    continue;
                }

                bounds.hasCells = true;
                bounds.minX = Mathf.Min(bounds.minX, x);
                bounds.minY = Mathf.Min(bounds.minY, y);
                bounds.maxX = Mathf.Max(bounds.maxX, x);
                bounds.maxY = Mathf.Max(bounds.maxY, y);
            }
        }

        if (bounds.hasCells)
        {
            bounds.width = bounds.maxX - bounds.minX + 1;
            bounds.height = bounds.maxY - bounds.minY + 1;
        }

        return bounds;
    }

    private void ClearDragPreview()
    {
        if (dragPreviewRoot != null)
        {
            dragPreviewRoot.SetActive(false);
        }

        ClearDragPreviewObjects();
        if (dragPreviewRect != null)
        {
            dragPreviewRect.sizeDelta = Vector2.zero;
        }
    }

    private void ClearDragPreviewObjects()
    {
        if (dragPreviewRect != null)
        {
            for (int i = dragPreviewRect.childCount - 1; i >= 0; i--)
            {
                GameObject child = dragPreviewRect.GetChild(i).gameObject;
                child.SetActive(false);
                DestroyObject(child);
            }
        }

        for (int i = 0; i < dragPreviewCells.Count; i++)
        {
            if (dragPreviewCells[i] != null)
            {
                dragPreviewCells[i].SetActive(false);
            }
        }

        dragPreviewCells.Clear();
    }

    private void RefreshMaterialDisplay()
    {
        if (materialDisplay != null)
        {
            materialDisplay.UpdateMaterialAmount();
        }
    }

    private void PlayPlacementEffect(Vector2 screenPosition)
    {
        if (placementEffectPrefab == null)
        {
            return;
        }

        Camera camera = rootCanvas != null && rootCanvas.worldCamera != null
            ? rootCanvas.worldCamera
            : Camera.main;
        if (camera == null)
        {
            return;
        }

        float distance = Mathf.Abs(camera.transform.position.z);
        Vector3 worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
        worldPosition.z = 0f;

        GameObject effect = Instantiate(placementEffectPrefab, worldPosition, Quaternion.identity);
        ParticleSystemRenderer[] renderers = effect.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingOrder = placementEffectSortingOrder;
        }

        if (placementEffectLifetime > 0f)
        {
            Destroy(effect, placementEffectLifetime);
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

    private void ApplyIneffectiveWarning(GameObject cell, UpgradePartSO part, int[,] grid, int x, int y)
    {
        if (cell == null || part == null || ineffectiveWarningSprite == null)
        {
            return;
        }

        int partIndex = grid != null ? grid[y, x] : -1;
        if (partIndex < 0 || !IsIneffectiveForCurrentWeapon(part) || !IsTopLeftCellOfPart(grid, partIndex, x, y))
        {
            return;
        }

        GameObject warning = new GameObject("IneffectiveWarningIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        warning.transform.SetParent(cell.transform, false);

        RectTransform rect = warning.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(ineffectiveWarningIconSize, ineffectiveWarningIconSize);

        Image image = warning.GetComponent<Image>();
        image.sprite = ineffectiveWarningSprite;
        image.preserveAspect = true;
        image.raycastTarget = true;

        TooltipTrigger trigger = warning.AddComponent<TooltipTrigger>();
        trigger.part = null;
        trigger.tooltipText = ineffectiveWarningText;
    }

    private static bool IsIneffectiveForCurrentWeapon(UpgradePartSO part)
    {
        if (part == null || !part.rangedOnly || StatusManager.Instance == null)
        {
            return false;
        }

        WeaponDataSO currentWeapon = StatusManager.Instance.currentWeapon;
        return currentWeapon != null && currentWeapon.weaponType != WeaponType.Ranged;
    }

    private static bool IsTopLeftCellOfPart(int[,] grid, int partIndex, int x, int y)
    {
        if (grid == null || partIndex < 0)
        {
            return false;
        }

        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        int topY = int.MaxValue;
        int leftX = int.MaxValue;

        for (int gy = 0; gy < height; gy++)
        {
            for (int gx = 0; gx < width; gx++)
            {
                if (grid[gy, gx] != partIndex)
                {
                    continue;
                }

                if (gy < topY || gy == topY && gx < leftX)
                {
                    topY = gy;
                    leftX = gx;
                }
            }
        }

        return x == leftX && y == topY;
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
                trigger.part = null;
                trigger.tooltipText = string.Empty;
            }

            return;
        }

        if (trigger == null)
        {
            trigger = target.AddComponent<TooltipTrigger>();
        }

        trigger.part = part;
        trigger.tooltipText = part.BuildTooltipText();
    }

    private static void ClearObjects(List<GameObject> objects)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] != null)
            {
                objects[i].SetActive(false);
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

            child.SetActive(false);
            DestroyObject(child);
        }
    }
}
