using System;
using UnityEngine;

public enum CraftTutorialWaitCondition
{
    None,
    CraftScrapModule,
    PlaceDamagePart
}

[Serializable]
public sealed class CraftTutorialStepData
{
    [SerializeField] private string title;
    [SerializeField, TextArea(2, 5)] private string body;
    [SerializeField] private string targetName;
    [SerializeField] private CraftTutorialWaitCondition waitCondition;

    public string Title => title;
    public string Body => body;
    public string TargetName => targetName;
    public CraftTutorialWaitCondition WaitCondition => waitCondition;

    public CraftTutorialStepData(
        string title,
        string body,
        string targetName,
        CraftTutorialWaitCondition waitCondition = CraftTutorialWaitCondition.None)
    {
        this.title = title;
        this.body = body;
        this.targetName = targetName;
        this.waitCondition = waitCondition;
    }
}

[CreateAssetMenu(menuName = "GearCraft/Craft Tutorial Data", fileName = "CraftTutorialData")]
public sealed class CraftTutorialDataSO : ScriptableObject
{
    [SerializeField] private CraftTutorialStepData[] craftSteps;
    [SerializeField] private CraftTutorialStepData[] weaponCustomSteps;

    public CraftTutorialStepData[] CraftSteps => craftSteps;
    public CraftTutorialStepData[] WeaponCustomSteps => weaponCustomSteps;
}
