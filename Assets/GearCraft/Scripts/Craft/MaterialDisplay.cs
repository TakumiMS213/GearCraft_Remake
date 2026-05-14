using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 素材表示（動的に全素材を表示）＋ 選択中レシピのコスト表示
/// </summary>
public class MaterialDisplay : MonoBehaviour
{
    [Header("動的表示")]
    public RectTransform materialListParent;     // 素材一覧の親
    public GameObject materialEntryPrefab;       // 素材1行分のプレハブ（Image + Text）


    [Header("コスト表示（現在数/必要数）")]
    [Tooltip("左側のコスト表示TMP")]
    public TMP_Text costTextLeft;
    [Tooltip("右側のコスト表示TMP")]
    public TMP_Text costTextRight;

    [Header("色設定")]
    public Color enoughColor = Color.white;
    public Color notEnoughColor = new Color(1f, 0.3f, 0.3f, 1f); // 赤

    private List<GameObject> dynamicEntries = new List<GameObject>();
    private CraftRecipeSO currentRecipe;

    void Start()
    {
        UpdateMaterialAmount();
    }

    /// <summary>
    /// 選択中のレシピを設定する（CraftManagerから呼ぶ）
    /// </summary>
    public void SetCurrentRecipe(CraftRecipeSO recipe)
    {
        currentRecipe = recipe;
        UpdateMaterialAmount();
    }

    public void UpdateMaterialAmount()
    {
        if (MaterialManager.Instance == null) return;


        // コスト表示を更新
        UpdateCostDisplay();

        // 動的リスト更新
        if (materialListParent != null && materialEntryPrefab != null)
        {
            // 既存エントリをクリア
            foreach (var entry in dynamicEntries)
                Destroy(entry);
            dynamicEntries.Clear();

            var allMats = MaterialManager.Instance.GetAllMaterials();
            foreach (var (type, count, name) in allMats)
            {
                GameObject entry = Instantiate(materialEntryPrefab, materialListParent);
                dynamicEntries.Add(entry);

                TMP_Text text = entry.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = $"{name}: ×{count}";
            }
        }
    }

    /// <summary>
    /// 左右のコスト表示TMPを「現在数/必要数」形式で更新
    /// </summary>
    private void UpdateCostDisplay()
    {
        if (currentRecipe == null)
        {
            // レシピ未選択時はクリア
            if (costTextLeft != null) costTextLeft.text = "";
            if (costTextRight != null) costTextRight.text = "";
            return;
        }

        bool hasRequiredWeapon = currentRecipe.requiredWeapon != null;
        var costs = currentRecipe.costs;
        bool hasCosts = costs != null && costs.Length > 0;

        if (hasRequiredWeapon)
        {
            // 前提条件あり：左に武器所持状態を表示
            if (costTextLeft != null)
            {
                bool owns = StatusManager.Instance != null
                    && StatusManager.Instance.ownedWeapons.Contains(currentRecipe.requiredWeapon);
                costTextLeft.text = owns ? "1/1" : "0/1";
                costTextLeft.color = owns ? enoughColor : notEnoughColor;
            }

            // 右にコスト表示（あれば）
            if (costTextRight != null)
            {
                if (hasCosts)
                    SetCostText(costTextRight, costs[0]);
                else
                    costTextRight.text = "";
            }
        }
        else
        {
            // 前提条件なし：従来通り左右にコスト表示
            if (!hasCosts)
            {
                if (costTextLeft != null) costTextLeft.text = "";
                if (costTextRight != null) costTextRight.text = "";
                return;
            }

            // 左テキスト：1番目のコスト
            if (costTextLeft != null)
            {
                SetCostText(costTextLeft, costs[0]);
            }

            // 右テキスト：2番目のコストがあればそれ、なければ1番目と同じ
            if (costTextRight != null)
            {
                if (costs.Length >= 2)
                    SetCostText(costTextRight, costs[1]);
                else
                    SetCostText(costTextRight, costs[0]);
            }
        }
    }

    /// <summary>
    /// 個別のコストテキストを設定
    /// </summary>
    private void SetCostText(TMP_Text textComp, CraftCost cost)
    {
        int current = MaterialManager.Instance.GetMaterial(cost.type);
        int required = cost.amount;
        textComp.text = $"{current}/{required}";
        textComp.color = (current >= required) ? enoughColor : notEnoughColor;
    }
}
