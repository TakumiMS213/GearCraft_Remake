using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainDebugEnemySpawnWindow : MonoBehaviour
{
    private const string MainSceneName = "Main";
    private const float PanelWidth = 720f;
    private const float PanelHeight = 760f;
    private const float RowHeight = 64f;

    private static MainDebugEnemySpawnWindow instance;

    private Canvas canvas;
    private GameObject panel;
    private RectTransform contentRoot;
    private EnemySpawner spawner;
    private bool comboLatch;

    public static bool IsDebugModeActive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeRuntime()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureForActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForActiveScene();
    }

    private static void EnsureForActiveScene()
    {
        if (SceneManager.GetActiveScene().name != MainSceneName)
        {
            IsDebugModeActive = false;
            if (instance != null)
            {
                Destroy(instance.gameObject);
                instance = null;
            }

            return;
        }

        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject("MainDebugEnemySpawnWindow");
        instance = root.AddComponent<MainDebugEnemySpawnWindow>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        CreateWindow();
        SetWindowVisible(false);
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != MainSceneName)
        {
            return;
        }

        bool comboPressed =
            Input.GetKey(KeyCode.G) &&
            Input.GetKey(KeyCode.E) &&
            Input.GetKey(KeyCode.A) &&
            Input.GetKey(KeyCode.R);

        if (comboPressed && !comboLatch)
        {
            ToggleWindow();
        }

        comboLatch = comboPressed;
    }

    private void ToggleWindow()
    {
        SetWindowVisible(panel == null || !panel.activeSelf);
    }

    private void SetWindowVisible(bool visible)
    {
        if (panel != null)
        {
            panel.SetActive(visible);
        }

        IsDebugModeActive = visible;
    }

    private void CreateWindow()
    {
        canvas = new GameObject("DebugEnemySpawnCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        panel = new GameObject("EnemySpawnPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.86f);
        panelImage.raycastTarget = true;

        CreateText("Title", panelRect, "Enemy Debug Spawn", 38f, TextAlignmentOptions.Center, new Vector2(0f, 320f), new Vector2(620f, 64f));
        Button closeButton = CreateButton("CloseButton", panelRect, "CLOSE", new Vector2(260f, 320f), new Vector2(150f, 52f));
        closeButton.onClick.AddListener(() => SetWindowVisible(false));

        ScrollRect scrollRect = CreateScrollArea(panelRect);
        contentRoot = scrollRect.content;
        RebuildEnemyRows();
    }

    private ScrollRect CreateScrollArea(RectTransform parent)
    {
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(parent, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(0f, -40f);
        viewportRect.sizeDelta = new Vector2(620f, 590f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.04f);
        viewportImage.raycastTarget = true;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject scrollObject = new GameObject("ScrollRect", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = viewportRect.anchorMin;
        scrollRectTransform.anchorMax = viewportRect.anchorMax;
        scrollRectTransform.pivot = viewportRect.pivot;
        scrollRectTransform.anchoredPosition = viewportRect.anchoredPosition;
        scrollRectTransform.sizeDelta = viewportRect.sizeDelta;

        viewport.transform.SetParent(scrollObject.transform, false);

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 36f;
        return scrollRect;
    }

    private void RebuildEnemyRows()
    {
        if (contentRoot == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }

        List<EnemyDataSO> enemies = CollectEnemies();
        for (int i = 0; i < enemies.Count; i++)
        {
            CreateEnemyRow(enemies[i]);
        }
    }

    private List<EnemyDataSO> CollectEnemies()
    {
        List<EnemyDataSO> enemies = new List<EnemyDataSO>();
        spawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        StageGeneratorSO config = spawner != null ? spawner.stageConfig : null;

        AddEnemies(enemies, config != null ? config.normalEnemyPool : null);
        AddEnemies(enemies, config != null ? config.bossPool : null);
        return enemies;
    }

    private static void AddEnemies(List<EnemyDataSO> enemies, EnemyDataSO[] source)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            EnemyDataSO enemy = source[i];
            if (enemy == null || enemy.prefab == null || enemies.Contains(enemy))
            {
                continue;
            }

            enemies.Add(enemy);
        }
    }

    private void CreateEnemyRow(EnemyDataSO enemyData)
    {
        GameObject row = new GameObject(enemyData.enemyName + "_Row", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(contentRoot, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, RowHeight);

        LayoutElement layoutElement = row.GetComponent<LayoutElement>();
        layoutElement.minHeight = RowHeight;
        layoutElement.preferredHeight = RowHeight;

        Image rowImage = row.GetComponent<Image>();
        rowImage.color = new Color(1f, 1f, 1f, 0.08f);
        rowImage.raycastTarget = true;

        string displayName = string.IsNullOrEmpty(enemyData.enemyName) ? enemyData.name : enemyData.enemyName;
        CreateText("Name", rowRect, displayName, 28f, TextAlignmentOptions.MidlineLeft, new Vector2(-105f, 0f), new Vector2(360f, 52f));

        Button spawnButton = CreateButton("SpawnButton", rowRect, "SPAWN", new Vector2(220f, 0f), new Vector2(170f, 48f));
        spawnButton.onClick.AddListener(() => SpawnEnemy(enemyData));
    }

    private void SpawnEnemy(EnemyDataSO enemyData)
    {
        if (enemyData == null || enemyData.prefab == null)
        {
            return;
        }

        if (spawner == null)
        {
            spawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        }

        Vector3 spawnPosition = spawner != null ? spawner.transform.position : Vector3.zero;
        GameObject spawned = Instantiate(enemyData.prefab, spawnPosition, Quaternion.identity);
        EnemyController enemy = spawned.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.enemyData = enemyData;
            enemy.SetLastEnemy(false);
        }
    }

    private TMP_Text CreateText(
        string objectName,
        RectTransform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string objectName, RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.24f, 0.24f, 0.24f, 1f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.24f, 0.24f, 0.24f, 1f);
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        colors.pressedColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        button.colors = colors;

        CreateText("Label", rect, label, 24f, TextAlignmentOptions.Center, Vector2.zero, size);
        return button;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        IsDebugModeActive = false;
    }
}
