using UnityEngine;

public static class WeaponRarityColors
{
    public static Color Get(WeaponRarity rarity)
    {
        switch (rarity)
        {
            case WeaponRarity.Common:
                return PartRarityColors.Get(PartRarity.Common);
            case WeaponRarity.Uncommon:
                return PartRarityColors.Get(PartRarity.Uncommon);
            case WeaponRarity.Rare:
                return PartRarityColors.Get(PartRarity.Rare);
            case WeaponRarity.Epic:
                return PartRarityColors.Get(PartRarity.Epic);
            default:
                return Color.white;
        }
    }
}
