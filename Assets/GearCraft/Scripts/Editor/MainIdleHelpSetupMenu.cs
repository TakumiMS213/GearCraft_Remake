#if UNITY_EDITOR
using GearCraft.Scripts.Main;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MainIdleHelpSetupMenu
{
    private const string MainScenePath = "Assets/GearCraft/Scenes/Main.unity";

    [MenuItem("GearCraft/Setup Main Idle Help")]
    public static void Setup()
    {
        EditorSceneManager.OpenScene(MainScenePath);

        GameObject controllerObject = GameObject.Find("MainIdleHelpController");
        if (controllerObject == null)
        {
            controllerObject = new GameObject("MainIdleHelpController");
        }

        MainIdleHelpController controller = controllerObject.GetComponent<MainIdleHelpController>();
        if (controller == null)
        {
            controller = controllerObject.AddComponent<MainIdleHelpController>();
        }

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("moveHelpDelay").floatValue = 7f;
        serialized.FindProperty("attackHelpDelay").floatValue = 7f;
        serialized.FindProperty("boostHelpDelay").floatValue = 10f;
        serialized.FindProperty("moveIcon").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/ad.png");
        serialized.FindProperty("attackIcon").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/Title/mouse.png");
        serialized.FindProperty("font").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GearCraft/DotGothic16-Regular SDF.asset");
        serialized.FindProperty("moveText").stringValue = "移動";
        serialized.FindProperty("attackText").stringValue = "攻撃";
        serialized.FindProperty("boostKeyObject").objectReferenceValue = GameObject.Find("Q");
        serialized.FindProperty("boostTextObject").objectReferenceValue = GameObject.Find("boost");
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controllerObject);
        EditorSceneManager.MarkSceneDirty(controllerObject.scene);
        EditorSceneManager.SaveScene(controllerObject.scene);
        AssetDatabase.SaveAssets();

        Debug.Log("Main idle help setup completed.");
    }
}
#endif
