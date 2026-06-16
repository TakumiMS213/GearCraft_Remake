using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using GearCraft.Scripts.Craft;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 武器クラフトシステム（再構築版）。
/// CraftRecipeSOに基づいたデータ駆動型クラフト。
/// </summary>
public class CraftManager : MonoBehaviour
{
    [Header("レシピ一覧")]
    public List<CraftRecipeSO> recipes;

    [Header("UI")]
    public CraftTransitionManager transitionManager;
    public Button craftButton;
    public MaterialDisplay materialDisplay;

    [Header("効果音")]
    public AudioSource successSE;
    public AudioSource errorSE;

    [Header("エラー表示")]
    public Image errorImage;
    public float errorDisplayTime = 1f;
    public float errorFadeTime = 0.3f;

    [Header("Unlock")]
    [SerializeField] private List<Button> recipeButtons = new List<Button>();
    [SerializeField] private CraftUnlockPopupView unlockPopupView;
    [SerializeField] private TMP_FontAsset dotGothicFont;

    private readonly List<CraftRecipeSO> unlockedRecipes = new List<CraftRecipeSO>();
    private readonly List<CraftRecipeSO> pendingUnlockNotifications = new List<CraftRecipeSO>();
    private readonly List<Image> recipeVisualImages = new List<Image>();
    private CraftRecipeSO selectedRecipe;

private IEnumerator Start()
    {
        yield return null;

        InitializeUnlockedRecipes();
        ConfigureRecipeButtons();
        if (unlockedRecipes.Count > 0)
        {
            SelectRecipe(0);
        }

        yield return ShowUnlockNotificationsAsync();
    }

    /// <summary>
    /// レシピを選択する（UIボタンから呼ぶ）
    /// </summary>
public void SelectRecipe(int index)
    {
        if (index < 0 || index >= unlockedRecipes.Count)
        {
            return;
        }

        selectedRecipe = unlockedRecipes[index];

        if (materialDisplay != null)
        {
            materialDisplay.SetCurrentRecipe(selectedRecipe);
        }

        if (craftButton != null)
        {
            craftButton.gameObject.SetActive(true);
        }

        ApplyRecipeVisuals(selectedRecipe);
    }

    /// <summary>
    /// 選択中のレシピが作成可能か判定
    /// </summary>
    public bool CanCraft()
    {
        return CanCraftRecipe(selectedRecipe);
    }

    public static bool CanCraftRecipe(CraftRecipeSO recipe)
    {
        return HasEnoughMaterials(recipe) && MeetsRequiredWeapon(recipe);
    }

    public static bool HasEnoughMaterials(CraftRecipeSO recipe)
    {
        if (recipe == null || MaterialManager.Instance == null) return false;
        return MaterialManager.Instance.CanAfford(recipe.costs);
    }

    public static bool MeetsRequiredWeapon(CraftRecipeSO recipe)
    {
        if (recipe == null) return false;
        if (recipe.requiredWeapon == null) return true;
        if (StatusManager.Instance == null) return false;

        return IsSameWeapon(StatusManager.Instance.currentWeapon, recipe.requiredWeapon);
    }

    private static bool IsSameWeapon(WeaponDataSO current, WeaponDataSO required)
    {
        if (current == null || required == null) return false;
        if (current == required) return true;
        return !string.IsNullOrEmpty(current.weaponName) && current.weaponName == required.weaponName;
    }

    /// <summary>
    /// クラフト実行（OnCraftButtonのOnClickに割り当て）
    /// </summary>
    public void OnCraftButton()
    {
        if (CanCraft())
        {
            // 素材消費
            MaterialManager.Instance.SpendCosts(selectedRecipe.costs);

            // トランジション
            if (transitionManager != null)
                transitionManager.StartTransition();

            if (craftButton != null)
                craftButton.gameObject.SetActive(false);

            if (successSE != null)
                successSE.Play();

            // 結果を適用
            ApplyCraftResult(selectedRecipe);

            if (materialDisplay != null)
                materialDisplay.UpdateMaterialAmount();
        }
        else
        {
            // 失敗
            if (errorSE != null)
                errorSE.Play();

            if (errorImage != null)
            {
                errorImage.DOKill();
                errorImage.gameObject.SetActive(true);
                var c = errorImage.color;
                errorImage.color = new Color(c.r, c.g, c.b, 1f);
                errorImage.DOFade(0f, errorFadeTime)
                    .SetDelay(errorDisplayTime)
                    .OnComplete(() => errorImage.gameObject.SetActive(false));
            }
        }
    }

    /// <summary>
    /// クラフト結果をStatusManagerに反映
    /// </summary>
    private void ApplyCraftResult(CraftRecipeSO recipe)
    {
        if (StatusManager.Instance == null) return;
        var status = StatusManager.Instance;

        switch (recipe.resultType)
        {
            case CraftResultType.Weapon:
                if (recipe.weaponResult != null)
                {
                    status.AcquireWeapon(recipe.weaponResult);
                    status.EquipWeapon(recipe.weaponResult);
                }
                if (recipe.additionalWeaponResult != null)
                {
                    status.AcquireWeapon(recipe.additionalWeaponResult);
                }
                break;

            case CraftResultType.PunkDrive:
                status.punkDrive = true;
                break;

            case CraftResultType.Module:
                if (!string.IsNullOrEmpty(recipe.moduleFlag))
                {
                    switch (recipe.moduleFlag)
                    {
                        case "module_scrap": status.module_scrap = true; break;
                        case "module_repair": status.module_repair = true; break;
                        case "module_barrier": status.module_barrier = true; break;
                    }
                }
                if (recipe.statBonus > 0)
                    status.craftWeaponDamagebuff += recipe.statBonus;
                break;
        }
    }


private void InitializeUnlockedRecipes()
    {
        int bossKillCount = StatusManager.Instance != null ? StatusManager.Instance.bossKillCount : 0;
        CraftRecipeUnlockModel model = new CraftRecipeUnlockModel(recipes);
        model.CollectUnlockedRecipes(bossKillCount, unlockedRecipes);
        model.CollectNewUnlocks(
            bossKillCount,
            id => StatusManager.Instance != null && StatusManager.Instance.IsCraftUnlockNotified(id),
            pendingUnlockNotifications);
    }

    private void ConfigureRecipeButtons()
    {
        ResolveRecipeButtons();
        EnsureRecipeButtonCapacity(unlockedRecipes.Count);
        ResolveFont();

        for (int i = 0; i < recipeButtons.Count; i++)
        {
            Button button = recipeButtons[i];
            if (button == null)
            {
                continue;
            }

            bool hasRecipe = i < unlockedRecipes.Count;
            button.gameObject.SetActive(hasRecipe);
            button.onClick.RemoveAllListeners();
            if (!hasRecipe)
            {
                continue;
            }

            int recipeIndex = i;
            CraftRecipeSO recipe = unlockedRecipes[i];
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.font = dotGothicFont != null ? dotGothicFont : label.font;
                label.text = recipe.DisplayName;
            }

            button.onClick.AddListener(() => SelectRecipe(recipeIndex));
        }
    }

    private void ResolveRecipeButtons()
    {
        if (recipeButtons.Count > 0)
        {
            return;
        }

        ButtonImageController imageController = GetComponent<ButtonImageController>();
        if (imageController == null || imageController.actionSets == null)
        {
            return;
        }

        for (int i = 0; i < imageController.actionSets.Count; i++)
        {
            Button button = imageController.actionSets[i] != null ? imageController.actionSets[i].button : null;
            if (button != null && !recipeButtons.Contains(button))
            {
                recipeButtons.Add(button);
            }
        }
    }

    private void EnsureRecipeButtonCapacity(int requiredCount)
    {
        if (requiredCount <= recipeButtons.Count || recipeButtons.Count == 0)
        {
            return;
        }

        Button template = recipeButtons[recipeButtons.Count - 1];
        if (template == null)
        {
            return;
        }

        RectTransform templateRect = template.GetComponent<RectTransform>();
        Transform parent = template.transform.parent;
        float yStep = GetRecipeButtonYStep();
        int templateIndex = recipeButtons.Count - 1;

        while (recipeButtons.Count < requiredCount)
        {
            Button button = Instantiate(template, parent);
            button.name = $"CraftRecipeButton_{recipeButtons.Count}";

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null && templateRect != null)
            {
                rect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, yStep * (recipeButtons.Count - templateIndex));
            }

            recipeButtons.Add(button);
        }
    }

    private float GetRecipeButtonYStep()
    {
        if (recipeButtons.Count < 2)
        {
            return -80f;
        }

        RectTransform previous = recipeButtons[recipeButtons.Count - 2] != null
            ? recipeButtons[recipeButtons.Count - 2].GetComponent<RectTransform>()
            : null;
        RectTransform last = recipeButtons[recipeButtons.Count - 1] != null
            ? recipeButtons[recipeButtons.Count - 1].GetComponent<RectTransform>()
            : null;

        if (previous == null || last == null)
        {
            return -80f;
        }

        float step = last.anchoredPosition.y - previous.anchoredPosition.y;
        return Mathf.Approximately(step, 0f) ? -80f : step;
    }

    private IEnumerator ShowUnlockNotificationsAsync()
    {
        if (pendingUnlockNotifications.Count == 0)
        {
            yield break;
        }

        CraftUnlockPopupView view = ResolveUnlockPopupView();
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (view == null || parentCanvas == null)
        {
            yield break;
        }

        for (int i = 0; i < pendingUnlockNotifications.Count; i++)
        {
            CraftRecipeSO recipe = pendingUnlockNotifications[i];
            if (recipe == null)
            {
                continue;
            }

            yield return view.ShowAsync(recipe, parentCanvas.transform);
            StatusManager.Instance?.MarkCraftUnlockNotified(recipe.UnlockId);
        }
    }

    private CraftUnlockPopupView ResolveUnlockPopupView()
    {
        if (unlockPopupView != null)
        {
            return unlockPopupView;
        }

        unlockPopupView = GetComponent<CraftUnlockPopupView>();
        if (unlockPopupView == null)
        {
            unlockPopupView = gameObject.AddComponent<CraftUnlockPopupView>();
        }

        return unlockPopupView;
    }

    private void ResolveFont()
    {
        if (dotGothicFont != null)
        {
            return;
        }

        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_FontAsset font = texts[i] != null ? texts[i].font : null;
            if (font != null && font.name.Contains("DotGothic"))
            {
                dotGothicFont = font;
                return;
            }
        }

        dotGothicFont = Resources.Load<TMP_FontAsset>("DotGothic16-Regular SDF");
    }




private void ApplyRecipeVisuals(CraftRecipeSO recipe)
    {
        ResolveRecipeVisualImages();
        HideRecipeVisualImages();

        if (recipe == null)
        {
            return;
        }

        ShowResultImage(recipe);
        ShowRequiredWeaponImage(recipe);
        ShowCostImages(recipe);
    }

    private void ResolveRecipeVisualImages()
    {
        if (recipeVisualImages.Count > 0)
        {
            return;
        }

        Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string objectName = image.gameObject.name;
            if (objectName.StartsWith("Material_") || objectName.StartsWith("Result_"))
            {
                recipeVisualImages.Add(image);
            }
        }
    }

    private void HideRecipeVisualImages()
    {
        for (int i = 0; i < recipeVisualImages.Count; i++)
        {
            if (recipeVisualImages[i] != null)
            {
                recipeVisualImages[i].gameObject.SetActive(false);
            }
        }
    }

private void ShowResultImage(CraftRecipeSO recipe)
    {
        string resultName = "Result_" + ToVisualKey(recipe.DisplayName);
        ShowRecipeVisualImage(resultName);
    }

    private void ShowRequiredWeaponImage(CraftRecipeSO recipe)
    {
        if (recipe.requiredWeapon == null)
        {
            return;
        }

        ShowRecipeVisualImage(recipe.DisplayName == "GearCraft" ? "Material_Sword" : "Material_Gun");
    }

private void ShowCostImages(CraftRecipeSO recipe)
    {
        if (recipe.costs == null)
        {
            return;
        }

        for (int i = 0; i < recipe.costs.Length; i++)
        {
            CraftCost cost = recipe.costs[i];
            Image image = ShowRecipeVisualImage(GetMaterialImageName(cost.type));
            ConfigureMaterialImage(image, cost);
        }
    }





    private Image FindRecipeVisualImage(string objectName)
    {
        for (int i = 0; i < recipeVisualImages.Count; i++)
        {
            Image image = recipeVisualImages[i];
            if (image != null && image.gameObject.name == objectName)
            {
                return image;
            }
        }

        return null;
    }

private Image ShowRecipeVisualImage(string objectName)
    {
        Image image = FindRecipeVisualImage(objectName);
        if (image != null)
        {
            image.gameObject.SetActive(true);
        }

        return image;
    }

    private static string ToVisualKey(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Replace(" ", string.Empty);
    }

    private static string GetMaterialImageName(MaterialManager.MaterialType type)
    {
        switch (type)
        {
            case MaterialManager.MaterialType.Scrap: return "Material_Scrap";
            case MaterialManager.MaterialType.Gear: return "Material_Gear";
            case MaterialManager.MaterialType.UpgradeCore: return "Material_lv1_drop";
            case MaterialManager.MaterialType.ModuleCore_lv1: return "Material_lv1_drop";
            case MaterialManager.MaterialType.ModuleCore_lv2: return "Material_lv2_drop";
            case MaterialManager.MaterialType.ModuleCore_lv3: return "Material_lv3_drop";
            default: return string.Empty;
        }
    }


private void ConfigureMaterialImage(Image image, CraftCost cost)
    {
        if (image == null || cost == null)
        {
            return;
        }

        image.raycastTarget = true;

        CraftMaterialTooltipTrigger trigger = image.GetComponent<CraftMaterialTooltipTrigger>();
        if (trigger == null)
        {
            trigger = image.gameObject.AddComponent<CraftMaterialTooltipTrigger>();
        }

        trigger.Configure(cost.type);
        UpdateMaterialCountLabel(image.rectTransform, cost);
    }

    private void UpdateMaterialCountLabel(RectTransform parent, CraftCost cost)
    {
        if (parent == null || cost == null)
        {
            return;
        }

        TMP_Text label = parent.Find("MaterialRequirementText")?.GetComponent<TMP_Text>();
        if (label == null)
        {
            GameObject labelObject = new GameObject("MaterialRequirementText", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -28f);
            rect.sizeDelta = new Vector2(180f, 34f);

            label = labelObject.GetComponent<TMP_Text>();
            label.font = dotGothicFont != null ? dotGothicFont : label.font;
            label.fontSize = 52f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
        }

        int current = MaterialManager.Instance != null ? MaterialManager.Instance.GetMaterial(cost.type) : 0;
        label.text = $"{current}/{cost.amount}";
        label.color = current >= cost.amount ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
        label.gameObject.SetActive(true);
    }
}
