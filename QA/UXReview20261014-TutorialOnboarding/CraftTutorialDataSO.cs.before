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
                "防衛を支える備え",
                "「ScrapModule」があれば、毎日素材を確保できます。今の手持ちで作れるか、確かめておきましょう。",
                "Recipe_ScrapModule|Material_lv1_drop|Material_Gear",
                CraftTutorialWaitCondition.AssessCraftMaterials,
                learningGoal: "作りたいものを選び、所持数と必要数から素材の不足を判断できる。",
                hint1: "手持ちの数が、作るために使う数を満たしているかが重要です。",
                hint2: "この装置のレシピで、必要な素材と手持ちを見比べられます。",
                hint3: "この装置を選んで、素材を確認してください。全てそろっていますか？",
                areaTargetName: "CraftRecipeScrollView|Material_lv1_drop|Material_Gear"),
            new CraftTutorialStepData(
                "素材を安定して確保する",
                "この装置を仕上げて、明日からの素材集めに役立てましょう。",
                "Recipe_ScrapModule|craft_button",
                CraftTutorialWaitCondition.CraftScrapModule,
                learningGoal: "必要な素材を消費して機能を作成し、得られた効果を確認できる。",
                hint1: "作成には材料を使いますが、完成すれば毎日の収入につながります。",
                hint2: "作る装置と、使う材料に間違いがないか見ておきましょう。",
                hint3: "この装置を選び、ハンマーを押せば作成できます。",
                areaTargetName: "Result_ScrapModule|Material_lv1_drop|Material_Gear"),
            new CraftTutorialStepData(
                "次の戦いに備える",
                "次に何を備えるかは、あなた次第です。まだ持っていないものから、戦いに役立つものを選んで作りましょう。",
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
                "敵を倒すのに手数がかかるなら、一撃の威力を高めましょう。攻撃力を上げるパーツを選んで、武器に組み込んでください。",
                "ShopItem_Damage+1|GridArea",
                CraftTutorialWaitCondition.PlaceDamagePart,
                learningGoal: "目的に合う効果のパーツを選び、形に合う空きへ装着して武器を強化できる。",
                hint1: "「ダメージ＋」の効果があれば、一撃の威力を高められます。",
                hint2: "候補のパーツは、効果と必要な材料を見比べて選びましょう。",
                hint3: "Damage Plateを、形が収まる空きマスへドラッグしてください。",
                areaTargetName: "ShopArea|EffectSummary"),
            new CraftTutorialStepData(
                "戦い方に合わせて改良する",
                "威力を伸ばすか、攻撃の間隔を縮めるか。先ほどとは違うパーツで、戦い方に合う強化を加えましょう。",
                "ShopArea|GridArea",
                CraftTutorialWaitCondition.ImproveWeapon,
                learningGoal: "効果・必要素材・形を比較し、別のパーツで狙った性能をさらに高められる。",
                hint1: "効果だけでなく、手持ちの材料と組み込める形かどうかも判断の材料です。",
                hint2: "今の武器の性能と見比べれば、次に伸ばしたい部分が見えてきます。",
                hint3: "選んだパーツを空きマスへドラッグして、性能の変化を確かめましょう。",
                areaTargetName: "ShopArea|EffectSummary",
                independent: true)
        };
    }
}
