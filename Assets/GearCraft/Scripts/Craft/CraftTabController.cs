using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Craftシーンのタブ切替コントローラー。
/// クラフトタブ / 強化パーツタブの表示を管理する。
/// </summary>
public class CraftTabController : MonoBehaviour
{
    [Header("タブボタン")]
    public Button craftTabButton;       // クラフトタブボタン
    public Button upgradeTabButton;     // 強化パーツタブボタン

    [Header("パネル")]
    public GameObject craftPanel;       // 既存のクラフトUI全体の親
    public GameObject upgradePanel;     // 強化パーツUI全体の親

    [Header("タブ色設定")]
    public Color activeTabColor   = new Color(0.9f, 0.75f, 0.3f, 1f);   // 選択中（ゴールド系）
    public Color inactiveTabColor = new Color(0.3f, 0.3f, 0.35f, 1f);   // 非選択（ダークグレー）

    [Header("効果音")]
    public AudioSource tabSwitchSE;

    [Header("連携")]
    public MaterialDisplay materialDisplay;       // 素材表示の更新用
    public UpgradeShopManager upgradeShopManager; // ショップ初期化用

    private int currentTab = 0; // 0=Craft, 1=Upgrade

    void Start()
    {
        // ボタンイベント登録
        if (craftTabButton != null)
            craftTabButton.onClick.AddListener(() => SwitchTab(0));
        if (upgradeTabButton != null)
            upgradeTabButton.onClick.AddListener(() => SwitchTab(1));

        // 初期状態：クラフトタブを表示
        SwitchTab(0);
    }

    /// <summary>
    /// タブを切り替える
    /// </summary>
    /// <param name="tabIndex">0=クラフト, 1=強化パーツ</param>
    public void SwitchTab(int tabIndex)
    {
        currentTab = tabIndex;

        // パネル表示切替
        if (craftPanel != null)
            craftPanel.SetActive(tabIndex == 0);
        if (upgradePanel != null)
            upgradePanel.SetActive(tabIndex == 1);

        // タブボタンの色更新
        UpdateTabColors();

        // 効果音
        if (tabSwitchSE != null)
            tabSwitchSE.Play();

        // 強化パーツタブに切替時：ショップ初期化 & グリッド更新
        if (tabIndex == 1)
        {
            if (upgradeShopManager != null)
                upgradeShopManager.GenerateLineup();

            if (UpgradeGridManager.Instance != null)
                UpgradeGridManager.Instance.RefreshGridSize();
        }

        // 素材表示を更新
        if (materialDisplay != null)
            materialDisplay.UpdateMaterialAmount();
    }

    private void UpdateTabColors()
    {
        SetTabColor(craftTabButton,   currentTab == 0);
        SetTabColor(upgradeTabButton, currentTab == 1);
    }

    private void SetTabColor(Button btn, bool isActive)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        if (img != null)
            img.color = isActive ? activeTabColor : inactiveTabColor;

        // テキスト色も変更
        TMP_Text txt = btn.GetComponentInChildren<TMP_Text>();
        if (txt != null)
            txt.color = isActive ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
    }
}
