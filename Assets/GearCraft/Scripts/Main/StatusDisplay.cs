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

    void Start()
    {
    statusManager = StatusManager.Instance ?? FindAnyObjectByType<StatusManager>();
        UpdateDisplay();
    }

    void Update()
    {
        // デモ用。実際は値が変わったときにのみ呼ぶのが理想
        UpdateDisplay();
    }

    /// <summary>
    /// ステータスの描画を更新する
    /// </summary>
    public void UpdateDisplay()
    {
        if(statusManager == null){ return; }
        // --- HP ---
        int hpBars = Mathf.Clamp(Mathf.RoundToInt(statusManager.HP / (HP_MAX / 10f)), 0, 10);
        Color hpColor = GetGaugeColor(statusManager.HP / HP_MAX);
        hpText.text = "    HP : " + BuildGaugeText(hpBars, 10, hpColor, Color.white);

        // --- GATE ---
        int gateBars = Mathf.Clamp(Mathf.RoundToInt(statusManager.GATE / (GATE_MAX / 10f)), 0, 10);
        Color gateColor = GetGaugeColor(statusManager.GATE / (float)GATE_MAX);
        gateText.text = "GATE : " + BuildGaugeText(gateBars, 10, gateColor, Color.white);

        // --- STR / ACC ---
        strText.text = $"STR : {statusManager.STR}";
        accText.text = $"ACC : {statusManager.ACC}";
        gearText.text = $"× {MaterialManager.Instance.GetMaterial(MaterialManager.MaterialType.Gear)}";
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
