using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    private readonly List<GameObject> cellObjects = new List<GameObject>();
    private readonly List<GameObject> partSlotObjects = new List<GameObject>();
    private UpgradePartSO selectedPart;
    private int selectedInventoryIndex = -1;
    private int selectedRotation;

    public void RefreshDisplay(
        int[,] grid,
        int gridSize,
        List<UpgradeGridManager.PlacedPart> placedParts,
        List<UpgradePartSO> ownedParts = null)
    {
        RefreshGrid(grid, gridSize, placedParts);
        RefreshPartList(ownedParts);
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
            infoText.text = $"{part.partName} selected. Click the grid to place it.";
        }
    }

    public void RotateSelected()
    {
        selectedRotation = (selectedRotation + 1) % 4;
        if (selectedPart != null && infoText != null)
        {
            infoText.text = $"{selectedPart.partName} rotation: {selectedRotation * 90} deg";
        }
    }

    private void RefreshGrid(int[,] grid, int gridSize, List<UpgradeGridManager.PlacedPart> placedParts)
    {
        ClearObjects(cellObjects);

        if (grid == null || gridParent == null || cellPrefab == null)
        {
            return;
        }

        float resolvedCellSize = ResolveCellSize(gridSize);
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
        if (selectedPart != null && UpgradeGridManager.Instance != null)
        {
            bool placed = UpgradeGridManager.Instance.TryPlace(selectedPart, x, y, selectedRotation, selectedInventoryIndex);
            if (infoText != null)
            {
                infoText.text = placed ? $"{selectedPart.partName} placed." : "Cannot place part here.";
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
            if (infoText != null)
            {
                infoText.text = "Part removed.";
            }
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

        image.color = part != null ? part.partColor : new Color(0.2f, 0.2f, 0.25f, 0.8f);
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
            texts[0].text = isPlaced ? $"{part.partName} (set)" : part.partName;
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
                Destroy(objects[i]);
            }
        }

        objects.Clear();
    }
}
