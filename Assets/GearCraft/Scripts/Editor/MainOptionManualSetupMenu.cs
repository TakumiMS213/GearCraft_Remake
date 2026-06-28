#if UNITY_EDITOR
using GearCraft.Scripts.Main;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainOptionManualSetupMenu
{
    private const string MainScenePath = "Assets/GearCraft/Scenes/Main.unity";

    [MenuItem("GearCraft/Setup Main Option Manual")]
    public static void Setup()
    {
        EditorSceneManager.OpenScene(MainScenePath);

        GameObject controllerObject = GameObject.Find("MainOptionManualController");
        if (controllerObject == null)
        {
            controllerObject = new GameObject("MainOptionManualController");
        }

        MainOptionManualController controller = controllerObject.GetComponent<MainOptionManualController>();
        if (controller == null)
        {
            controller = controllerObject.AddComponent<MainOptionManualController>();
        }

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("optionButton").objectReferenceValue = FindSceneComponent<Button>("OptionButton");
        GameObject optionPanel = FindOptionPanel();
        serialized.FindProperty("optionPanel").objectReferenceValue = optionPanel;
        serialized.FindProperty("optionCloseButton").objectReferenceValue = FindSceneComponent<Button>("HideSetting");
        serialized.FindProperty("font").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GearCraft/DotGothic16-Regular SDF.asset");
        serialized.FindProperty("panelSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/gearcraft_setting_panel.png");
        serialized.FindProperty("buttonSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/gearcraft_setting_off.png");
        serialized.FindProperty("closeSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/gearcraft_setting_off.png");
        serialized.FindProperty("manualTitleSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/ManualTitle.png");
        serialized.FindProperty("moveKeySprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/ad.png");
        serialized.FindProperty("moveTextSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/wasdText.mdp.png");
        serialized.FindProperty("attackKeySprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/mouse.png");
        serialized.FindProperty("attackTextSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/attacktext.png");
        serialized.FindProperty("boostKeySprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/Q.png");
        serialized.FindProperty("boostTextSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/BoostManual_text.png");
        serialized.FindProperty("shiftKeySprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/SHIFT.png");
        serialized.FindProperty("shiftTextSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/shiftext.png");
        serialized.FindProperty("spaceKeySprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/SPACE.png");
        serialized.FindProperty("jumpTextSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/Jumptext.png");
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controllerObject);
        EditorSceneManager.MarkSceneDirty(controllerObject.scene);
        EditorSceneManager.SaveScene(controllerObject.scene);
        AssetDatabase.SaveAssets();

        Debug.Log("Main option manual setup completed.");
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate != null && candidate.name == objectName && candidate.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    private static GameObject FindOptionPanel()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject fallback = null;
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate == null || candidate.name != "SettingPanel" || !candidate.scene.IsValid())
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }

            if (candidate.transform.Find("SettingPanel") != null ||
                FindChildComponent<Button>(candidate.transform, "HideSetting") != null)
            {
                return candidate;
            }
        }

        return fallback;
    }

    private static T FindSceneComponent<T>(string objectName) where T : Component
    {
        GameObject gameObject = FindSceneObject(objectName);
        return gameObject != null ? gameObject.GetComponent<T>() : null;
    }

    private static T FindChildComponent<T>(Transform parent, string childName) where T : Component
    {
        if (parent == null)
        {
            return null;
        }

        T[] components = parent.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].gameObject.name == childName)
            {
                return components[i];
            }
        }

        return null;
    }
}
#endif
