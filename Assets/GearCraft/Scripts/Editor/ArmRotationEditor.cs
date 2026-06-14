using UnityEditor;
using UnityEngine;

namespace GearCraft.Editor
{
    [CustomEditor(typeof(global::ArmRotation))]
    public sealed class ArmRotationEditor : UnityEditor.Editor
    {
        private SerializedProperty playerProperty;
        private SerializedProperty shoulderProperty;
        private SerializedProperty armProperty;
        private SerializedProperty armOffsetsProperty;
        private SerializedProperty armScalesProperty;
        private SerializedProperty armBaseRotationsProperty;
        private SerializedProperty poseRulesProperty;

        private int selectedPoseIndex;
        private bool sceneEditEnabled = true;
        private static readonly string[] BuiltInPoseDescriptions =
        {
            "Pose 0: Default / Katana / GearCraft_Sword / fallback",
            "Pose 1: Assault / RailCraft / SteamGatling",
            "Pose 2: SteamShoot",
            "Pose 3: GearCraft_Axe"
        };

        private void OnEnable()
        {
            playerProperty = serializedObject.FindProperty("player");
            shoulderProperty = serializedObject.FindProperty("sholder");
            armProperty = serializedObject.FindProperty("arm");
            armOffsetsProperty = serializedObject.FindProperty("armOffsets");
            armScalesProperty = serializedObject.FindProperty("armScales");
            armBaseRotationsProperty = serializedObject.FindProperty("armBaseRotations");
            poseRulesProperty = serializedObject.FindProperty("poseRules");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawReferences();
            EditorGUILayout.Space(8f);
            DrawPoseEditor();
            EditorGUILayout.Space(8f);
            DrawPoseRules();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReferences()
        {
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playerProperty);
            EditorGUILayout.PropertyField(shoulderProperty, new GUIContent("Shoulder"));
            EditorGUILayout.PropertyField(armProperty);
        }

        private void DrawPoseEditor()
        {
            EditorGUILayout.LabelField("Weapon Visual Pose Editor", EditorStyles.boldLabel);

            int currentLength = GetMaxPoseLength();
            int newLength = Mathf.Max(1, EditorGUILayout.IntField("Pose Count", Mathf.Max(1, currentLength)));
            if (newLength != currentLength)
            {
                ResizePoseArrays(newLength);
                currentLength = newLength;
            }

            selectedPoseIndex = Mathf.Clamp(selectedPoseIndex, 0, currentLength - 1);
            selectedPoseIndex = EditorGUILayout.IntSlider("Editing Pose", selectedPoseIndex, 0, currentLength - 1);

            DrawSelectedPoseDescription(selectedPoseIndex);
            DrawPoseFields(selectedPoseIndex);
            DrawPoseActions(selectedPoseIndex);
        }

        private void DrawSelectedPoseDescription(int poseIndex)
        {
            string description = poseIndex < BuiltInPoseDescriptions.Length
                ? BuiltInPoseDescriptions[poseIndex]
                : $"Pose {poseIndex}: poseRules で割り当てた武器用";

            EditorGUILayout.HelpBox(description, MessageType.None);
            DrawCustomPoseRuleMatches(poseIndex);
        }

        private void DrawCustomPoseRuleMatches(int poseIndex)
        {
            if (poseRulesProperty == null || !poseRulesProperty.isArray)
            {
                return;
            }

            string matches = string.Empty;
            for (int i = 0; i < poseRulesProperty.arraySize; i++)
            {
                SerializedProperty rule = poseRulesProperty.GetArrayElementAtIndex(i);
                SerializedProperty trigger = rule.FindPropertyRelative("armTriggerName");
                SerializedProperty index = rule.FindPropertyRelative("poseIndex");
                if (trigger == null || index == null || index.intValue != poseIndex || string.IsNullOrEmpty(trigger.stringValue))
                {
                    continue;
                }

                matches = string.IsNullOrEmpty(matches) ? trigger.stringValue : $"{matches}, {trigger.stringValue}";
            }

            if (!string.IsNullOrEmpty(matches))
            {
                EditorGUILayout.HelpBox($"poseRules: {matches}", MessageType.None);
            }
        }

        private void DrawPoseFields(int poseIndex)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(armOffsetsProperty.GetArrayElementAtIndex(poseIndex), new GUIContent("Position Offset"));
            EditorGUILayout.PropertyField(armScalesProperty.GetArrayElementAtIndex(poseIndex), new GUIContent("Scale"));
            EditorGUILayout.PropertyField(armBaseRotationsProperty.GetArrayElementAtIndex(poseIndex), new GUIContent("Base Rotation"));
            EditorGUILayout.EndVertical();
        }

        private void DrawPoseActions(int poseIndex)
        {
            global::ArmRotation armRotation = (global::ArmRotation)target;
            Transform arm = armProperty.objectReferenceValue as Transform;
            Transform shoulder = shoulderProperty.objectReferenceValue as Transform;

            sceneEditEnabled = EditorGUILayout.Toggle("Scene View Handles", sceneEditEnabled);

            using (new EditorGUI.DisabledScope(arm == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply Pose To Arm"))
                {
                    ApplyPoseToArm(armRotation, arm, poseIndex);
                }

                if (GUILayout.Button("Save Current Arm Transform"))
                {
                    SaveCurrentArmTransform(armRotation, arm, shoulder, poseIndex);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Select Arm Transform"))
                {
                    Selection.activeTransform = arm;
                    EditorGUIUtility.PingObject(arm);
                }

                if (GUILayout.Button("Open Preview Window"))
                {
                    ArmRotationPreviewWindow.Open(armRotation, poseIndex);
                }
            }

            EditorGUILayout.HelpBox(
                "Position Offset / Scale / Base Rotation が武器の見た目位置です。SceneビューでArmを動かしてから Save Current Arm Transform を押すと、選択中Poseへ保存できます。",
                MessageType.Info);
        }

        private void DrawPoseRules()
        {
            EditorGUILayout.LabelField("Weapon Trigger To Pose", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(poseRulesProperty, true);
        }

        private int GetMaxPoseLength()
        {
            int maxLength = armOffsetsProperty.arraySize;
            maxLength = Mathf.Max(maxLength, armScalesProperty.arraySize);
            maxLength = Mathf.Max(maxLength, armBaseRotationsProperty.arraySize);
            return maxLength;
        }

        private void ResizePoseArrays(int length)
        {
            ResizeVector3Array(armOffsetsProperty, length, Vector3.zero);
            ResizeVector3Array(armScalesProperty, length, Vector3.one);
            ResizeVector3Array(armBaseRotationsProperty, length, Vector3.zero);
        }

        private static void ResizeVector3Array(SerializedProperty arrayProperty, int length, Vector3 defaultValue)
        {
            int oldSize = arrayProperty.arraySize;
            arrayProperty.arraySize = length;

            for (int i = oldSize; i < length; i++)
            {
                arrayProperty.GetArrayElementAtIndex(i).vector3Value = defaultValue;
            }
        }

        private void ApplyPoseToArm(global::ArmRotation armRotation, Transform arm, int poseIndex)
        {
            Undo.RecordObject(arm, "Apply Weapon Visual Pose");
            arm.localPosition = armOffsetsProperty.GetArrayElementAtIndex(poseIndex).vector3Value;
            arm.localScale = armScalesProperty.GetArrayElementAtIndex(poseIndex).vector3Value;
            arm.localEulerAngles = armBaseRotationsProperty.GetArrayElementAtIndex(poseIndex).vector3Value;
            EditorUtility.SetDirty(arm);
            EditorUtility.SetDirty(armRotation);
        }

        private void SaveCurrentArmTransform(global::ArmRotation armRotation, Transform arm, Transform shoulder, int poseIndex)
        {
            Undo.RecordObject(armRotation, "Save Weapon Visual Pose");

            Vector3 offset = arm.localPosition;
            if (shoulder != null && arm.parent != shoulder)
            {
                offset = shoulder.InverseTransformPoint(arm.position);
            }

            armOffsetsProperty.GetArrayElementAtIndex(poseIndex).vector3Value = offset;
            armScalesProperty.GetArrayElementAtIndex(poseIndex).vector3Value = arm.localScale;
            armBaseRotationsProperty.GetArrayElementAtIndex(poseIndex).vector3Value = arm.localEulerAngles;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(armRotation);
        }

        private void OnSceneGUI()
        {
            if (!sceneEditEnabled)
            {
                return;
            }

            serializedObject.Update();

            global::ArmRotation armRotation = (global::ArmRotation)target;
            Transform arm = armProperty.objectReferenceValue as Transform;
            if (arm == null || GetMaxPoseLength() == 0)
            {
                return;
            }

            selectedPoseIndex = Mathf.Clamp(selectedPoseIndex, 0, GetMaxPoseLength() - 1);

            Transform shoulder = shoulderProperty.objectReferenceValue as Transform;
            SerializedProperty offsetProperty = armOffsetsProperty.GetArrayElementAtIndex(selectedPoseIndex);
            SerializedProperty scaleProperty = armScalesProperty.GetArrayElementAtIndex(selectedPoseIndex);
            SerializedProperty rotationProperty = armBaseRotationsProperty.GetArrayElementAtIndex(selectedPoseIndex);

            Vector3 offset = offsetProperty.vector3Value;
            Vector3 scale = scaleProperty.vector3Value;
            Vector3 rotation = rotationProperty.vector3Value;

            Transform reference = shoulder != null ? shoulder : arm.parent;
            Vector3 worldPosition = reference != null ? reference.TransformPoint(offset) : arm.position;
            Quaternion worldRotation = reference != null
                ? reference.rotation * Quaternion.Euler(rotation)
                : Quaternion.Euler(rotation);

            float handleSize = HandleUtility.GetHandleSize(worldPosition);
            Handles.Label(
                worldPosition + Vector3.up * handleSize * 0.35f,
                GetSceneLabel(selectedPoseIndex),
                EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPosition = Handles.PositionHandle(worldPosition, worldRotation);
            Quaternion newWorldRotation = Handles.RotationHandle(worldRotation, worldPosition);
            Vector3 newScale = Handles.ScaleHandle(scale, worldPosition, worldRotation, handleSize);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(armRotation, "Edit Weapon Visual Pose");
                Undo.RecordObject(arm, "Preview Weapon Visual Pose");

                Vector3 newOffset = reference != null ? reference.InverseTransformPoint(newWorldPosition) : newWorldPosition;
                Quaternion localRotation = reference != null
                    ? Quaternion.Inverse(reference.rotation) * newWorldRotation
                    : newWorldRotation;

                offsetProperty.vector3Value = newOffset;
                scaleProperty.vector3Value = newScale;
                rotationProperty.vector3Value = localRotation.eulerAngles;

                serializedObject.ApplyModifiedProperties();
                ApplyPoseToArm(armRotation, arm, selectedPoseIndex);
                SceneView.RepaintAll();
            }
        }

        private static string GetSceneLabel(int poseIndex)
        {
            if (poseIndex < BuiltInPoseDescriptions.Length)
            {
                return BuiltInPoseDescriptions[poseIndex];
            }

            return $"Pose {poseIndex}";
        }
    }

    public sealed class ArmRotationPreviewWindow : EditorWindow
    {
        private const float MinZoom = 0.5f;
        private const float MaxZoom = 8f;

        private static readonly string[] PoseDescriptions =
        {
            "Pose 0: Default / Katana / GearCraft_Sword / fallback",
            "Pose 1: Assault / RailCraft / SteamGatling",
            "Pose 2: SteamShoot",
            "Pose 3: GearCraft_Axe"
        };

        private global::ArmRotation armRotation;
        private SerializedObject armRotationObject;
        private SerializedProperty armProperty;
        private SerializedProperty shoulderProperty;
        private SerializedProperty armOffsetsProperty;
        private SerializedProperty armScalesProperty;
        private SerializedProperty armBaseRotationsProperty;

        private PreviewRenderUtility previewUtility;
        private GameObject previewInstance;
        private Transform sourceArm;
        private int poseIndex;
        private float zoom = 3f;

        public static void Open(global::ArmRotation target, int poseIndex)
        {
            ArmRotationPreviewWindow window = GetWindow<ArmRotationPreviewWindow>("Weapon Visual Preview");
            window.SetTarget(target, poseIndex);
            window.Show();
        }

        private void OnEnable()
        {
            CreatePreviewUtility();
        }

        private void OnDisable()
        {
            DestroyPreviewInstance();
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            armRotation = EditorGUILayout.ObjectField("ArmRotation", armRotation, typeof(global::ArmRotation), true) as global::ArmRotation;
            if (EditorGUI.EndChangeCheck())
            {
                SetTarget(armRotation, poseIndex);
            }

            if (armRotation == null)
            {
                EditorGUILayout.HelpBox("ArmRotation を指定してください。", MessageType.Info);
                return;
            }

            RefreshSerializedObject();
            if (armRotationObject == null)
            {
                return;
            }

            armRotationObject.Update();
            EnsurePoseArrays(Mathf.Max(1, GetMaxPoseLength()));

            int poseCount = Mathf.Max(1, GetMaxPoseLength());
            poseIndex = Mathf.Clamp(poseIndex, 0, poseCount - 1);
            poseIndex = EditorGUILayout.IntSlider("Editing Pose", poseIndex, 0, poseCount - 1);
            EditorGUILayout.HelpBox(GetPoseDescription(poseIndex), MessageType.None);

            EditorGUILayout.Space(4f);
            DrawPoseFields();
            EditorGUILayout.Space(4f);
            DrawToolbar();
            EditorGUILayout.Space(6f);
            DrawPreview();

            armRotationObject.ApplyModifiedProperties();
        }

        private void SetTarget(global::ArmRotation target, int newPoseIndex)
        {
            armRotation = target;
            poseIndex = Mathf.Max(0, newPoseIndex);
            RefreshSerializedObject();
            RebuildPreviewInstance();
            Repaint();
        }

        private void RefreshSerializedObject()
        {
            if (armRotation == null)
            {
                armRotationObject = null;
                return;
            }

            if (armRotationObject == null || armRotationObject.targetObject != armRotation)
            {
                armRotationObject = new SerializedObject(armRotation);
                armProperty = armRotationObject.FindProperty("arm");
                shoulderProperty = armRotationObject.FindProperty("sholder");
                armOffsetsProperty = armRotationObject.FindProperty("armOffsets");
                armScalesProperty = armRotationObject.FindProperty("armScales");
                armBaseRotationsProperty = armRotationObject.FindProperty("armBaseRotations");
            }
        }

        private void DrawPoseFields()
        {
            SerializedProperty offsetProperty = armOffsetsProperty.GetArrayElementAtIndex(poseIndex);
            SerializedProperty scaleProperty = armScalesProperty.GetArrayElementAtIndex(poseIndex);
            SerializedProperty rotationProperty = armBaseRotationsProperty.GetArrayElementAtIndex(poseIndex);

            EditorGUI.BeginChangeCheck();
            Vector3 offset = EditorGUILayout.Vector3Field("Position Offset", offsetProperty.vector3Value);
            Vector3 scale = EditorGUILayout.Vector3Field("Scale", scaleProperty.vector3Value);
            Vector3 rotation = EditorGUILayout.Vector3Field("Base Rotation", rotationProperty.vector3Value);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(armRotation, "Edit Weapon Visual Pose");
                offsetProperty.vector3Value = offset;
                scaleProperty.vector3Value = scale;
                rotationProperty.vector3Value = rotation;
                armRotationObject.ApplyModifiedProperties();
                ApplyPoseToLiveArm();
                EditorUtility.SetDirty(armRotation);
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            zoom = EditorGUILayout.Slider("Preview Zoom", zoom, MinZoom, MaxZoom);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Preview"))
            {
                RebuildPreviewInstance();
            }

            if (GUILayout.Button("Apply To Arm"))
            {
                ApplyPoseToLiveArm();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview()
        {
            Transform arm = armProperty.objectReferenceValue as Transform;
            if (arm == null)
            {
                EditorGUILayout.HelpBox("Arm が設定されていません。", MessageType.Warning);
                return;
            }

            if (sourceArm != arm || previewInstance == null)
            {
                RebuildPreviewInstance();
            }

            Rect previewRect = GUILayoutUtility.GetRect(320f, 320f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(previewRect, new Color(0.08f, 0.08f, 0.09f, 1f));
            DrawPreviewGuides(previewRect);

            if (previewUtility == null || previewInstance == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            UpdatePreviewTransform();
            previewUtility.BeginPreview(previewRect, GUIStyle.none);
            previewUtility.camera.orthographic = true;
            previewUtility.camera.orthographicSize = zoom;
            previewUtility.camera.transform.position = new Vector3(0f, 0f, -10f);
            previewUtility.camera.transform.rotation = Quaternion.identity;
            previewUtility.camera.nearClipPlane = 0.1f;
            previewUtility.camera.farClipPlane = 100f;
            previewUtility.camera.clearFlags = CameraClearFlags.Color;
            previewUtility.camera.backgroundColor = new Color(0.08f, 0.08f, 0.09f, 1f);
            previewUtility.Render();
            Texture texture = previewUtility.EndPreview();
            GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, false);
            DrawPreviewGuides(previewRect);
        }

        private void DrawPreviewGuides(Rect previewRect)
        {
            Color guideColor = new Color(1f, 1f, 1f, 0.18f);
            EditorGUI.DrawRect(new Rect(previewRect.center.x - 1f, previewRect.y, 2f, previewRect.height), guideColor);
            EditorGUI.DrawRect(new Rect(previewRect.x, previewRect.center.y - 1f, previewRect.width, 2f), guideColor);
            GUI.Label(new Rect(previewRect.x + 8f, previewRect.y + 6f, 240f, 20f), "Center = Shoulder Origin", EditorStyles.miniLabel);
        }

        private void CreatePreviewUtility()
        {
            if (previewUtility != null)
            {
                return;
            }

            previewUtility = new PreviewRenderUtility();
            previewUtility.cameraFieldOfView = 30f;
            if (previewUtility.lights != null && previewUtility.lights.Length > 0)
            {
                previewUtility.lights[0].intensity = 1.2f;
                previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            }
        }

        private void RebuildPreviewInstance()
        {
            DestroyPreviewInstance();
            CreatePreviewUtility();

            Transform arm = armProperty != null ? armProperty.objectReferenceValue as Transform : null;
            if (arm == null || previewUtility == null)
            {
                return;
            }

            sourceArm = arm;
            previewInstance = Instantiate(arm.gameObject);
            previewInstance.name = $"{arm.name}_Preview";
            previewInstance.hideFlags = HideFlags.HideAndDontSave;

            Animator[] animators = previewInstance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = false;
            }

            previewUtility.AddSingleGO(previewInstance);
            UpdatePreviewTransform();
        }

        private void DestroyPreviewInstance()
        {
            if (previewInstance == null)
            {
                return;
            }

            DestroyImmediate(previewInstance);
            previewInstance = null;
            sourceArm = null;
        }

        private void UpdatePreviewTransform()
        {
            if (previewInstance == null || armOffsetsProperty == null || GetMaxPoseLength() == 0)
            {
                return;
            }

            poseIndex = Mathf.Clamp(poseIndex, 0, GetMaxPoseLength() - 1);
            previewInstance.transform.position = armOffsetsProperty.GetArrayElementAtIndex(poseIndex).vector3Value;
            previewInstance.transform.rotation = Quaternion.Euler(armBaseRotationsProperty.GetArrayElementAtIndex(poseIndex).vector3Value);
            previewInstance.transform.localScale = armScalesProperty.GetArrayElementAtIndex(poseIndex).vector3Value;
        }

        private void ApplyPoseToLiveArm()
        {
            Transform arm = armProperty.objectReferenceValue as Transform;
            if (arm == null || GetMaxPoseLength() == 0)
            {
                return;
            }

            poseIndex = Mathf.Clamp(poseIndex, 0, GetMaxPoseLength() - 1);
            Undo.RecordObject(arm, "Apply Weapon Visual Pose");
            arm.localPosition = armOffsetsProperty.GetArrayElementAtIndex(poseIndex).vector3Value;
            arm.localScale = armScalesProperty.GetArrayElementAtIndex(poseIndex).vector3Value;
            arm.localEulerAngles = armBaseRotationsProperty.GetArrayElementAtIndex(poseIndex).vector3Value;
            EditorUtility.SetDirty(arm);
            SceneView.RepaintAll();
        }

        private int GetMaxPoseLength()
        {
            int maxLength = armOffsetsProperty != null ? armOffsetsProperty.arraySize : 0;
            maxLength = Mathf.Max(maxLength, armScalesProperty != null ? armScalesProperty.arraySize : 0);
            maxLength = Mathf.Max(maxLength, armBaseRotationsProperty != null ? armBaseRotationsProperty.arraySize : 0);
            return maxLength;
        }

        private void EnsurePoseArrays(int length)
        {
            ResizeVector3Array(armOffsetsProperty, length, Vector3.zero);
            ResizeVector3Array(armScalesProperty, length, Vector3.one);
            ResizeVector3Array(armBaseRotationsProperty, length, Vector3.zero);
        }

        private static void ResizeVector3Array(SerializedProperty property, int length, Vector3 defaultValue)
        {
            if (property == null || property.arraySize >= length)
            {
                return;
            }

            int oldSize = property.arraySize;
            property.arraySize = length;
            for (int i = oldSize; i < length; i++)
            {
                property.GetArrayElementAtIndex(i).vector3Value = defaultValue;
            }
        }

        private static string GetPoseDescription(int index)
        {
            return index < PoseDescriptions.Length ? PoseDescriptions[index] : $"Pose {index}";
        }
    }
}
