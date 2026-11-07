#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors persistent Title scene objects; no UI is generated during play.</summary>
public static class StartingWeaponSelectionSetup
{
    private const string TitlePath = "Assets/GearCraft/Scenes/Title.unity";
    private const string Weapons = "Assets/GearCraft/ScriptableObject/WeaponData/";
    private static readonly Color Gold = new Color(.88f, .66f, .29f, 1f);
    private static TMP_FontAsset font;

    [MenuItem("GearCraft/Setup Starting Weapon Selection")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != TitlePath || scene.isDirty) throw new InvalidOperationException("Open a saved Title scene first.");
        if (scene.GetRootGameObjects().Any(x => x.name == "StartingWeaponSelection"))
            throw new InvalidOperationException("Selection window is already authored in Title.");

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GearCraft/DotGothic16-Regular SDF.asset");
        var loader = UnityEngine.Object.FindFirstObjectByType<StorySceneLoader>();
        var transition = UnityEngine.Object.FindFirstObjectByType<SceneTransitionManager>(FindObjectsInactive.Include);
        var start = GameObject.Find("StartButton").GetComponent<Button>();
        Canvas titleCanvas = start.GetComponentInParent<Canvas>();
        var titleGroup = titleCanvas.GetComponent<CanvasGroup>();
        if (titleGroup == null) titleGroup = titleCanvas.gameObject.AddComponent<CanvasGroup>();
        var sound = UnityEngine.Object.FindFirstObjectByType<SoundButton>();

        var root = new GameObject("StartingWeaponSelection", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        var controller = root.AddComponent<StartingWeaponSelectionController>();

        var window = Rect("Window", root.transform, Vector2.zero, Vector2.zero);
        window.anchorMin = Vector2.zero; window.anchorMax = Vector2.one;
        ImageOf(window, new Color(0,0,0,.88f), true);
        var panel = Rect("Panel", window, Vector2.zero, new Vector2(1040,940));
        ImageOf(panel, new Color(.075f,.075f,.07f,1), true);
        Border(panel, Gold, 2);
        Text("Heading",panel,new Vector2(0,401),new Vector2(800,58),"出撃武器を選択",40,Gold);
        var unlockNote=Text("Reward",panel,new Vector2(0,347),new Vector2(900,38),"AR-227 撃破報酬",24,new Color(.7f,.68f,.63f));
        var close = ButtonOf("Close",panel,new Vector2(445,411),new Vector2(100,50),"戻る",24);
        UnityEventTools.AddPersistentListener(close.onClick,controller.Close);

        // Acquisition progression; ties follow the recipe unlock display order.
        string[] ids = { "Katana", "GearCraft_Sword", "SteamCannon", "SteamThrower", "SteamGatling", "RailCraft" };
        string[] names = { "歩兵用軍刀", "GearCraft", "SteamCannon", "SteamThrower", "SteamGatling", "RailCraft" };
        string[] notes = {
            "扱いやすい近接武器。従来の装備で出撃する。",
            "大剣と斧を切り替えて戦う近接武装。",
            "着弾地点に水蒸気爆発を起こす遠距離武装。",
            "高温の蒸気で敵をオーバーヒートさせ、進行を遅らせる。",
            "撃ち続けると発射レートが上がる制圧用武装。",
            "長押しでチャージし、離して貫通弾を放つ。"
        };
        var buttons = new Button[ids.Length];
        var frames = new GameObject[ids.Length];
        var labels = new GameObject[ids.Length];
        for (int i=0;i<ids.Length;i++)
        {
            var card = Rect(ids[i],panel,new Vector2((i%3-1)*272,186-(i/3)*258),new Vector2(228,228));
            Image bg = ImageOf(card,new Color(.16f,.16f,.145f),true);
            Border(card,new Color(.38f,.35f,.28f),1);
            Button button = card.gameObject.AddComponent<Button>();button.targetGraphic=bg;SetColors(button);
            if(sound!=null)UnityEventTools.AddPersistentListener(button.onClick,sound.PlayClickSound);
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(Weapons+ids[i]+".asset");
            var icon = Rect("Icon",card,new Vector2(0,10),new Vector2(184,150));
            var image=ImageOf(icon,Color.white,false);image.sprite=weapon.icon;image.preserveAspect=true;
            Text("Name",card,new Vector2(0,-79),new Vector2(218,46),names[i],25,new Color(.92f,.9f,.83f));
            var frame=Rect("SelectionFrame",card,Vector2.zero,new Vector2(228,228));Border(frame,Gold,4);
            var label=Text("Selected",card,new Vector2(0,91),new Vector2(190,30),"選択中",21,Gold);
            buttons[i]=button;frames[i]=frame.gameObject;labels[i]=label.gameObject;
            frame.gameObject.SetActive(false);label.gameObject.SetActive(false);
        }
        var selectedName=Text("SelectedName",panel,new Vector2(0,-256),new Vector2(900,40),"武器を選択してください",28,Gold);
        var description=Text("Description",panel,new Vector2(0,-300),new Vector2(950,55),"ブロックをクリックして選択できます。",24,new Color(.87f,.86f,.81f));
        var deploy=ButtonOf("Deploy",panel,new Vector2(0,-389),new Vector2(620,82),"この武器で出撃する",32);
        UnityEventTools.AddPersistentListener(deploy.onClick,controller.Confirm);deploy.interactable=false;
        if(sound!=null){UnityEventTools.AddPersistentListener(close.onClick,sound.PlayClickSound);UnityEventTools.AddPersistentListener(deploy.onClick,sound.PlayClickSound);}

        // Keep keyboard/controller navigation inside the modal as well as pointer input.
        for(int i=0;i<buttons.Length;i++)
        {
            Navigation nav = new Navigation { mode=Navigation.Mode.Explicit,
                selectOnLeft=buttons[i/3*3+(i+2)%3], selectOnRight=buttons[i/3*3+(i+1)%3],
                selectOnUp=i<3 ? close : buttons[i-3], selectOnDown=i<3 ? buttons[i+3] : deploy };
            buttons[i].navigation=nav;
        }
        deploy.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=buttons[4],selectOnDown=close};
        close.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnDown=buttons[2],selectOnUp=deploy};

        var so=new SerializedObject(controller);
        so.FindProperty("window").objectReferenceValue=window.gameObject;
        so.FindProperty("titleControls").objectReferenceValue=titleGroup;
        so.FindProperty("startButton").objectReferenceValue=start;
        so.FindProperty("deployButton").objectReferenceValue=deploy;
        so.FindProperty("selectedName").objectReferenceValue=selectedName;
        so.FindProperty("selectedDescription").objectReferenceValue=description;
        so.FindProperty("unlockNote").objectReferenceValue=unlockNote;
        so.FindProperty("storyLoader").objectReferenceValue=loader;
        so.FindProperty("sceneTransition").objectReferenceValue=transition;
        var choices=so.FindProperty("choices");choices.arraySize=ids.Length;
        for(int i=0;i<ids.Length;i++)
        {
            var item=choices.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("weapon").objectReferenceValue=AssetDatabase.LoadAssetAtPath<WeaponDataSO>(Weapons+ids[i]+".asset");
            item.FindPropertyRelative("additionalWeapon").objectReferenceValue=i==1 ? AssetDatabase.LoadAssetAtPath<WeaponDataSO>(Weapons+"GearCraft_Axe.asset") : null;
            item.FindPropertyRelative("displayName").stringValue=names[i];item.FindPropertyRelative("description").stringValue=notes[i];
            item.FindPropertyRelative("button").objectReferenceValue=buttons[i];item.FindPropertyRelative("selectionFrame").objectReferenceValue=frames[i];item.FindPropertyRelative("selectedLabel").objectReferenceValue=labels[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        for(int i=start.onClick.GetPersistentEventCount()-1;i>=0;i--)
            if(start.onClick.GetPersistentTarget(i)==loader)UnityEventTools.RemovePersistentListener(start.onClick,i);
        UnityEventTools.AddPersistentListener(start.onClick,controller.RequestStart);
        window.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;
    }
    private static Image ImageOf(RectTransform rect,Color color,bool raycast)
    {
        var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return image;
    }
    private static TMP_Text Text(string name,Transform parent,Vector2 pos,Vector2 size,string value,float fontSize,Color color)
    {
        var rect=Rect(name,parent,pos,size);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;
        text.fontSize=fontSize;text.color=color;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;
    }
    private static Button ButtonOf(string name,Transform parent,Vector2 pos,Vector2 size,string label,float fontSize)
    {
        var rect=Rect(name,parent,pos,size);var image=ImageOf(rect,new Color(.29f,.23f,.14f),true);Border(rect,Gold,2);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;SetColors(button);Text("Label",rect,Vector2.zero,size,label,fontSize,new Color(.96f,.92f,.8f));return button;
    }
    private static void SetColors(Button button)
    {
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.5f,1.4f,1.15f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.8f,.7f,.5f);colors.disabledColor=new Color(.45f,.45f,.45f);button.colors=colors;
    }
    private static void Border(RectTransform parent,Color color,float thickness)
    {
        var size=parent.sizeDelta;
        ImageOf(Rect("Top",parent,new Vector2(0,(size.y-thickness)/2),new Vector2(size.x,thickness)),color,false);
        ImageOf(Rect("Bottom",parent,new Vector2(0,-(size.y-thickness)/2),new Vector2(size.x,thickness)),color,false);
        ImageOf(Rect("Left",parent,new Vector2(-(size.x-thickness)/2,0),new Vector2(thickness,size.y)),color,false);
        ImageOf(Rect("Right",parent,new Vector2((size.x-thickness)/2,0),new Vector2(thickness,size.y)),color,false);
    }
}
#endif
