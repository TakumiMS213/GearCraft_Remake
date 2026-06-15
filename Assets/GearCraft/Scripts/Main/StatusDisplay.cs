using UnityEngine;
using TMPro;

public class StatusDisplay : MonoBehaviour
{
    [Header("ステータス値")]
    [Range(0, 100)] public int hp = 100;
    [Range(0, 10)] public int gate = 100;
    public int str = 10;
    public int acc = 10;
    public int gear = 0;

    [Header("TextMeshPro参照")]
    public TMP_Text hpText;
    public TMP_Text gateText;
    public TMP_Text strText;
    public TMP_Text accText;
    public TMP_Text gearText;

    private const int HP_MAX = 100;
    private const int GATE_MAX = 100;
    public StatusManager statusManager;
    private MaterialManager materialManager;
    private bool hasDisplaySnapshot;
    private int displayedHp;
    private float displayedGate;
    private int displayedStr;
    private int displayedAcc;
    private int displayedGear;

    void Start()
    {
        ResolveManagers();
        UpdateDisplay();
    }

    void Update()
    {
        ResolveManagers();
        if (statusManager == null) { return; }

        int gearCount = GetGearCount();
        if (!hasDisplaySnapshot ||
            displayedHp != statusManager.HP ||
            !Mathf.Approximately(displayedGate, statusManager.GATE) ||
            displayedStr != statusManager.STR ||
            displayedAcc != statusManager.ACC ||
            displayedGear != gearCount)
        {
            UpdateDisplay();
        }
    }

    /// <summary>
    /// ステータスの描画を更新する
    /// </summary>
    public void UpdateDisplay()
    {
        ResolveManagers();
        if (statusManager == null) { return; }

        // --- HP ---
        int hpBars = Mathf.Clamp(Mathf.RoundToInt(statusManager.HP / (HP_MAX / 10f)), 0, 10);
        Color hpColor = GetGaugeColor(statusManager.HP / (float)HP_MAX);
        SetText(hpText,  "HP : " + BuildGaugeText(hpBars, 10, hpColor, Color.white));

        // --- GATE ---
        int gateBars = Mathf.Clamp(Mathf.RoundToInt(statusManager.GATE / (GATE_MAX / 10f)), 0, 10);
        Color gateColor = GetGaugeColor(statusManager.GATE / (float)GATE_MAX);
        SetText(gateText, "GATE : " + BuildGaugeText(gateBars, 10, gateColor, Color.white));

        // --- STR / ACC ---
        SetText(strText, $"STR : {statusManager.STR}");
        SetText(accText, $"ACC : {statusManager.ACC}");

        int gearCount = GetGearCount();
        SetText(gearText, $"× {gearCount}");

        hasDisplaySnapshot = true;
        displayedHp = statusManager.HP;
        displayedGate = statusManager.GATE;
        displayedStr = statusManager.STR;
        displayedAcc = statusManager.ACC;
        displayedGear = gearCount;
    }

    private void ResolveManagers()
    {
        if (statusManager == null)
        {
            statusManager = StatusManager.Instance ?? FindAnyObjectByType<StatusManager>();
        }

        if (materialManager == null)
        {
            materialManager = MaterialManager.Instance ?? FindAnyObjectByType<MaterialManager>();
        }
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target == null) { return; }
        target.text = value;
    }

    private int GetGearCount()
    {
        return materialManager != null
            ? materialManager.GetMaterial(MaterialManager.MaterialType.Gear)
            : 0;
    }

    /// <summary>
    /// 「IIIIIIIIII」形式のゲージ文字列を作る
    /// </summary>
    private string BuildGaugeText(int filled, int max, Color filledColor, Color emptyColor)
    {
        string result = "";
        for (int i = 0; i < max; i++)
        {
            if (i < filled)
                result += $"<color=#{ColorUtility.ToHtmlStringRGB(filledColor)}>I</color>";
            else
                result += $"<color=#{ColorUtility.ToHtmlStringRGB(emptyColor)}>I</color>";
        }
        return result;
    }

    /// <summary>
    /// 残量に応じたゲージ色を返す
    /// </summary>
    private Color GetGaugeColor(float ratio)
    {
        if (ratio <= 0.3f)
            return Color.red;       // 3割以下 → 赤
        else if (ratio <= 0.5f)
            return Color.yellow;    // 5割以下 → 黄
        else
            return Color.green;     // 通常 → 緑
    }

    // --- 外部スクリプトから操作用のプロパティ ---
    public void SetHP(int value)
    {
        hp = Mathf.Clamp(value, 0, HP_MAX);
        UpdateDisplay();
    }

    public void SetGate(int value)
    {
        gate = Mathf.Clamp(value, 0, GATE_MAX);
        UpdateDisplay();
    }

    public void SetSTR(int value)
    {
        str = value;
        UpdateDisplay();
    }

    public void SetACC(int value)
    {
        acc = value;
        UpdateDisplay();
    }
}
