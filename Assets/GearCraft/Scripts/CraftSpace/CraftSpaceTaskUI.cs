using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>宿舎の左上に、読み取り専用のチェックボックスとタスク内容を表示する。</summary>
[RequireComponent(typeof(Canvas))]
public sealed class CraftSpaceTaskUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private StageGeneratorSO stageConfig;
    [SerializeField] private Transform modalLayerStart;

    [Header("Tasks")]
    [SerializeField] private string sleepTask = "ベッドで眠って明日に備える";
    [SerializeField] private string defenseTask = "門の防衛を続ける";
    [SerializeField] private string craftTask = "作業台で新しい装備を作る";
    [SerializeField] private string upgradeTask = "作業台で武器の性能を高める";

    [Header("Layout")]
    [SerializeField, Min(220f)] private float panelWidth = 670f;
    [SerializeField, Min(16f)] private float fontSize = 28f;

    [Header("First Bed Guide")]
    [SerializeField] private Sprite bedGuideSprite;
    [SerializeField] private GameObject bedInteractionPrompt;
    [SerializeField] private Vector2 bedGuideSize = new Vector2(51f, 96f);
    [SerializeField, Min(0f)] private float bedGuideLeftInset = 60f;
    [SerializeField, Min(0f)] private float bedGuideSwayDistance = 14f;
    [SerializeField, Min(0.1f)] private float bedGuideSwayPeriod = 1.4f;

    private const string DormSceneName = "CraftSpace";
    private const float Padding = 16f;
    private const float CheckboxSize = 26f;
    private const float CheckboxGap = 14f;
    private const float RowGap = 10f;
    private static readonly Color PendingColor = new Color(0.95f, 0.93f, 0.87f, 1f);
    private static readonly Color CompletedColor = new Color(0.65f, 0.8f, 0.69f, 1f);
    private static readonly Color BorderColor = new Color(0.74f, 0.68f, 0.5f, 1f);

    private sealed class TaskRow
    {
        public RectTransform Rect;
        public TMP_Text Label;
        public GameObject Check;
    }

    private readonly TaskRow[] rows = new TaskRow[3];
    private readonly DormTaskProgress fallbackProgress = new DormTaskProgress();
    private Canvas canvas;
    private RectTransform panel;
    private RectTransform bedGuideArrow;
    private float bedGuideSwayTime;
    private int lastState = -1;
    private float lastWidth = -1f;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        CreatePanel();
        CreateBedGuide();
        Refresh(true);
    }

    private void OnEnable()
    {
        lastState = -1;
    }

    private void LateUpdate()
    {
        Refresh(false);
    }

    private void OnDisable()
    {
        if (panel != null) panel.gameObject.SetActive(false);
        if (bedGuideArrow != null) bedGuideArrow.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel.gameObject);
        if (bedGuideArrow != null) Destroy(bedGuideArrow.gameObject);
    }

    private void Refresh(bool force)
    {
        if (panel == null) return;
        if (gameObject.scene.name != DormSceneName || SceneManager.GetActiveScene().name != DormSceneName)
        {
            panel.gameObject.SetActive(false);
            if (bedGuideArrow != null) bedGuideArrow.gameObject.SetActive(false);
            lastState = -1;
            return;
        }
        StatusManager status = StatusManager.Instance;
        DormTaskProgress progress = status != null ? status.DormTasks : fallbackProgress;
        int stage = StageCounter.Instance != null ? StageCounter.Instance.StageCount : 1;
        int bossKills = status != null ? status.bossKillCount : 0;
        int firstBossStage = stageConfig != null && stageConfig.bossStages != null && stageConfig.bossStages.Length > 0
            ? Mathf.Max(1, stageConfig.bossStages[0]) : 9;
        DormTaskPhase phase = progress.GetPhase(stage, bossKills, firstBossStage);
        RefreshBedGuide(phase);
        int state = (int)phase | (progress.hasCrafted ? 4 : 0) | (progress.hasUpgradedWeapon ? 8 : 0);

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float canvasWidth = canvasRect.rect.width > 0f ? canvasRect.rect.width : Screen.width / Mathf.Max(0.01f, canvas.scaleFactor);
        float width = Mathf.Min(panelWidth, Mathf.Max(220f, canvasWidth));
        if (!force && state == lastState && Mathf.Abs(width - lastWidth) < 0.5f) return;
        lastState = state;
        lastWidth = width;
        panel.gameObject.SetActive(true);
        panel.anchoredPosition = Vector2.zero;

        int count = phase == DormTaskPhase.FirstBossBuild ? 3 : 1;
        float height = Padding;
        for (int i = 0; i < rows.Length; i++)
        {
            TaskRow row = rows[i];
            row.Rect.gameObject.SetActive(i < count);
            if (i >= count) continue;

            string content = phase == DormTaskPhase.Preparation ? sleepTask
                : i == 0 ? defenseTask : i == 1 ? craftTask : upgradeTask;
            if (phase == DormTaskPhase.Preparation)
                content += "\nA / D：移動　ベッドのそばで Space";
            // 防衛は継続目標なので、宿舎に戻った後も未完了のまま次の日へ案内する。
            bool complete = phase == DormTaskPhase.FirstBossBuild &&
                (i == 1 ? progress.hasCrafted : i == 2 && progress.hasUpgradedWeapon);
            row.Label.text = content;
            row.Label.color = complete ? CompletedColor : PendingColor;
            row.Check.SetActive(complete);
            float textWidth = width - Padding * 2f - CheckboxSize - CheckboxGap;
            float rowHeight = Mathf.Max(44f, row.Label.GetPreferredValues(content, textWidth, 0f).y + 8f);
            row.Rect.anchoredPosition = new Vector2(Padding, -height);
            row.Rect.sizeDelta = new Vector2(width - Padding * 2f, rowHeight);
            height += rowHeight + (i + 1 < count ? RowGap : 0f);
        }
        panel.sizeDelta = new Vector2(width, height + Padding);
    }

    private void CreateBedGuide()
    {
        if (bedGuideSprite == null) return;

        bedGuideArrow = CreateRect("FirstBedGuideArrow", transform);
        bedGuideArrow.anchorMin = bedGuideArrow.anchorMax = new Vector2(0f, 0.5f);
        bedGuideArrow.pivot = new Vector2(0.5f, 0.5f);
        bedGuideArrow.sizeDelta = bedGuideSize;
        // The existing arrow sprite points right; turn it toward the bed on the left.
        bedGuideArrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
        if (modalLayerStart != null && modalLayerStart.parent == transform)
            bedGuideArrow.SetSiblingIndex(modalLayerStart.GetSiblingIndex());

        Image arrow = bedGuideArrow.gameObject.AddComponent<Image>();
        arrow.sprite = bedGuideSprite;
        arrow.preserveAspect = true;
        arrow.raycastTarget = false;
        bedGuideArrow.gameObject.SetActive(false);
    }

    private void RefreshBedGuide(DormTaskPhase phase)
    {
        if (bedGuideArrow == null) return;

        bool visible = phase == DormTaskPhase.Preparation
            && !ArrivalMessagePresenter.IsMessagePlaying
            && !ArrivalMessagePresenter.IsCameraPresentationPlaying
            && Time.timeScale > 0f
            && (bedInteractionPrompt == null || !bedInteractionPrompt.activeInHierarchy);
        if (bedGuideArrow.gameObject.activeSelf != visible)
        {
            bedGuideArrow.gameObject.SetActive(visible);
            bedGuideSwayTime = 0f;
        }
        if (!visible) return;

        float sway = Mathf.Sin(bedGuideSwayTime * Mathf.PI * 2f / Mathf.Max(0.1f, bedGuideSwayPeriod)) * bedGuideSwayDistance;
        // Keep the arrow inside the screen even when its size or sway is adjusted.
        float inset = Mathf.Max(bedGuideLeftInset, bedGuideSize.x * 0.5f + bedGuideSwayDistance + 8f);
        bedGuideArrow.anchoredPosition = new Vector2(inset + sway, 0f);
        bedGuideSwayTime += Time.unscaledDeltaTime;
    }

    private void CreatePanel()
    {
        panel = CreateRect("DormTaskPanel", transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        // 操作案内より前、オプション・会話・ロード画面より後ろに表示する。
        if (modalLayerStart != null && modalLayerStart.parent == transform)
            panel.SetSiblingIndex(modalLayerStart.GetSiblingIndex());
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.055f, 0.065f, 0.085f, 0.8f);
        background.raycastTarget = false;
        for (int i = 0; i < rows.Length; i++) rows[i] = CreateRow(i);
    }

    private TaskRow CreateRow(int index)
    {
        RectTransform rect = CreateRect("TaskRow_" + index, panel);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);

        RectTransform checkbox = CreateRect("Checkbox", rect);
        checkbox.anchorMin = checkbox.anchorMax = new Vector2(0f, 0.5f);
        checkbox.pivot = new Vector2(0f, 0.5f);
        checkbox.sizeDelta = Vector2.one * CheckboxSize;
        CreateBar("Top", checkbox, new Vector2(0f, 12f), new Vector2(26f, 2f), 0f, BorderColor);
        CreateBar("Bottom", checkbox, new Vector2(0f, -12f), new Vector2(26f, 2f), 0f, BorderColor);
        CreateBar("Left", checkbox, new Vector2(-12f, 0f), new Vector2(2f, 26f), 0f, BorderColor);
        CreateBar("Right", checkbox, new Vector2(12f, 0f), new Vector2(2f, 26f), 0f, BorderColor);
        RectTransform check = CreateRect("Checkmark", checkbox);
        check.anchorMin = check.anchorMax = new Vector2(0.5f, 0.5f);
        check.sizeDelta = Vector2.one * CheckboxSize;
        CreateBar("ShortStroke", check, new Vector2(-4f, -1f), new Vector2(8f, 3f), -45f, CompletedColor);
        CreateBar("LongStroke", check, new Vector2(3f, 1f), new Vector2(17f, 3f), 45f, CompletedColor);

        RectTransform labelRect = CreateRect("TaskContent", rect);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(CheckboxSize + CheckboxGap, 4f);
        labelRect.offsetMax = new Vector2(0f, -4f);
        TMP_Text label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = false;
        label.raycastTarget = false;
        return new TaskRow { Rect = rect, Label = label, Check = check.gameObject };
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void CreateBar(string name, RectTransform parent, Vector2 position, Vector2 size, float angle, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
