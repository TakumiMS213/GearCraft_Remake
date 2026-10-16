using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using GearCraft.Scripts.Craft;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 武器クラフトシステム（再構築版）。
/// CraftRecipeSOに基づいたデータ駆動型クラフト。
/// </summary>
public class CraftManager : MonoBehaviour
{
    public event Action<CraftRecipeSO> RecipeCrafted;
    public event Action<CraftRecipeSO> RecipeSelected;
    public event Action<CraftRecipeSO> CraftFailed;

    public CraftRecipeSO SelectedRecipe => selectedRecipe;
    public bool IsInitialized { get; private set; }
    public bool IsReadyForTutorial { get; private set; }
    public bool IsCrafting => pendingCraftRecipe != null ||
        (transitionManager != null && transitionManager.IsPlaying);

    [Header("レシピ一覧")]
    public List<CraftRecipeSO> recipes;

    [Header("UI")]
    public CraftTransitionManager transitionManager;
    public Button craftButton;
    public MaterialDisplay materialDisplay;
    [SerializeField] private TMP_Text craftStatusText;
    [SerializeField] private GameObject craftFeedbackPanel;

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
    private readonly List<CraftRecipeSO> displayRecipes = new List<CraftRecipeSO>();
    private readonly List<CraftRecipeSO> pendingUnlockNotifications = new List<CraftRecipeSO>();
    private readonly List<Image> recipeVisualImages = new List<Image>();
    private CraftRecipeSO selectedRecipe;
    private CraftRecipeSO pendingCraftRecipe;
    private bool showCraftFailure;

    private void OnEnable()
    {
        MaterialManager.MaterialsChanged += RefreshMaterialCountLabels;
        if (transitionManager != null)
            transitionManager.TransitionFinished += OnCraftTransitionFinished;
        RefreshMaterialCountLabels();
        RefreshCraftControls();
    }

    private void OnDisable()
    {
        showCraftFailure = false;
        pendingCraftRecipe = null;
        MaterialManager.MaterialsChanged -= RefreshMaterialCountLabels;
        if (transitionManager != null)
        {
            transitionManager.TransitionFinished -= OnCraftTransitionFinished;
            transitionManager.CancelTransition();
        }
        if (errorImage != null)
        {
            errorImage.DOKill();
            errorImage.gameObject.SetActive(false);
        }
    }

    private void OnCraftTransitionFinished()
    {
        CraftRecipeSO completedRecipe = pendingCraftRecipe;
        pendingCraftRecipe = null;
        RefreshCraftControls();
        // Hidden/cancelled presentations are reconciled when the tutorial is resumed.
        if (completedRecipe != null && isActiveAndEnabled)
            RecipeCrafted?.Invoke(completedRecipe);
    }

    private void RefreshCraftControls()
    {
        bool alreadyOwned = HasCraftResult(selectedRecipe);
        if (craftButton != null)
        {
            craftButton.gameObject.SetActive(selectedRecipe != null);
            // Shortage clicks remain available so the tutorial can explain a failed attempt.
            craftButton.interactable = selectedRecipe != null && !IsCrafting && !alreadyOwned;
        }
        if (craftStatusText != null)
        {
            craftStatusText.text = IsCrafting ? string.Empty : alreadyOwned ? "作成済み" :
                showCraftFailure ? GetCraftFeedback() : string.Empty;
            craftStatusText.gameObject.SetActive(!string.IsNullOrEmpty(craftStatusText.text));
            if (craftFeedbackPanel != null)
                craftFeedbackPanel.SetActive(!string.IsNullOrEmpty(craftStatusText.text));
        }
        for (int i = 0; i < recipeButtons.Count && i < displayRecipes.Count; i++)
        {
            if (recipeButtons[i] != null)
                recipeButtons[i].interactable = !IsCrafting && unlockedRecipes.Contains(displayRecipes[i]);
        }
    }

    private void RefreshMaterialCountLabels()
    {
        if (selectedRecipe == null || selectedRecipe.costs == null) return;

        ResolveRecipeVisualImages();
        foreach (CraftCost cost in selectedRecipe.costs)
        {
            if (cost == null) continue;
            Image image = FindRecipeVisualImage(GetMaterialImageName(cost.type));
            if (image != null)
            {
                UpdateMaterialCountLabel(image.rectTransform, cost);
            }
        }
        RefreshCraftControls();
    }

    private string GetCraftFeedback()
    {
        if (selectedRecipe == null) return "作成するレシピを選んでください。";
        if (!MeetsRequiredWeapon(selectedRecipe))
            return $"作成には {selectedRecipe.requiredWeapon.weaponName} の装備が必要です。";
        if (MaterialManager.Instance == null) return "材料を確認できません。作業台を開き直してください。";
        string shortage = MaterialManager.Instance.GetShortageText(selectedRecipe.costs);
        return string.IsNullOrEmpty(shortage) ? "材料が揃いました。作成できます。" : $"材料が足りません。\n{shortage}";
    }

private IEnumerator Start()
    {
        yield return null;

        InitializeUnlockedRecipes();
        ConfigureRecipeButtons();
        if (unlockedRecipes.Count > 0)
        {
            CraftRecipeSO previousRecipe = FindUnlockedRecipe(StatusManager.Instance?.LastSelectedCraftRecipeId);
            SelectRecipe(previousRecipe != null ? unlockedRecipes.IndexOf(previousRecipe) : 0);
        }

        IsInitialized = true;
        yield return ShowUnlockNotificationsAsync();
        IsReadyForTutorial = true;
    }

    /// <summary>
    /// レシピを選択する（UIボタンから呼ぶ）
    /// </summary>
public void SelectRecipe(int index)
    {
        if (IsCrafting || index < 0 || index >= unlockedRecipes.Count)
        {
            return;
        }

        selectedRecipe = unlockedRecipes[index];
        if (StatusManager.Instance != null)
        {
            StatusManager.Instance.LastSelectedCraftRecipeId = selectedRecipe.UnlockId;
        }
        showCraftFailure = false;

        if (materialDisplay != null)
        {
            materialDisplay.SetCurrentRecipe(selectedRecipe);
        }

        ApplyRecipeVisuals(selectedRecipe);
        RefreshCraftControls();
        RecipeSelected?.Invoke(selectedRecipe);
    }

    public bool TrySelectRecipe(CraftRecipeSO recipe)
    {
        int index = unlockedRecipes.IndexOf(recipe);
        if (IsCrafting || index < 0)
        {
            return false;
        }

        SelectRecipe(index);
        return true;
    }

    public CraftRecipeSO FindUnlockedRecipe(string recipeKey)
    {
        if (string.IsNullOrWhiteSpace(recipeKey))
        {
            return null;
        }

        for (int i = 0; i < unlockedRecipes.Count; i++)
        {
            CraftRecipeSO recipe = unlockedRecipes[i];
            if (RecipeMatches(recipe, recipeKey))
            {
                return recipe;
            }
        }

        return null;
    }

    public CraftRecipeSO FindRecipe(string recipeKey)
    {
        if (string.IsNullOrWhiteSpace(recipeKey) || recipes == null)
        {
            return null;
        }

        for (int i = 0; i < recipes.Count; i++)
        {
            CraftRecipeSO recipe = recipes[i];
            if (RecipeMatches(recipe, recipeKey))
            {
                return recipe;
            }
        }

        return null;
    }

    /// <summary>
    /// 選択中のレシピが作成可能か判定
    /// </summary>
    public bool CanCraft()
    {
        return !IsCrafting && CanCraftRecipe(selectedRecipe);
    }

    public static bool CanCraftRecipe(CraftRecipeSO recipe)
    {
        return StatusManager.Instance != null && !HasCraftResult(recipe) &&
            HasEnoughMaterials(recipe) && MeetsRequiredWeapon(recipe);
    }

    // Unique outcomes cannot benefit from another purchase; numerical modules can stack.
    public static bool HasCraftResult(CraftRecipeSO recipe)
    {
        StatusManager status = StatusManager.Instance;
        if (recipe == null || status == null) return false;
        switch (recipe.resultType)
        {
            case CraftResultType.Weapon:
                return (recipe.weaponResult != null || recipe.additionalWeaponResult != null) &&
                    (recipe.weaponResult == null || OwnsWeapon(status, recipe.weaponResult)) &&
                    (recipe.additionalWeaponResult == null || OwnsWeapon(status, recipe.additionalWeaponResult));
            case CraftResultType.PunkDrive:
                return status.punkDrive;
            case CraftResultType.Module:
                if (recipe.statBonus > 0) return false;
                switch (recipe.moduleFlag)
                {
                    case "module_scrap": return status.module_scrap;
                    case "module_repair": return status.module_repair;
                    case "module_barrier": return status.module_barrier;
                }
                break;
        }
        return false;
    }

    private static bool OwnsWeapon(StatusManager status, WeaponDataSO weapon)
    {
        if (status.ownedWeapons == null) return false;
        foreach (WeaponDataSO owned in status.ownedWeapons)
            if (IsSameWeapon(owned, weapon)) return true;
        return false;
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

    private static bool RecipeMatches(CraftRecipeSO recipe, string recipeKey)
    {
        if (recipe == null)
        {
            return false;
        }

        return string.Equals(recipe.UnlockId, recipeKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recipe.DisplayName, recipeKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recipe.recipeName, recipeKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(recipe.name, recipeKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// クラフト実行（OnCraftButtonのOnClickに割り当て）
    /// </summary>
    public void OnCraftButton()
    {
        if (IsCrafting) return;
        CraftRecipeSO recipe = selectedRecipe;
        bool canCraft = CanCraft();
        // Material/result changes can refresh the UI before the animation starts.
        // Keep all completion feedback pending until its final fade has finished.
        if (canCraft) pendingCraftRecipe = recipe;
        if (canCraft && MaterialManager.Instance.SpendCosts(recipe.costs))
        {
            showCraftFailure = false;
            // The result is committed once; cancelling its presentation does not undo crafting.
            ApplyCraftResult(recipe);
            if (transitionManager != null)
                transitionManager.StartTransition();
            RefreshCraftControls();

            if (successSE != null)
                successSE.Play();

            if (StatusManager.Instance != null)
                StatusManager.Instance.DormTasks.hasCrafted = true;
            if (materialDisplay != null)
                materialDisplay.UpdateMaterialAmount();

            if (transitionManager == null || !transitionManager.IsPlaying)
                OnCraftTransitionFinished();
        }
        else
        {
            pendingCraftRecipe = null;
            showCraftFailure = true;
            RefreshCraftControls();
            CraftFailed?.Invoke(recipe);
            if (errorSE != null)
                errorSE.Play();

            if (errorImage != null)
            {
                errorImage.DOKill();
                // The legacy image always says "素材不足", even for a weapon mismatch.
                // Keep the actual cause visible beside the action until the player resolves it.
                errorImage.gameObject.SetActive(false);
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
        GearCraft.Gallery.GalleryProgress.RecordCraft(recipe);
    }


private void InitializeUnlockedRecipes()
    {
        int bossKillCount = StatusManager.Instance != null ? StatusManager.Instance.bossKillCount : 0;
        CraftRecipeUnlockModel model = new CraftRecipeUnlockModel(recipes);
        model.CollectUnlockedRecipes(bossKillCount, unlockedRecipes);
        model.CollectDisplayRecipes(bossKillCount, displayRecipes);
        model.CollectNewUnlocks(
            bossKillCount,
            id => StatusManager.Instance != null && StatusManager.Instance.IsCraftUnlockNotified(id),
            pendingUnlockNotifications);
    }

    private void ConfigureRecipeButtons()
    {
        ResolveRecipeButtons();
        EnsureRecipeButtonCapacity(displayRecipes.Count);
        ResolveFont();

        for (int i = 0; i < recipeButtons.Count; i++)
        {
            Button button = recipeButtons[i];
            if (button == null)
            {
                continue;
            }

            bool hasRecipe = i < displayRecipes.Count;
            button.gameObject.SetActive(hasRecipe);
            // Serialized index listeners refer to the old list and must also be replaced.
            button.onClick = new Button.ButtonClickedEvent();
            if (!hasRecipe)
            {
                continue;
            }

            CraftRecipeSO recipe = displayRecipes[i];
            bool isUnlocked = unlockedRecipes.Contains(recipe);
            button.name = "Recipe_" + (string.IsNullOrEmpty(recipe.UnlockId) ? recipe.name : recipe.UnlockId);
            button.interactable = isUnlocked;

            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = isUnlocked ? 1f : 0.5f;
            group.interactable = isUnlocked;
            group.blocksRaycasts = isUnlocked;

            // Apply the same opacity to the whole row without a second disabled-state fade.
            ColorBlock colors = button.colors;
            colors.disabledColor = colors.normalColor;
            button.colors = colors;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.font = dotGothicFont != null ? dotGothicFont : label.font;
                label.text = isUnlocked ? recipe.DisplayName : "？？？";
                label.color = isUnlocked ? GetRecipeTextColor(recipe) : Color.white;
            }

            if (isUnlocked)
            {
                button.onClick.AddListener(() => TrySelectRecipe(recipe));
            }
        }
    }

    private static Color GetRecipeTextColor(CraftRecipeSO recipe)
    {
        return recipe != null && recipe.resultType == CraftResultType.Weapon
            ? WeaponRarityColors.Get(recipe.weaponRarity)
            : Color.white;
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
        Image image = ShowRecipeVisualImage(resultName);
        ConfigureResultImage(image, recipe);
    }

    private void ShowRequiredWeaponImage(CraftRecipeSO recipe)
    {
        if (recipe == null || recipe.requiredWeapon == null)
        {
            return;
        }

        string imageName = recipe.requiredWeapon.weaponType == WeaponType.Melee ? "Material_Sword" : "Material_Gun";
        Image image = ShowRecipeVisualImage(imageName);
        ConfigureRequiredWeaponImage(image, recipe);
    }

    private static void ConfigureRequiredWeaponImage(Image image, CraftRecipeSO recipe)
    {
        if (image == null)
        {
            return;
        }

        image.raycastTarget = recipe != null && recipe.requiredWeapon != null;
        CraftRequiredWeaponTooltipTrigger trigger = image.GetComponent<CraftRequiredWeaponTooltipTrigger>();
        if (trigger == null)
        {
            trigger = image.gameObject.AddComponent<CraftRequiredWeaponTooltipTrigger>();
        }

        trigger.Configure(recipe);
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
            case MaterialManager.MaterialType.UpgradeCore: return "Material_UpgradeCore";
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

    private void ConfigureResultImage(Image image, CraftRecipeSO recipe)
    {
        if (image == null)
        {
            return;
        }

        CraftResultDescriptionSO description = recipe != null ? recipe.resultDescription : null;
        CraftResultTooltipTrigger trigger = image.GetComponent<CraftResultTooltipTrigger>();
        if (description == null)
        {
            image.raycastTarget = false;
            if (trigger != null)
            {
                trigger.Configure(null);
            }

            return;
        }

        image.raycastTarget = true;
        if (trigger == null)
        {
            trigger = image.gameObject.AddComponent<CraftResultTooltipTrigger>();
        }

        trigger.Configure(description);
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
