using UnityEngine;

[System.Serializable]
public class MaterialDropEntry
{
    public MaterialManager.MaterialType type;
    public int minAmount = 1;
    public int maxAmount = 3;
    [Range(0f, 1f)] public float dropChance = 0.5f;
}
