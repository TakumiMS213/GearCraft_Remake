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
                "毎日の歯車を確保する",
                "ScrapModule は、毎日使う歯車を確保できる装置です。",
                "Recipe_ScrapModule|Material_lv1_drop|Material_Gear",
                CraftTutorialWaitCondition.AssessCraftMaterials,
                learningGoal: "作りたいものを選び、所持数と必要数から素材の不足を判断できる。",
                hint1: "必要な数以上に持っている素材は使えます。足りないものがあれば、まだ作成できません。",
                hint2: "レシピを選ぶと、使う素材と必要数が表示されます。",
                hint3: "ScrapModule を選び、素材の下の数を見比べましょう。",
                areaTargetName: "CraftRecipeScrollView"),
            new CraftTutorialStepData(
                "装置を仕上げる",
                "次は、この装置を完成させましょう。明日からの素材集めに役立ちます。",
                "Recipe_ScrapModule|craft_button",
                CraftTutorialWaitCondition.CraftScrapModule,
                learningGoal: "必要な素材を消費して機能を作成し、得られた効果を確認できる。",
                hint1: "作成すると材料を使い、装置の効果が使えるようになります。",
                hint2: "材料がそろったレシピは、中央のハンマーから作成できます。",
                hint3: "ScrapModule を選び、中央のハンマーを押しましょう。",
                areaTargetName: "craft_button"),
            new CraftTutorialStepData(
                "戦い方に合う備えを作る",
                "欲しい効果と手持ちの材料をもとに、次の備えを選びましょう。",
                "craft_button",
                CraftTutorialWaitCondition.CraftDifferentRecipe,
                learningGoal: "効果と必要素材を読み、別のレシピを自分で選んで作成できる。",
                hint1: "欲しい効果と、今ある素材で作れるか。その両方で選ぶとよいでしょう。",
                hint2: "他のレシピにも目を通して、戦い方に合う効果を探してみてください。",
                hint3: "選んだものの材料をそろえたら、ハンマーを押して仕上げましょう。",
                areaTargetName: "CraftRecipeScrollView",
                independent: true)
        };
    }

    public static CraftTutorialStepData[] CreateDefaultWeaponCustomSteps()
    {
        return new[]
        {
            new CraftTutorialStepData(
                "一撃の威力を高める",
                "効果は、パーツにカーソルを合わせると確認できます。",
                "ShopItem_Damage+1|GridArea",
                CraftTutorialWaitCondition.PlaceDamagePart,
                learningGoal: "目的に合う効果のパーツを選び、形に合う空きへ装着して武器を強化できる。",
                hint1: "「ダメージ＋」の効果で、一撃の威力を高められます。",
                hint2: "左のパーツを選び、形が収まる空きマスまでドラッグします。",
                hint3: "Damage Plate を空きマスへドラッグし、マウスを離しましょう。",
                areaTargetName: "ShopArea"),
            new CraftTutorialStepData(
                "戦い方に合わせて改良する",
                "威力を伸ばすか、攻撃を速めるか。装備中の効果と見比べて、次の改良を考えましょう。",
                "ShopArea|GridArea",
                CraftTutorialWaitCondition.ImproveWeapon,
                learningGoal: "効果・必要素材・形を比較し、別のパーツで狙った性能をさらに高められる。",
                hint1: "効果だけでなく、手持ちの材料と組み込める形かどうかも判断の材料です。",
                hint2: "右の効果とパーツの説明を見比べて、威力か攻撃の速さを選びましょう。",
                hint3: "選んだパーツを空きマスへドラッグして、性能の変化を確かめましょう。",
                areaTargetName: "ShopArea|EffectSummary",
                independent: true)
        };
    }
}
