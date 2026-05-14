using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UpgradePartSO))]
public class UpgradePartSOEditor : Editor
{
    private const float CELL_SIZE = 40f;
    private const float CELL_SPACING = 2f;

    // 色定義
    private static readonly Color COLOR_FILLED = new Color(0.2f, 0.8f, 0.9f, 1f);
    private static readonly Color COLOR_EMPTY = new Color(0.15f, 0.15f, 0.2f, 1f);
    private static readonly Color COLOR_HOVER = new Color(0.4f, 0.9f, 1f, 0.7f);
    private static readonly Color COLOR_GRID_BG = new Color(0.1f, 0.1f, 0.14f, 1f);

    // プレビュー回転
    private int previewRotation = 0;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        UpgradePartSO part = (UpgradePartSO)target;

        // ===== 基本情報 =====
        EditorGUILayout.LabelField("基本情報", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("partName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rarity"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("partColor"));

        EditorGUILayout.Space(10);

        // ===== 形状エディタ =====
        EditorGUILayout.LabelField("形状エディタ", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "マスをクリックして ON/OFF を切り替えてください。\n" +
            "サイズ変更は下のWidth/Heightで行えます。",
            MessageType.Info);

        // Width / Height
        EditorGUILayout.BeginHorizontal();
        int newWidth = EditorGUILayout.IntField("Width", part.width);
        int newHeight = EditorGUILayout.IntField("Height", part.height);
        EditorGUILayout.EndHorizontal();

        // サイズ変更があった場合、shapeDataをリサイズ
        newWidth = Mathf.Clamp(newWidth, 1, 6);
        newHeight = Mathf.Clamp(newHeight, 1, 6);
        if (newWidth != part.width || newHeight != part.height)
        {
            Undo.RecordObject(part, "Resize Upgrade Part Shape");
            ResizeShape(part, newWidth, newHeight);
            EditorUtility.SetDirty(part);
        }

        EditorGUILayout.Space(5);

        // グリッド描画
        DrawShapeGrid(part);

        EditorGUILayout.Space(5);

        // クイック形状ボタン
        EditorGUILayout.LabelField("クイック形状", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全埋め", GUILayout.Height(25)))
        {
            Undo.RecordObject(part, "Fill All");
            for (int i = 0; i < part.shapeData.Length; i++) part.shapeData[i] = true;
            EditorUtility.SetDirty(part);
        }
        if (GUILayout.Button("全消去", GUILayout.Height(25)))
        {
            Undo.RecordObject(part, "Clear All");
            for (int i = 0; i < part.shapeData.Length; i++) part.shapeData[i] = false;
            EditorUtility.SetDirty(part);
        }
        if (GUILayout.Button("反転", GUILayout.Height(25)))
        {
            Undo.RecordObject(part, "Invert Shape");
            for (int i = 0; i < part.shapeData.Length; i++) part.shapeData[i] = !part.shapeData[i];
            EditorUtility.SetDirty(part);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // ===== 回転プレビュー =====
        EditorGUILayout.LabelField("回転プレビュー", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        for (int r = 0; r < 4; r++)
        {
            string label = r == 0 ? "0°" : $"{r * 90}°";
            bool selected = (previewRotation == r);
            GUI.backgroundColor = selected ? COLOR_FILLED : Color.gray;
            if (GUILayout.Button(label, GUILayout.Height(25)))
                previewRotation = r;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (previewRotation != 0)
        {
            EditorGUILayout.Space(3);
            DrawRotatedPreview(part, previewRotation);
        }

        EditorGUILayout.Space(10);

        // ===== 効果 =====
        EditorGUILayout.LabelField("効果", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("effects"), true);

        EditorGUILayout.Space(5);

        // ===== コスト =====
        EditorGUILayout.LabelField("コスト（ショップ購入時）", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("costs"), true);

        EditorGUILayout.Space(5);

        // ===== 制限 =====
        EditorGUILayout.LabelField("制限", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rangedOnly"));

        // ===== 占有マス数表示 =====
        EditorGUILayout.Space(10);
        int filledCount = 0;
        if (part.shapeData != null)
            foreach (bool b in part.shapeData) if (b) filledCount++;
        EditorGUILayout.HelpBox($"占有マス数: {filledCount} / {part.width * part.height}", MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// クリック可能な形状グリッドを描画
    /// </summary>
    private void DrawShapeGrid(UpgradePartSO part)
    {
        if (part.shapeData == null || part.shapeData.Length != part.width * part.height)
        {
            ResizeShape(part, part.width, part.height);
        }

        float gridWidth = part.width * (CELL_SIZE + CELL_SPACING) - CELL_SPACING;
        float gridHeight = part.height * (CELL_SIZE + CELL_SPACING) - CELL_SPACING;

        // 背景
        Rect bgRect = GUILayoutUtility.GetRect(gridWidth + 20, gridHeight + 20);
        bgRect.x += (EditorGUIUtility.currentViewWidth - gridWidth - 20) / 2f;
        bgRect.width = gridWidth + 20;
        EditorGUI.DrawRect(new Rect(bgRect.x, bgRect.y, gridWidth + 20, gridHeight + 20), COLOR_GRID_BG);

        float startX = bgRect.x + 10;
        float startY = bgRect.y + 10;

        Event e = Event.current;

        for (int y = 0; y < part.height; y++)
        {
            for (int x = 0; x < part.width; x++)
            {
                int idx = y * part.width + x;
                Rect cellRect = new Rect(
                    startX + x * (CELL_SIZE + CELL_SPACING),
                    startY + y * (CELL_SIZE + CELL_SPACING),
                    CELL_SIZE,
                    CELL_SIZE
                );

                bool filled = idx < part.shapeData.Length && part.shapeData[idx];

                // セル描画
                Color cellColor = filled ? part.partColor : COLOR_EMPTY;
                if (cellRect.Contains(e.mousePosition))
                    cellColor = Color.Lerp(cellColor, COLOR_HOVER, 0.4f);

                EditorGUI.DrawRect(cellRect, cellColor);

                // 枠線
                DrawCellBorder(cellRect, filled ? Color.white * 0.6f : Color.white * 0.2f);

                // テキスト表示
                GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                style.normal.textColor = filled ? Color.white : Color.gray * 0.5f;
                style.fontSize = 16;
                GUI.Label(cellRect, filled ? "■" : "☐", style);

                // クリック判定
                if (e.type == EventType.MouseDown && cellRect.Contains(e.mousePosition))
                {
                    Undo.RecordObject(part, "Toggle Shape Cell");
                    part.shapeData[idx] = !part.shapeData[idx];
                    EditorUtility.SetDirty(part);
                    e.Use();
                    Repaint();
                }
            }
        }
    }

    /// <summary>
    /// 回転後の形状プレビュー（読み取り専用）
    /// </summary>
    private void DrawRotatedPreview(UpgradePartSO part, int rotation)
    {
        bool[,] rotated = part.GetRotatedShape(rotation);
        int rh = rotated.GetLength(0);
        int rw = rotated.GetLength(1);

        float previewCellSize = 25f;
        float previewSpacing = 2f;
        float gridWidth = rw * (previewCellSize + previewSpacing) - previewSpacing;
        float gridHeight = rh * (previewCellSize + previewSpacing) - previewSpacing;

        Rect bgRect = GUILayoutUtility.GetRect(gridWidth + 10, gridHeight + 10);
        bgRect.x += (EditorGUIUtility.currentViewWidth - gridWidth - 10) / 2f;
        bgRect.width = gridWidth + 10;
        EditorGUI.DrawRect(bgRect, COLOR_GRID_BG);

        float startX = bgRect.x + 5;
        float startY = bgRect.y + 5;

        for (int y = 0; y < rh; y++)
        {
            for (int x = 0; x < rw; x++)
            {
                Rect cellRect = new Rect(
                    startX + x * (previewCellSize + previewSpacing),
                    startY + y * (previewCellSize + previewSpacing),
                    previewCellSize,
                    previewCellSize
                );

                bool filled = rotated[y, x];
                EditorGUI.DrawRect(cellRect, filled ? part.partColor * 0.8f : COLOR_EMPTY);
                DrawCellBorder(cellRect, Color.white * 0.15f);
            }
        }
    }

    /// <summary>
    /// shapeDataをリサイズ（既存データを保持）
    /// </summary>
    private void ResizeShape(UpgradePartSO part, int newWidth, int newHeight)
    {
        bool[] oldData = part.shapeData ?? new bool[0];
        int oldWidth = part.width;
        int oldHeight = part.height;

        bool[] newData = new bool[newWidth * newHeight];

        // 既存データをコピー
        for (int y = 0; y < Mathf.Min(oldHeight, newHeight); y++)
        {
            for (int x = 0; x < Mathf.Min(oldWidth, newWidth); x++)
            {
                int oldIdx = y * oldWidth + x;
                int newIdx = y * newWidth + x;
                if (oldIdx < oldData.Length)
                    newData[newIdx] = oldData[oldIdx];
            }
        }

        part.width = newWidth;
        part.height = newHeight;
        part.shapeData = newData;
    }

    /// <summary>
    /// セルの枠線を描画
    /// </summary>
    private void DrawCellBorder(Rect rect, Color color)
    {
        float t = 1f;
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), color);               // 上
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), color);         // 下
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), color);               // 左
        EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), color);        // 右
    }
}
