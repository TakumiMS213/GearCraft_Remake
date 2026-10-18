#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WeaponBonusCardGenerator
{
    private const string CardFolder = "Assets/GearCraft/ScriptableObject/BonusCard";
    private const string PrefabFolder = "Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/WeaponSpecific";
    private const string MainScenePath = "Assets/GearCraft/Scenes/Main.unity";
    private const float WeaponSpecificCardWeight = 0.25f;

    private struct CardSpec
    {
        public string Id;
        public string Weapon;
        public string Title;
        public string Description;
        public CardEffect[] Effects;

        public CardSpec(string id, string weapon, string title, string description, CardEffect[] effects)
        {
            Id = id;
            Weapon = weapon;
            Title = title;
            Description = description;
            Effects = effects;
        }
    }

    [MenuItem("GearCraft/Generate Weapon Bonus Cards")]
    public static void Generate()
    {
        EnsureFolder(PrefabFolder);

        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GearCraft/Images/main/BonusCards/BonusCard.png");
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GearCraft/DotGothic16-Regular SDF.asset");
        List<BonusCardDataSO> createdCards = new List<BonusCardDataSO>();

        foreach (CardSpec spec in GetSpecs())
        {
            WeaponDataSO weapon = LoadWeapon(spec.Weapon);
            string assetPath = $"{CardFolder}/Weapon_{spec.Id}.asset";
            string prefabPath = $"{PrefabFolder}/BonusCard_{spec.Id}.prefab";

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                prefab = CreatePrefab(prefabPath, spec, weapon, background, font);
            }
            else
            {
                UpdatePrefab(prefab, spec, weapon, background, font);
            }

            BonusCardDataSO card = AssetDatabase.LoadAssetAtPath<BonusCardDataSO>(assetPath);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<BonusCardDataSO>();
                AssetDatabase.CreateAsset(card, assetPath);
            }

            card.cardName = spec.Title;
            card.cardImage = weapon != null ? weapon.icon : null;
            card.cardPrefab = prefab;
            card.weight = WeaponSpecificCardWeight;
            card.requiredWeaponNames = spec.Weapon == "GearCraft"
                ? new[] { "GearCraft", "GearCraft_Sword", "GearCraft_Axe" }
                : new[] { spec.Weapon };
            card.selectableOnce = true;
            card.uniqueSelectionId = spec.Id;
            card.effects = spec.Effects;
            EditorUtility.SetDirty(card);
            createdCards.Add(card);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RegisterCardsInMainScene(createdCards);
        Debug.Log($"Generated weapon bonus cards: {createdCards.Count}");
    }

    private static CardSpec[] GetSpecs()
    {
        return new[]
        {
            new CardSpec("GearCraft_AxeSize", "GearCraft", "変形刃拡張", "変形後の武器サイズ拡大", new[] { Effect(CardEffectType.GearCraftAxeSizeMultiplier, 1.35f) }),
            new CardSpec("GearCraft_SwordMove", "GearCraft", "軽量駆動", "変形前の移動速度増加", new[] { Effect(CardEffectType.GearCraftSwordMoveSpeedBonus, 1.2f) }),
            new CardSpec("GearCraft_TransformGear", "GearCraft", "歯車回収機構", "変形で歯車1枚獲得", new[] { Effect(CardEffectType.GearCraftTransformGainGear, 1f) }),
            new CardSpec("SteamCannon_SlowBigExplosion", "SteamCannon", "重爆蒸気弾", "弾速半減\n爆発範囲増加", new[] { Effect(CardEffectType.SteamCannonBulletSpeedMultiplier, 0.5f), Effect(CardEffectType.SteamCannonExplosionRadiusMultiplier, 1.5f) }),
            new CardSpec("SteamCannon_GiantShot", "SteamCannon", "超巨大弾", "20%の確率で超巨大弾発射", new[] { Effect(CardEffectType.SteamCannonGiantBulletChance, 0.2f) }),
            new CardSpec("SteamCannon_Knockback", "SteamCannon", "衝撃直撃", "直撃で敵を若干後ろに下げる", new[] { Effect(CardEffectType.SteamCannonDirectHitKnockback, 0.35f) }),
            new CardSpec("SteamThrower_SlipOverheat", "SteamThrower", "焼付き蒸気", "オーバーヒートに\nスリップダメージ追加", new[] { Effect(CardEffectType.SteamThrowerOverheatSlipDamage, 0.35f) }),
            new CardSpec("SteamThrower_NoFall", "SteamThrower", "浮遊蒸気", "蒸気が落下しなくなる", new[] { Effect(CardEffectType.SteamThrowerNoBulletGravity, 1f) }),
            new CardSpec("SteamThrower_BoostBurn", "SteamThrower", "過燃焼BOOST", "BOOST中、火力1.2倍\nドロップを燃やす", new[] { Effect(CardEffectType.SteamThrowerBoostDamageMultiplier, 1.2f), Effect(CardEffectType.SteamThrowerBoostBurnDrops, 1f) }),
            new CardSpec("RailCraft_BulletSize", "RailCraft", "大口径レール", "弾丸サイズが3倍になる", new[] { Effect(CardEffectType.RailCraftBulletSizeMultiplier, 3f) }),
            new CardSpec("RailCraft_Overheat", "RailCraft", "灼熱軌条", "オーバーヒート付与", new[] { Effect(CardEffectType.RailCraftApplyOverheat, 1f) }),
            new CardSpec("RailCraft_Ricochet", "RailCraft", "反射軌道制御", "反射数が倍になる", new[] { Effect(CardEffectType.RailCraftRicochetMultiplier, 2f) }),
            new CardSpec("SteamGatling_BulletSpeed", "SteamGatling", "倍速機関", "弾速が倍になる\nWeaponCustom値に乗算", new[] { Effect(CardEffectType.SteamGatlingBulletSpeedMultiplier, 2f) }),
            new CardSpec("SteamGatling_TargetDamage", "SteamGatling", "制圧射撃", "ボスへのダメージ0.7倍\n雑魚へのダメージ1.3倍", new[] { Effect(CardEffectType.SteamGatlingBossDamageMultiplier, 0.7f), Effect(CardEffectType.SteamGatlingNormalDamageMultiplier, 1.3f) }),
            new CardSpec("SteamGatling_RecoilJump", "SteamGatling", "反動跳躍", "下向きに撃つと\n反動で飛べる", new[] { Effect(CardEffectType.SteamGatlingDownwardRecoil, 1f) }),
        };
    }

    private static CardEffect Effect(CardEffectType type, float value)
    {
        return new CardEffect { type = type, value = value };
    }

    private static WeaponDataSO LoadWeapon(string weaponName)
    {
        return AssetDatabase.LoadAssetAtPath<WeaponDataSO>($"Assets/GearCraft/ScriptableObject/WeaponData/{weaponName}.asset");
    }

    private static GameObject CreatePrefab(string prefabPath, CardSpec spec, WeaponDataSO weapon, Sprite background, TMP_FontAsset font)
    {
        GameObject root = BuildCardObject(spec, weapon, background, font);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void UpdatePrefab(GameObject prefab, CardSpec spec, WeaponDataSO weapon, Sprite background, TMP_FontAsset font)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        ApplyCardVisual(root, spec, weapon, background, font);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static GameObject BuildCardObject(CardSpec spec, WeaponDataSO weapon, Sprite background, TMP_FontAsset font)
    {
        GameObject root = new GameObject($"BonusCard_{spec.Id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(500f, 800f);
        ApplyCardVisual(root, spec, weapon, background, font);
        return root;
    }

    private static void ApplyCardVisual(GameObject root, CardSpec spec, WeaponDataSO weapon, Sprite background, TMP_FontAsset font)
    {
        Image backgroundImage = root.GetComponent<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.sprite = background;
            backgroundImage.raycastTarget = true;
        }

        Button button = root.GetComponent<Button>();
        if (button != null)
        {
            button.targetGraphic = backgroundImage;
        }

        Image icon = EnsureImage(root.transform, "WeaponIcon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(210f, 160f));
        icon.sprite = weapon != null ? weapon.icon : null;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text title = EnsureText(root.transform, "Title", font, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -275f), new Vector2(0f, 72f));
        title.text = spec.Title;
        title.fontSize = 42f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(1f, 0.96f, 0.82f, 1f);

        TMP_Text description = EnsureText(root.transform, "Description", font, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.52f), Vector2.zero, Vector2.zero);
        description.text = spec.Description;
        description.fontSize = 34f;
        description.alignment = TextAlignmentOptions.Center;
        description.color = Color.white;
        description.enableWordWrapping = true;
    }

    private static Image EnsureImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform;
            child.SetParent(parent, false);
        }

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return child.GetComponent<Image>();
    }

    private static TMP_Text EnsureText(Transform parent, string name, TMP_FontAsset font, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).transform;
            child.SetParent(parent, false);
        }

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TMP_Text text = child.GetComponent<TMP_Text>();
        text.font = font;
        text.raycastTarget = false;
        return text;
    }

    private static void RegisterCardsInMainScene(List<BonusCardDataSO> cards)
    {
        EditorSceneManager.OpenScene(MainScenePath);
        BonusCardsManager manager = Object.FindFirstObjectByType<BonusCardsManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogError("BonusCardsManager was not found in Main scene.");
            return;
        }

        if (manager.cardDataList == null)
        {
            manager.cardDataList = new List<BonusCardDataSO>();
        }

        foreach (BonusCardDataSO card in cards)
        {
            if (!manager.cardDataList.Contains(card))
            {
                manager.cardDataList.Add(card);
            }
        }

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folder).Replace("\\", "/");
        string child = System.IO.Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
