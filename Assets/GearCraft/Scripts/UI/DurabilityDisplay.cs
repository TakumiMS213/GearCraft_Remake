using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD上に現在の武器の耐久値バーを表示する
/// </summary>
public class DurabilityDisplay : MonoBehaviour
{
    [Header("UI参照")]
    public Image durabilityBar;          // スライダー的なFilled Image
    public TMP_Text durabilityText;      // 数値テキスト（例："8/10"）
    public TMP_Text weaponNameText;      // 武器名表示
    public Image weaponIcon;             // 武器アイコン

    [Header("色設定")]
    public Color fullColor = Color.green;
    public Color midColor = Color.yellow;
    public Color lowColor = Color.red;
    public float lowThreshold = 0.3f;
    public float midThreshold = 0.6f;

    private StatusManager status;

    void Start()
    {
        status = StatusManager.Instance;
    }

    void Update()
    {
        if (status == null || status.currentWeapon == null) return;

        // デフォルト武器（刀）は耐久表示しない
        if (status.currentWeapon.isDefault)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        int current = status.GetCurrentDurability();
        int max = status.GetCurrentMaxDurability();

        // 武器名
        if (weaponNameText != null)
            weaponNameText.text = status.currentWeapon.weaponName;

        // アイコン
        if (weaponIcon != null && status.currentWeapon.icon != null)
            weaponIcon.sprite = status.currentWeapon.icon;

        // 数値
        if (durabilityText != null)
            durabilityText.text = $"{current}/{max}";

        // バー
        if (durabilityBar != null && max > 0)
        {
            float ratio = (float)current / max;
            durabilityBar.fillAmount = ratio;

            // 色
            if (ratio <= lowThreshold)
                durabilityBar.color = lowColor;
            else if (ratio <= midThreshold)
                durabilityBar.color = midColor;
            else
                durabilityBar.color = fullColor;
        }
    }

    private void SetVisible(bool visible)
    {
        if (durabilityBar != null) durabilityBar.gameObject.SetActive(visible);
        if (durabilityText != null) durabilityText.gameObject.SetActive(visible);
        if (weaponNameText != null) weaponNameText.gameObject.SetActive(visible);
        if (weaponIcon != null) weaponIcon.gameObject.SetActive(visible);
    }
}
