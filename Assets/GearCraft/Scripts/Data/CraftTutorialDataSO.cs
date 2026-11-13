using System;
using UnityEngine;

public enum CraftTutorialWaitCondition
{
    None = 0,
    CraftScrapModule = 1,
    PlaceDamagePart = 2,
    AssessCraftMaterials = 3,
    CraftDifferentRecipe = 4,
    ImproveWeapon = 5
}

[Serializable]
public sealed class CraftTutorialStepData
{
    [SerializeField] private string title;
    [SerializeField, TextArea(2, 5)] private string body;
    [SerializeField, TextArea(2, 3)] private string learningGoal;
    [SerializeField, TextArea(2, 3)] private string hint1;
    [SerializeField, TextArea(2, 3)] private string hint2;
    [SerializeField, TextArea(2, 3)] private string hint3;
    [SerializeField] private string areaTargetName;
    [SerializeField] private string targetName;
    [SerializeField] private CraftTutorialWaitCondition waitCondition;
    [SerializeField] private bool independent;

    public string Title => title;
    public string Body => body;
    public string LearningGoal => learningGoal;
    public string Hint1 => hint1;
    public string Hint2 => hint2;
    public string Hint3 => hint3;
    public string AreaTargetName => areaTargetName;
    public string TargetName => targetName;
    public CraftTutorialWaitCondition WaitCondition => waitCondition;
    public bool Independent => independent;

    public CraftTutorialStepData(
        string title,
        string body,
        string targetName,
        CraftTutorialWaitCondition waitCondition = CraftTutorialWaitCondition.None,
        string learningGoal = "",
        string hint1 = "",
        string hint2 = "",
        string hint3 = "",
        string areaTargetName = "",
        bool independent = false)
    {
        this.title = title;
        this.body = body;
        this.learningGoal = learningGoal;
        this.hint1 = hint1;
        this.hint2 = hint2;
        this.hint3 = hint3;
        this.areaTargetName = areaTargetName;
        this.targetName = targetName;
        this.waitCondition = waitCondition;
        this.independent = independent;
    }
}

[CreateAssetMenu(menuName = "GearCraft/Craft Tutorial Data", fileName = "CraftTutorialData")]
public sealed class CraftTutorialDataSO : ScriptableObject
{
    [SerializeField] private CraftTutorialStepData[] craftSteps;
    [SerializeField] private CraftTutorialStepData[] weaponCustomSteps;

    public CraftTutorialStepData[] CraftSteps => craftSteps;
    public CraftTutorialStepData[] WeaponCustomSteps => weaponCustomSteps;

    public static CraftTutorialStepData[] CreateDefaultCraftSteps()
    {
        return new[]
        {
            new CraftTutorialStepData(
                "ScrapModuleの材料を確認する",
                "ScrapModuleを作ると、1日が終わるごとに歯車を5個獲得します。",
                "Recipe_ScrapModule|Material_lv1_drop|Material_Gear",
                CraftTutorialWaitCondition.AssessCraftMaterials,
                learningGoal: "作りたいものを選び、所持数と必要数から素材の不足を判断できる。",
                hint1: "全ての素材で、所持数が必要数以上なら作成できます。",
                hint2: "レシピを選び、素材の下の「所持数 / 必要数」を比べましょう。",
                hint3: "ScrapModuleを選び、「足りている」か「足りない」で答えましょう。",
                areaTargetName: "CraftRecipeScrollView"),
            new CraftTutorialStepData(
                "ScrapModuleを作成する",
                "作成すると素材を消費します。完成後は1日ごとに歯車を5個獲得します。",
                "Recipe_ScrapModule|craft_button",
                CraftTutorialWaitCondition.CraftScrapModule,
                learningGoal: "必要な素材を消費して機能を作成し、得られた効果を確認できる。",
                hint1: "材料がそろったら、レシピを選んで作成できます。",
                hint2: "材料がそろったレシピは、中央のハンマーから作成できます。",
                hint3: "ScrapModule を選び、中央のハンマーを押しましょう。",
                areaTargetName: "craft_button"),
            new CraftTutorialStepData(
                "別のレシピを作成する",
                "効果と必要素材を比べ、未作成のレシピを1つ選びましょう。",
                "craft_button",
                CraftTutorialWaitCondition.CraftDifferentRecipe,
                learningGoal: "効果と必要素材を読み、別のレシピを自分で選んで作成できる。",
                hint1: "レシピの効果と必要素材を確認し、作成するものを選びましょう。",
                hint2: "各レシピにカーソルを合わせると、完成品の効果を確認できます。",
                hint3: "未作成のレシピを選び、材料をそろえてハンマーを押しましょう。",
                areaTargetName: "CraftRecipeScrollView",
                independent: true)
        };
    }

    public static CraftTutorialStepData[] CreateDefaultWeaponCustomSteps()
    {
        return new[]
        {
            new CraftTutorialStepData(
                "武器の攻撃力を上げる",
                "攻撃力補正のパーツを装着すると、基礎ダメージが割合で増えます。",
                "ShopItem_Damage+1|GridArea",
                CraftTutorialWaitCondition.PlaceDamagePart,
                learningGoal: "目的に合う効果のパーツを選び、形に合う空きへ装着して武器を強化できる。",
                hint1: "パーツにカーソルを合わせ、攻撃力補正の効果を探しましょう。",
                hint2: "左のパーツを選び、形が収まる空きマスまでドラッグします。",
                hint3: "Damage Plate を空きマスへドラッグし、マウスを離しましょう。",
                areaTargetName: "ShopArea"),
            new CraftTutorialStepData(
                "別のパーツで攻撃力か攻撃速度を上げる",
                "パーツの効果・必要素材・形を比べ、攻撃力か攻撃速度を上げましょう。",
                "ShopArea|GridArea",
                CraftTutorialWaitCondition.ImproveWeapon,
                learningGoal: "効果・必要素材・形を比較し、別のパーツで狙った性能をさらに高められる。",
                hint1: "現在の武器で使えるパーツから、必要素材と空きマスに合うものを選びましょう。",
                hint2: "パーツの説明と右の効果一覧を比べ、攻撃力か攻撃速度の上昇を確認しましょう。",
                hint3: "先ほどとは違うパーツを空きマスへドラッグして装着しましょう。",
                areaTargetName: "ShopArea|EffectSummary",
                independent: true)
        };
    }
}
