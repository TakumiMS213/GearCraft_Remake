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

        if (status.damageBonusPercent != 0f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatPercentDelta("基礎ダメージ", status.damageBonusPercent), status.damageBonusPercent < 0f);
        }

        if (status.attackSpeedMult != 1f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatAttackSpeedMultiplier(status.attackSpeedMult), status.attackSpeedMult > 1f);
        }

        if (status.hasBulletDouble)
        {
            AddSignedEntry("弾数+1", false);
        }

        if (status.spreadModifier != 0f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatSpreadModifier(status.spreadModifier), status.spreadModifier > 0f);
        }

        if (status.ricochetCount > 0)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatMagnitudeDelta("跳弾", status.ricochetCount), false);
        }

        if (status.junkCollectorMult != 1f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatMultiplierDelta("素材獲得数", status.junkCollectorMult), status.junkCollectorMult < 1f);
        }

        if (status.bulletSizeMult != 1f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatMultiplierDelta("弾サイズ", status.bulletSizeMult), status.bulletSizeMult < 1f);
        }

        if (status.magnetRange > 3f)
        {
            AddSignedEntry(UpgradeEffectTextFormatter.FormatMagnitudeDelta("回収範囲", status.magnetRange - 3f), false);
        }

        var melee = status.meleeUpgrades;
        if (status.HasMeleeEquipped && melee != null)
        {
            if (melee.knockbackDistance > 0f) AddSignedEntry($"命中時ノックバック {Mathf.Min(melee.knockbackDistance, 1.5f):0.##}", false);
            if (melee.SwingMultiplier < 1f) AddSignedEntry($"振り時間{(1f - melee.SwingMultiplier) * 100f:0}%短縮", false);
            if (melee.SizeMultiplier > 1f) AddSignedEntry($"武器サイズ・攻撃範囲+{(melee.SizeMultiplier - 1f) * 100f:0}%", false);
            if (melee.explosionDamageRatio > 0f) AddSignedEntry("近接命中時に爆発", false);
            if (melee.punkDriveKillBonus > 0f) AddSignedEntry($"近接撃破後5秒、PunkDrive火力+{melee.punkDriveKillBonus * 100f:0}%", false);
            if (melee.killDropBonus > 0f) AddSignedEntry($"近接撃破時の素材獲得数+{melee.killDropBonus * 100f:0}%", false);
            if (melee.invincibilityBonus > 0f) AddSignedEntry($"被弾後の無敵時間+{(melee.InvincibilityDurationMultiplier - 1f) * 100f:0}%", false);
            if (melee.extraJumps > 0) AddSignedEntry($"空中ジャンプ+{melee.extraJumps}回", false);
            if (melee.moveSpeedBonus > 0f) AddSignedEntry($"移動速度+{melee.moveSpeedBonus * 100f:0}%", false);
            if (melee.bossDamageBonus > 0f) AddSignedEntry($"ボスへの近接ダメージ{1f + melee.bossDamageBonus:0.##}倍", false);
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
        AddEntry($"{(negative ? "-" : "+")} {text}", negative ? negativeColor : positiveColor);
    }
}
