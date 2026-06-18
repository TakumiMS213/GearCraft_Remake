using UnityEngine;

[CreateAssetMenu(menuName = "GearCraft/Craft Result Description", fileName = "NewCraftResultDescription")]
public class CraftResultDescriptionSO : ScriptableObject
{
    [Header("Display")]
    public string displayName;

    [TextArea(2, 6)]
    public string description;

    public string BuildTooltipText()
    {
        string title = !string.IsNullOrWhiteSpace(displayName) ? displayName : name;
        string body = !string.IsNullOrWhiteSpace(description) ? description : "説明なし";
        return $"【{title}】\n{body}";
    }
}
