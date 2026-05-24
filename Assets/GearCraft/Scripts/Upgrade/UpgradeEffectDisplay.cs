using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UpgradeEffectDisplay : MonoBehaviour
{
    [Header("UI")]
    public RectTransform effectListParent;
    public GameObject effectEntryPrefab;
    public float textSizeMultiplier = 1.5f;

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
            AddEntry("現在有効な強化効果はありません。", neutralColor);
            return;
        }

        StatusManager status = StatusManager.Instance;

        if (status.bonusDamage != 0f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatMagnitudeDelta("ダメージ", status.bonusDamage), status.bonusDamage < 0f);
        }

        if (status.attackSpeedMult != 1f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatAttackSpeedMultiplier(status.attackSpeedMult), status.attackSpeedMult > 1f);
        }

        if (status.hasBulletDouble)
        {
            AddSignedEntry("弾数100%増加", false);
        }

        if (status.spreadModifier != 0f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatSpreadModifier(status.spreadModifier), status.spreadModifier > 0f);
        }

        if (status.durabilityDrainChance > 0f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatPercentDelta("耐久吸収率", status.durabilityDrainChance), false);
        }

        if (status.ricochetCount > 0)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatMagnitudeDelta("跳弾", status.ricochetCount), false);
        }

        if (status.junkCollectorMult != 1f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatMultiplierDelta("素材ドロップ率", status.junkCollectorMult), status.junkCollectorMult < 1f);
        }

        if (status.bulletSizeMult != 1f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatMultiplierDelta("弾サイズ", status.bulletSizeMult), status.bulletSizeMult < 1f);
        }

        if (status.magnetRange > 3f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatMagnitudeDelta("回収範囲", status.magnetRange - 3f), false);
        }

        if (status.maxDurabilityBonus != 0)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatMagnitudeDelta("最大耐久", status.maxDurabilityBonus), status.maxDurabilityBonus < 0);
        }

        if (entries.Count == 0)
        {
            AddEntry("現在有効な強化効果はありません。", neutralColor);
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
            textComponent.fontSize *= Mathf.Max(0.1f, textSizeMultiplier);
        }
    }

    private void AddSignedEntry(string text, bool negative)
    {
        AddEntry($"{(negative ? "-" : "＋")} {text}", negative ? negativeColor : positiveColor);
    }
}
