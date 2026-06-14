using System.Collections.Generic;
using UnityEngine;

public class UpgradeGridManager : MonoBehaviour
{
    public static UpgradeGridManager Instance { get; private set; }

    [System.Serializable]
    public class PlacedPart
    {
        public UpgradePartSO partData;
        public int gridX;
        public int gridY;
        public int rotation;
        public int inventoryIndex = -1;
        public bool refundCostsOnRemove;
        public bool returnToShopOnSameDay;
        public int placedShopDay = -1;
    }

    [Header("UI")]
    public UpgradeGridUI gridUI;
    public UpgradeEffectDisplay effectDisplay;

    private int[,] grid;
    private int gridSize;
    private readonly List<PlacedPart> placedParts = new List<PlacedPart>();

    public int GridSize => gridSize;
    public List<PlacedPart> PlacedParts => placedParts;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        RestorePlacedParts();
        RefreshGridSize();
    }

    public void RefreshGridSize()
    {
        int newSize = StatusManager.Instance != null ? StatusManager.Instance.GetUpgradeGridSize() : 3;
        if (grid != null && gridSize == newSize)
        {
            RefreshUI();
            return;
        }

        int oldSize = gridSize;
        gridSize = newSize;
        MigrateGrid(oldSize, newSize);
    }

    public bool CanPlace(UpgradePartSO part, int posX, int posY, int rotation)
    {
        return CanPlace(part, posX, posY, rotation, grid, gridSize);
    }

    public bool TryPlace(UpgradePartSO part, int posX, int posY, int rotation)
    {
        return TryPlace(part, posX, posY, rotation, -1);
    }

    public bool TryPlace(UpgradePartSO part, int posX, int posY, int rotation, int inventoryIndex)
    {
        return TryPlaceInternal(part, posX, posY, rotation, inventoryIndex, false);
    }

    public bool TryPurchaseAndPlace(UpgradePartSO part, int posX, int posY, int rotation)
    {
        return TryPurchaseAndPlace(part, posX, posY, rotation, false, -1);
    }

    public bool TryPurchaseAndPlace(UpgradePartSO part, int posX, int posY, int rotation, bool returnToShopOnSameDay, int placedShopDay)
    {
        if (part == null || MaterialManager.Instance == null)
        {
            return false;
        }

        if (!CanPlace(part, posX, posY, rotation))
        {
            return false;
        }

        if (!MaterialManager.Instance.CanAfford(part.costs))
        {
            return false;
        }

        MaterialManager.Instance.SpendCosts(part.costs);
        bool placed = TryPlaceInternal(part, posX, posY, rotation, -1, true, returnToShopOnSameDay, placedShopDay);
        if (placed)
        {
            RefreshMaterialDisplays();
        }

        return placed;
    }

    private bool TryPlaceInternal(UpgradePartSO part, int posX, int posY, int rotation, int inventoryIndex, bool refundCostsOnRemove)
    {
        return TryPlaceInternal(part, posX, posY, rotation, inventoryIndex, refundCostsOnRemove, false, -1);
    }

    private bool TryPlaceInternal(
        UpgradePartSO part,
        int posX,
        int posY,
        int rotation,
        int inventoryIndex,
        bool refundCostsOnRemove,
        bool returnToShopOnSameDay,
        int placedShopDay)
    {
        if (!CanPlace(part, posX, posY, rotation))
        {
            return false;
        }

        if (inventoryIndex >= 0 && IsInventoryPartPlaced(inventoryIndex))
        {
            return false;
        }

        int partIndex = placedParts.Count;
        PlaceInGrid(part, posX, posY, rotation, grid, gridSize, partIndex);
        placedParts.Add(new PlacedPart
        {
            partData = part,
            gridX = posX,
            gridY = posY,
            rotation = rotation,
            inventoryIndex = inventoryIndex,
            refundCostsOnRemove = refundCostsOnRemove,
            returnToShopOnSameDay = returnToShopOnSameDay,
            placedShopDay = placedShopDay
        });

        RecalculateEffects();
        RefreshUI();
        SavePlacedParts();
        return true;
    }

    public void RemovePart(int partIndex)
    {
        if (partIndex < 0 || partIndex >= placedParts.Count)
        {
            return;
        }

        PlacedPart removedPart = placedParts[partIndex];

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                if (grid[y, x] == partIndex)
                {
                    grid[y, x] = -1;
                }
            }
        }

        RefundPartCosts(removedPart);
        ReturnPartToShopIfNeeded(removedPart);
        placedParts.RemoveAt(partIndex);
        RebuildGridIndices();
        RecalculateEffects();
        RefreshUI();
        RefreshMaterialDisplays();
        SavePlacedParts();
    }

    private static void ReturnPartToShopIfNeeded(PlacedPart part)
    {
        if (part == null || !part.returnToShopOnSameDay || part.partData == null || StatusManager.Instance == null)
        {
            return;
        }

        if (part.placedShopDay != StatusManager.Instance.upgradeShopDay)
        {
            return;
        }

        UpgradeShopManager shopManager = FindFirstObjectByType<UpgradeShopManager>();
        if (shopManager != null)
        {
            shopManager.RestoreLineupPart(part.partData);
        }
    }

    private static void RefundPartCosts(PlacedPart part)
    {
        if (part == null || !part.refundCostsOnRemove || part.partData == null || part.partData.costs == null)
        {
            return;
        }

        if (MaterialManager.Instance == null)
        {
            return;
        }

        for (int i = 0; i < part.partData.costs.Length; i++)
        {
            CraftCost cost = part.partData.costs[i];
            if (cost == null)
            {
                continue;
            }

            MaterialManager.Instance.AddMaterial(cost.type, cost.amount);
        }
    }

    private static void RefreshMaterialDisplays()
    {
        MaterialDisplay[] displays = FindObjectsByType<MaterialDisplay>(FindObjectsSortMode.None);
        for (int i = 0; i < displays.Length; i++)
        {
            if (displays[i] != null)
            {
                displays[i].UpdateMaterialAmount();
            }
        }
    }

    public void ClearAllParts()
    {
        placedParts.Clear();

        if (grid != null)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    grid[y, x] = -1;
                }
            }
        }

        RecalculateEffects();
        RefreshUI();
        SavePlacedParts();
    }

    public bool IsInventoryPartPlaced(int inventoryIndex)
    {
        if (inventoryIndex < 0)
        {
            return false;
        }

        for (int i = 0; i < placedParts.Count; i++)
        {
            if (placedParts[i] != null && placedParts[i].inventoryIndex == inventoryIndex)
            {
                return true;
            }
        }

        return false;
    }

    public void RefreshUI()
    {
        if (gridUI == null)
        {
            return;
        }

        List<UpgradePartSO> ownedParts = StatusManager.Instance != null
            ? StatusManager.Instance.ownedUpgradeParts
            : null;

        gridUI.RefreshDisplay(grid, gridSize, placedParts, ownedParts);
    }

    public void RecalculateEffects()
    {
        if (StatusManager.Instance == null)
        {
            return;
        }

        StatusManager status = StatusManager.Instance;
        status.bonusDamage = 0f;
        status.attackSpeedMult = 1f;
        status.hasBulletDouble = false;
        status.spreadModifier = 0f;
        status.durabilityDrainChance = 0f;
        status.ricochetCount = 0;
        status.junkCollectorMult = 1f;
        status.bulletSizeMult = 1f;
        status.maxDurabilityBonus = 0;

        float magnetBonus = 0f;
        int maxDurabilityBonus = 0;

        for (int i = 0; i < placedParts.Count; i++)
        {
            UpgradePartSO part = placedParts[i]?.partData;
            if (part == null || part.effects == null)
            {
                continue;
            }

            for (int j = 0; j < part.effects.Length; j++)
            {
                UpgradeEffect effect = part.effects[j];
                if (effect == null)
                {
                    continue;
                }

                ApplyEffect(status, effect, ref magnetBonus, ref maxDurabilityBonus);
            }
        }

        status.magnetRange = 3f + magnetBonus;
        status.maxDurabilityBonus = maxDurabilityBonus;

        if (effectDisplay != null)
        {
            effectDisplay.Refresh();
        }
    }

    private void MigrateGrid(int oldSize, int newSize)
    {
        int[,] newGrid = CreateEmptyGrid(newSize);
        List<PlacedPart> validParts = new List<PlacedPart>();

        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];
            if (part == null || part.partData == null)
            {
                continue;
            }

            if (CanPlace(part.partData, part.gridX, part.gridY, part.rotation, newGrid, newSize))
            {
                PlaceInGrid(part.partData, part.gridX, part.gridY, part.rotation, newGrid, newSize, validParts.Count);
                validParts.Add(part);
            }
        }

        placedParts.Clear();
        placedParts.AddRange(validParts);
        grid = newGrid;
        RecalculateEffects();
        RefreshUI();
        SavePlacedParts();
    }

    private void RestorePlacedParts()
    {
        placedParts.Clear();
        if (StatusManager.Instance == null || StatusManager.Instance.savedUpgradePartPlacements == null)
        {
            return;
        }

        List<StatusManager.SavedUpgradePartPlacement> savedParts = StatusManager.Instance.savedUpgradePartPlacements;
        for (int i = 0; i < savedParts.Count; i++)
        {
            StatusManager.SavedUpgradePartPlacement saved = savedParts[i];
            if (saved == null || saved.partData == null)
            {
                continue;
            }

            placedParts.Add(new PlacedPart
            {
                partData = saved.partData,
                gridX = saved.gridX,
                gridY = saved.gridY,
                rotation = saved.rotation,
                inventoryIndex = saved.inventoryIndex,
                refundCostsOnRemove = saved.refundCostsOnRemove,
                returnToShopOnSameDay = saved.returnToShopOnSameDay,
                placedShopDay = saved.placedShopDay
            });
        }
    }

    private void SavePlacedParts()
    {
        if (StatusManager.Instance != null)
        {
            StatusManager.Instance.SaveUpgradeGridState(placedParts);
        }
    }

    private bool CanPlace(UpgradePartSO part, int posX, int posY, int rotation, int[,] targetGrid, int targetSize)
    {
        if (part == null || targetGrid == null)
        {
            return false;
        }

        bool[,] shape = part.GetRotatedShape(rotation);
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

                int gridX = posX + x;
                int gridY = posY + y;
                if (gridX < 0 || gridX >= targetSize || gridY < 0 || gridY >= targetSize)
                {
                    return false;
                }

                if (targetGrid[gridY, gridX] != -1)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void RebuildGridIndices()
    {
        grid = CreateEmptyGrid(gridSize);
        for (int i = 0; i < placedParts.Count; i++)
        {
            PlacedPart part = placedParts[i];
            if (part != null && part.partData != null)
            {
                PlaceInGrid(part.partData, part.gridX, part.gridY, part.rotation, grid, gridSize, i);
            }
        }
    }

    private static int[,] CreateEmptyGrid(int size)
    {
        int[,] result = new int[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                result[y, x] = -1;
            }
        }

        return result;
    }

    private static void PlaceInGrid(UpgradePartSO part, int posX, int posY, int rotation, int[,] targetGrid, int targetSize, int index)
    {
        bool[,] shape = part.GetRotatedShape(rotation);
        int height = shape.GetLength(0);
        int width = shape.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (shape[y, x])
                {
                    targetGrid[posY + y, posX + x] = index;
                }
            }
        }
    }

    private static void ApplyEffect(StatusManager status, UpgradeEffect effect, ref float magnetBonus, ref int maxDurabilityBonus)
    {
        switch (effect.type)
        {
            case UpgradeEffectType.DamageFlat:
                status.bonusDamage += effect.value;
                break;
            case UpgradeEffectType.AttackSpeedMult:
                status.attackSpeedMult *= effect.value;
                break;
            case UpgradeEffectType.BulletDouble:
                status.hasBulletDouble = true;
                break;
            case UpgradeEffectType.SpreadReduction:
                status.spreadModifier -= effect.value;
                break;
            case UpgradeEffectType.SpreadIncrease:
                status.spreadModifier += effect.value;
                break;
            case UpgradeEffectType.DurabilityDrain:
                status.durabilityDrainChance += effect.value;
                break;
            case UpgradeEffectType.Ricochet:
                status.ricochetCount += (int)effect.value;
                break;
            case UpgradeEffectType.JunkCollector:
                status.junkCollectorMult += effect.value;
                break;
            case UpgradeEffectType.BulletSizeUp:
                status.bulletSizeMult += effect.value;
                break;
            case UpgradeEffectType.MaxDurabilityUp:
                maxDurabilityBonus += (int)effect.value;
                break;
            case UpgradeEffectType.MagnetRangeUp:
                magnetBonus += effect.value;
                break;
        }
    }
}
