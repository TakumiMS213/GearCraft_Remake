using UnityEngine;

public static class PartRarityColors
{
    public static Color Get(PartRarity rarity)
    {
        switch (rarity)
        {
            case PartRarity.Common:
                return Color.white;
            case PartRarity.Uncommon:
                return new Color(0.45f, 1f, 0.45f, 1f);
            case PartRarity.Rare:
                return new Color(0.35f, 0.75f, 1f, 1f);
            case PartRarity.Epic:
                return new Color(0.95f, 0.45f, 1f, 1f);
            default:
                return Color.white;
        }
    }
}
