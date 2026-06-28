using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class UpgradeShopManager : MonoBehaviour
{
    public event Action<UpgradePartSO> PartPlaced;

    [Header("Parts")]
    public UpgradePartSO[] allParts;
    public int shopSlotCount = 3;
    public int rerollGearCost = 1;

    [Header("UI")]
    public RectTransform shopParent;
    public GameObject shopItemPrefab;
    public TMP_Text infoText;
    public UpgradeGridUI targetGridUI;
    public int shopContentBottomPadding = 96;

    [Header("Audio")]
    public AudioSource purchaseSound;
    public AudioSource errorSound;

    private readonly List<UpgradePartSO> currentLineup = new List<UpgradePartSO>();
    private readonly List<GameObject> shopItemObjects = new List<GameObject>();
    private bool hasGeneratedLineup;
    private int shopDay = -1;

    public int ShopDay => shopDay;

    public void GenerateLineup()
    {
        if (!hasGeneratedLineup)
        {
            StartShopDayIfNeeded();
            GenerateLineupInternal();
            return;
        }

        TryRerollLineup();
    }

    public void TryRerollLineup()
    {
        if (MaterialManager.Instance == null || !MaterialManager.Instance.UseMaterial(MaterialManager.MaterialType.Gear, rerollGearCost))
        {
            if (infoText != null)
            {
                infoText.text = $"ラインナップ更新にはGear x{rerollGearCost}が必要です。";
            }

            if (errorSound != null)
            {
                errorSound.Play();
            }

            return;
        }

        GenerateLineupInternal();
        RefreshMaterialDisplays();
        if (infoText != null)
        {
            infoText.text = $"Gear x{rerollGearCost}を消費してラインナップを更新しました。";
        }
    }

    private void GenerateLineupInternal()
    {
        currentLineup.Clear();
        if (allParts == null || allParts.Length == 0)
        {
            hasGeneratedLineup = true;
            RefreshShopUI();
            return;
        }

        AddGuaranteedRarePart();
        AddRandomRemainingParts();
        ShuffleLineup();
        hasGeneratedLineup = true;
        RefreshShopUI();
    }

    public void EnsureLineupInitialized()
    {
        if (!hasGeneratedLineup)
        {
            StartShopDayIfNeeded();
            GenerateLineupInternal();
            return;
        }

        if (shopParent != null && shopParent.childCount == 0 && currentLineup.Count > 0)
        {
            RefreshShopUI();
        }
    }

    public void RestoreLineupPart(UpgradePartSO part)
    {
        if (part == null || currentLineup.Contains(part))
        {
            return;
        }

        currentLineup.Add(part);
        RefreshShopUI();

        if (infoText != null)
        {
            infoText.text = $"{part.partName}をラインナップに戻しました。";
        }
    }

    public UpgradePartSO FindPart(string partKey)
    {
        if (string.IsNullOrWhiteSpace(partKey) || allParts == null)
        {
            return null;
        }

        for (int i = 0; i < allParts.Length; i++)
        {
            UpgradePartSO part = allParts[i];
            if (PartMatches(part, partKey))
            {
                return part;
            }
        }

        return null;
    }

    public void EnsureLineupContains(UpgradePartSO part)
    {
        if (part == null)
        {
            return;
        }

        StartShopDayIfNeeded();
        if (!hasGeneratedLineup)
        {
            GenerateLineupInternal();
        }

        if (currentLineup.Contains(part))
        {
            currentLineup.Remove(part);
            currentLineup.Insert(0, part);
            RefreshShopUI();
            return;
        }

        if (currentLineup.Count >= shopSlotCount && currentLineup.Count > 0)
        {
            currentLineup[currentLineup.Count - 1] = part;
            currentLineup.Remove(part);
            currentLineup.Insert(0, part);
        }
        else
        {
            currentLineup.Insert(0, part);
        }

        RefreshShopUI();
    }

    private void RefreshShopUI()
    {
        ClearShopItems();
        if (shopParent == null || shopItemPrefab == null)
        {
            return;
        }

        for (int i = 0; i < currentLineup.Count; i++)
        {
            UpgradePartSO part = currentLineup[i];
            GameObject item = Instantiate(shopItemPrefab, shopParent);
            item.name = BuildShopItemName(part);
            shopItemObjects.Add(item);

            ApplyIcon(item, part);
            ApplyTexts(item, part);
            ApplyTooltip(item, part);
            ApplyDragHandler(item, part);
            ClearButtonClick(item);
        }

        ConfigureShopScrollPadding();
    }

    private void ClearShopItems()
    {
        if (shopParent != null)
        {
            for (int i = shopParent.childCount - 1; i >= 0; i--)
            {
                DestroyShopItem(shopParent.GetChild(i).gameObject);
            }
        }
        else
        {
            for (int i = 0; i < shopItemObjects.Count; i++)
            {
                if (shopItemObjects[i] != null)
                {
                    DestroyShopItem(shopItemObjects[i]);
                }
            }
        }

        shopItemObjects.Clear();
    }

    private void DestroyShopItem(GameObject item)
    {
        if (item == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(item);
        }
        else
        {
            DestroyImmediate(item);
        }
    }

    private void ConfigureShopScrollPadding()
    {
        if (shopParent == null)
        {
            return;
        }

        VerticalLayoutGroup layout = shopParent.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            RectOffset padding = layout.padding;
            padding.bottom = Mathf.Max(padding.bottom, shopContentBottomPadding);
            layout.padding = padding;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(shopParent);
    }

    private void AddGuaranteedRarePart()
    {
        List<UpgradePartSO> highRarity = new List<UpgradePartSO>();
        for (int i = 0; i < allParts.Length; i++)
        {
            UpgradePartSO part = allParts[i];
            if (part != null && part.rarity >= PartRarity.Rare)
            {
                highRarity.Add(part);
            }
        }

        if (highRarity.Count > 0)
        {
            currentLineup.Add(highRarity[Random.Range(0, highRarity.Count)]);
        }
    }

    private void AddRandomRemainingParts()
    {
        List<UpgradePartSO> remaining = new List<UpgradePartSO>();
        for (int i = 0; i < allParts.Length; i++)
        {
            UpgradePartSO part = allParts[i];
            if (part != null && !currentLineup.Contains(part))
            {
                remaining.Add(part);
            }
        }

        while (currentLineup.Count < shopSlotCount && remaining.Count > 0)
        {
            int index = Random.Range(0, remaining.Count);
            currentLineup.Add(remaining[index]);
            remaining.RemoveAt(index);
        }
    }

    private void ShuffleLineup()
    {
        for (int i = currentLineup.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            UpgradePartSO temp = currentLineup[i];
            currentLineup[i] = currentLineup[j];
            currentLineup[j] = temp;
        }
    }

    private static void ApplyIcon(GameObject item, UpgradePartSO part)
    {
        Transform iconTransform = item.transform.Find("Icon");
        if (iconTransform == null)
        {
            return;
        }

        Image iconImage = iconTransform.GetComponent<Image>();
        if (iconImage != null && part != null && part.icon != null)
        {
            iconImage.sprite = part.icon;
        }
    }

    private static void ApplyTexts(GameObject item, UpgradePartSO part)
    {
        TMP_Text nameText = FindText(item, "NameText", 0);
        if (nameText != null)
        {
            nameText.text = part != null ? part.partName : string.Empty;
            nameText.color = part != null ? PartRarityColors.Get(part.rarity) : Color.white;
        }

        TMP_Text costText = FindText(item, "CostText", 1);
        if (costText != null)
        {
            costText.text = BuildCostText(part);
        }
    }

    private static TMP_Text FindText(GameObject item, string childName, int fallbackIndex)
    {
        Transform child = item.transform.Find(childName);
        if (child != null)
        {
            TMP_Text directText = child.GetComponent<TMP_Text>();
            if (directText != null)
            {
                return directText;
            }
        }

        TMP_Text[] texts = item.GetComponentsInChildren<TMP_Text>();
        return fallbackIndex >= 0 && fallbackIndex < texts.Length ? texts[fallbackIndex] : null;
    }

    private static void ApplyTooltip(GameObject item, UpgradePartSO part)
    {
        TooltipTrigger tooltip = item.GetComponent<TooltipTrigger>();
        if (tooltip == null)
        {
            tooltip = item.AddComponent<TooltipTrigger>();
        }

        tooltip.part = part;
        tooltip.tooltipText = part != null ? part.BuildTooltipText() : string.Empty;
    }

    public bool CanAfford(UpgradePartSO part)
    {
        return part != null &&
            MaterialManager.Instance != null &&
            MaterialManager.Instance.CanAfford(part.costs);
    }

    public void ShowCannotAfford(UpgradePartSO part)
    {
        if (infoText != null)
        {
            infoText.text = part != null ? $"{part.partName}の素材が足りません。" : "素材が足りません。";
        }

        if (errorSound != null)
        {
            errorSound.Play();
        }
    }

    public void OnPartPlaced(UpgradePartSO part)
    {
        if (part != null && currentLineup.Remove(part))
        {
            RefreshShopUI();
        }

        if (infoText != null && part != null)
        {
            infoText.text = $"{part.partName}を装備しました。";
        }

        if (purchaseSound != null)
        {
            purchaseSound.Play();
        }

        PartPlaced?.Invoke(part);
    }

    public void OnPartPlacementFailed(UpgradePartSO part)
    {
        if (infoText != null)
        {
            infoText.text = part != null ? $"{part.partName}はそこに配置できません。" : "そこには配置できません。";
        }

        if (errorSound != null)
        {
            errorSound.Play();
        }
    }

    private void ApplyDragHandler(GameObject item, UpgradePartSO part)
    {
        UpgradeShopItemDragHandler dragHandler = item.GetComponent<UpgradeShopItemDragHandler>();
        if (dragHandler == null)
        {
            dragHandler = item.AddComponent<UpgradeShopItemDragHandler>();
        }

        UpgradeGridUI gridUI = targetGridUI != null
            ? targetGridUI
            : UpgradeGridManager.Instance != null ? UpgradeGridManager.Instance.gridUI : null;
        dragHandler.Initialize(part, this, gridUI);
    }

    private void StartShopDayIfNeeded()
    {
        if (shopDay >= 0)
        {
            return;
        }

        shopDay = StatusManager.Instance != null
            ? StatusManager.Instance.BeginUpgradeShopDay()
            : 0;
    }

    private static void RefreshMaterialDisplays()
    {
        MaterialDisplay[] displays = FindObjectsByType<MaterialDisplay>(FindObjectsSortMode.None);
        for (int i = 0; i < displays.Length; i++)
        {
            displays[i].UpdateMaterialAmount();
        }
    }

    private static void ClearButtonClick(GameObject item)
    {
        Button button = item.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }
    }

    private static string BuildShopItemName(UpgradePartSO part)
    {
        string key = part != null && !string.IsNullOrWhiteSpace(part.name)
            ? part.name
            : part != null ? part.partName : "Unknown";
        return "ShopItem_" + key;
    }

    private static bool PartMatches(UpgradePartSO part, string partKey)
    {
        if (part == null)
        {
            return false;
        }

        return string.Equals(part.name, partKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(part.partName, partKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCostText(UpgradePartSO part)
    {
        if (part == null || part.costs == null || part.costs.Length == 0)
        {
            return "無料";
        }

        string result = string.Empty;
        for (int i = 0; i < part.costs.Length; i++)
        {
            CraftCost cost = part.costs[i];
            if (cost == null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(result))
            {
                result += "  ";
            }

            result += $"{FormatMaterialName(cost.type)} x{cost.amount}";
        }

        return result;
    }

    private static string FormatMaterialName(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap: return "Scrap";
            case MaterialManager.MaterialType.Gear: return "Gear";
            case MaterialManager.MaterialType.UpgradeCore: return "UpCore";
            case MaterialManager.MaterialType.ModuleCore_lv1: return "ModC1";
            case MaterialManager.MaterialType.ModuleCore_lv2: return "ModC2";
            case MaterialManager.MaterialType.ModuleCore_lv3: return "ModC3";
            default: return type.ToString();
        }
    }
}
