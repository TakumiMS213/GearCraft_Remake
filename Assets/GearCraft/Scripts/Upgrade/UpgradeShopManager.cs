using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeShopManager : MonoBehaviour
{
    [Header("Parts")]
    public UpgradePartSO[] allParts;
    public int shopSlotCount = 3;

    [Header("UI")]
    public RectTransform shopParent;
    public GameObject shopItemPrefab;
    public TMP_Text infoText;

    [Header("Audio")]
    public AudioSource purchaseSound;
    public AudioSource errorSound;

    private readonly List<UpgradePartSO> currentLineup = new List<UpgradePartSO>();
    private readonly List<GameObject> shopItemObjects = new List<GameObject>();

    public void GenerateLineup()
    {
        currentLineup.Clear();
        if (allParts == null || allParts.Length == 0)
        {
            RefreshShopUI();
            return;
        }

        AddGuaranteedRarePart();
        AddRandomRemainingParts();
        ShuffleLineup();
        RefreshShopUI();
    }

    public void TryPurchase(UpgradePartSO part)
    {
        if (part == null || MaterialManager.Instance == null)
        {
            return;
        }

        if (!MaterialManager.Instance.CanAfford(part.costs))
        {
            if (infoText != null)
            {
                infoText.text = "Not enough materials.";
            }

            if (errorSound != null)
            {
                errorSound.Play();
            }

            return;
        }

        MaterialManager.Instance.SpendCosts(part.costs);
        int inventoryIndex = StatusManager.Instance != null
            ? StatusManager.Instance.AcquireUpgradePart(part)
            : -1;

        if (UpgradeGridManager.Instance != null && UpgradeGridManager.Instance.gridUI != null)
        {
            UpgradeGridManager.Instance.gridUI.SelectPart(part, inventoryIndex);
            UpgradeGridManager.Instance.RefreshUI();
        }

        if (infoText != null)
        {
            infoText.text = $"{part.partName} acquired. Place it on the grid.";
        }

        if (purchaseSound != null)
        {
            purchaseSound.Play();
        }
    }

    private void RefreshShopUI()
    {
        for (int i = 0; i < shopItemObjects.Count; i++)
        {
            if (shopItemObjects[i] != null)
            {
                DestroyShopItem(shopItemObjects[i]);
            }
        }

        shopItemObjects.Clear();
        if (shopParent == null || shopItemPrefab == null)
        {
            return;
        }

        for (int i = 0; i < currentLineup.Count; i++)
        {
            UpgradePartSO part = currentLineup[i];
            GameObject item = Instantiate(shopItemPrefab, shopParent);
            shopItemObjects.Add(item);

            ApplyIcon(item, part);
            ApplyTexts(item, part);
            ApplyTooltip(item, part);
            ApplyButton(item, part);
        }
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
        TMP_Text[] texts = item.GetComponentsInChildren<TMP_Text>();
        if (texts.Length > 0)
        {
            texts[0].text = part != null ? part.partName : string.Empty;
        }

        if (texts.Length > 1)
        {
            texts[1].text = BuildCostText(part);
        }
    }

    private static void ApplyTooltip(GameObject item, UpgradePartSO part)
    {
        TooltipTrigger tooltip = item.GetComponent<TooltipTrigger>();
        if (tooltip == null)
        {
            tooltip = item.AddComponent<TooltipTrigger>();
        }

        tooltip.tooltipText = part != null ? part.BuildTooltipText() : string.Empty;
    }

    private void ApplyButton(GameObject item, UpgradePartSO part)
    {
        Button button = item.GetComponent<Button>();
        if (button != null)
        {
            UpgradePartSO capturedPart = part;
            button.onClick.AddListener(() => TryPurchase(capturedPart));
        }
    }

    private static string BuildCostText(UpgradePartSO part)
    {
        if (part == null || part.costs == null || part.costs.Length == 0)
        {
            return "Free";
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
