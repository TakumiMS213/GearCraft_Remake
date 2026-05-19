using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

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

    private CraftRecipeSO selectedRecipe;

    void Start()
    {
        // 初期状態で最初のレシピを選択
        if (recipes != null && recipes.Count > 0)
            SelectRecipe(0);
    }

    /// <summary>
    /// レシピを選択する（UIボタンから呼ぶ）
    /// </summary>
    public void SelectRecipe(int index)
    {
        if (index < 0 || index >= recipes.Count) return;
        selectedRecipe = recipes[index];

        if (materialDisplay != null)
            materialDisplay.SetCurrentRecipe(selectedRecipe);
    }

    /// <summary>
    /// 選択中のレシピが作成可能か判定
    /// </summary>
    public bool CanCraft()
    {
        if (selectedRecipe == null) return false;
        if (MaterialManager.Instance == null) return false;

        // 素材チェック
        if (!MaterialManager.Instance.CanAfford(selectedRecipe.costs))
            return false;

        // 前提武器チェック
        if (selectedRecipe.requiredWeapon != null && StatusManager.Instance != null)
        {
            if (!StatusManager.Instance.ownedWeapons.Contains(selectedRecipe.requiredWeapon))
                return false;
        }

        return true;
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
}
