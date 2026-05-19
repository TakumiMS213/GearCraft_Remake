using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UpgradeEffectDisplay : MonoBehaviour
{
    [Header("UI")]
    public RectTransform effectListParent;
    public GameObject effectEntryPrefab;

    [Header("Colors")]
    public Color positiveColor = new Color(0.5f, 1f, 0.5f, 1f);
    public Color negativeColor = new Color(1f, 0.5f, 0.5f, 1f);
    public Color neutralColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    private readonly List<GameObject> entries = new List<GameObject>();

    public void Refresh()
    {
        ClearEntries();

        if (effectListParent == null)
        {
            return;
        }

        if (StatusManager.Instance == null)
        {
            AddEntry("No active upgrade effects.", neutralColor);
            return;
        }

        StatusManager status = StatusManager.Instance;

        if (status.bonusDamage != 0f)
        {
            AddEntry($"Damage +{status.bonusDamage:F0}", positiveColor);
        }

        if (status.attackSpeedMult != 1f)
        {
            float percent = (1f - status.attackSpeedMult) * 100f;
            AddEntry(percent > 0f ? $"Attack speed +{percent:F0}%" : $"Attack speed {percent:F0}%", percent > 0f ? positiveColor : negativeColor);
        }

        if (status.hasBulletDouble)
        {
            AddEntry("Bullet double ON", positiveColor);
        }

        if (status.spreadModifier != 0f)
        {
            AddEntry(status.spreadModifier < 0f ? $"Spread {status.spreadModifier:F1}" : $"Spread +{status.spreadModifier:F1}", status.spreadModifier < 0f ? positiveColor : negativeColor);
        }

        if (status.durabilityDrainChance > 0f)
        {
            AddEntry($"Durability drain {status.durabilityDrainChance * 100f:F0}%", positiveColor);
        }

        if (status.ricochetCount > 0)
        {
            AddEntry($"Ricochet {status.ricochetCount}", positiveColor);
        }

        if (status.junkCollectorMult != 1f)
        {
            AddEntry($"Material drop x{status.junkCollectorMult:F1}", positiveColor);
        }

        if (status.bulletSizeMult != 1f)
        {
            AddEntry($"Bullet size x{status.bulletSizeMult:F1}", positiveColor);
        }

        if (status.magnetRange > 3f)
        {
            AddEntry($"Magnet range +{status.magnetRange - 3f:F1}", positiveColor);
        }

        if (entries.Count == 0)
        {
            AddEntry("No active upgrade effects.", neutralColor);
        }
    }

    private void ClearEntries()
    {
        if (effectListParent != null)
        {
            for (int i = effectListParent.childCount - 1; i >= 0; i--)
            {
                DestroyEntry(effectListParent.GetChild(i).gameObject);
            }
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
            {
                DestroyEntry(entries[i]);
            }
        }

        entries.Clear();
    }

    private void DestroyEntry(GameObject entry)
    {
        if (entry == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(entry);
        }
        else
        {
            DestroyImmediate(entry);
        }
    }

    private void AddEntry(string text, Color color)
    {
        if (effectEntryPrefab == null || effectListParent == null)
        {
            return;
        }

        GameObject entry = Instantiate(effectEntryPrefab, effectListParent);
        entries.Add(entry);

        TMP_Text textComponent = entry.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = text;
            textComponent.color = color;
        }
    }
}
