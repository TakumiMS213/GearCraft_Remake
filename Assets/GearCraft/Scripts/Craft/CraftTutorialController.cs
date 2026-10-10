using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GearCraft.Scripts.Craft
{
    public sealed class CraftTutorialController : MonoBehaviour
    {
        private const int CraftTab = 0;
        private const int WeaponCustomTab = 1;
        private const string DefaultTutorialDataResourcePath = "GearCraft/CraftTutorialData";
        private const float HintOfferDelay = 12f;
        private Canvas canvas;
        private CanvasGroup messageGroup;
        private RectTransform canvasRect, messagePanel;
        private readonly List<RectTransform[]> highlightFrames = new List<RectTransform[]>();
        private readonly List<Rect> highlightBounds = new List<Rect>();
        private TMP_Text stateText, titleText, bodyText, pageText, confirmText;
        private Button nextButton, closeButton, hintButton, practiceButton, affordableButton, shortageButton;
        private TMP_FontAsset fontAsset;
        private GameObject tutorialRoot, confirmRoot;
        private int currentTab, openTab;
        private readonly bool[] autoStarted = new bool[2];
        private Coroutine autoStartCoroutine;
        private CraftTutorialStepData[] currentSteps;
        private CraftTutorialDataSO tutorialData;
        private CraftManager craftManager;
        private UpgradeShopManager upgradeShopManager;
        private UpgradeGridManager upgradeGridManager;
        private CraftRecipeSO tutorialScrapModuleRecipe;
        private UpgradePartSO tutorialDamagePart;
        private readonly CraftTutorialRunProgress fallbackProgress = new CraftTutorialRunProgress();
        private CraftTutorialTrackProgress progress;
        private string notice = "";
        private float segmentSeconds;
        private bool referenceOnly;
        private bool compactTask;
        private int referenceStepIndex;

        private CraftTutorialRunProgress RunProgress => StatusManager.Instance != null
            ? StatusManager.Instance.CraftTutorials : fallbackProgress;
        private int StepIndex => referenceOnly ? referenceStepIndex : progress?.stepIndex ?? 0;
        private CraftTutorialStepData Step => currentSteps != null && progress != null &&
            StepIndex >= 0 && StepIndex < currentSteps.Length ? currentSteps[StepIndex] : null;

        private void Awake()
        {
            EnsureLayout();
            HideTutorial();
            HideConfirm();
        }

        private void LateUpdate()
        {
            if (!IsTutorialActive()) return;
            if (!referenceOnly && !progress.showingFeedback)
            {
                progress.elapsedSeconds += Time.unscaledDeltaTime;
                segmentSeconds += Time.unscaledDeltaTime;
                hintButton.gameObject.SetActive(progress.elapsedSeconds >= HintOfferDelay);
            }
            bool readingDetails = TooltipSystem.Instance != null && TooltipSystem.Instance.tooltipPanel != null &&
                TooltipSystem.Instance.tooltipPanel.gameObject.activeInHierarchy;
            messageGroup.alpha = readingDetails ? 0f : 1f;
            messageGroup.blocksRaycasts = !readingDetails;
            messageGroup.interactable = !readingDetails;
            if (readingDetails) { ClearHighlight(); return; }
            UpdateHighlight();
        }

        private void OnDisable() { PauseTutorial(); }
        private void OnDestroy()
        {
            PauseTutorial();
            UnsubscribeTutorialEvents();
            if (canvas != null) Destroy(canvas.gameObject);
        }

        public void Configure(CraftTutorialDataSO data) { tutorialData = data; }

        public void OnTabOpened(int tabIndex)
        {
            openTab = tabIndex;
            if (IsTutorialActive() && currentTab != tabIndex) PauseTutorial();
            if (confirmRoot != null && confirmRoot.activeSelf) HideConfirm();
            TryAutoStartTutorial(tabIndex);
        }

        public void OnCraftSceneEntered() { TryAutoStartTutorial(CraftTab); }

        private void TryAutoStartTutorial(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex > 1 || IsTutorialActive() || autoStarted[tabIndex] ||
                PlayerPrefs.GetInt(MasteryKey(tabIndex), 0) == 1) return;
            if (autoStartCoroutine != null) StopCoroutine(autoStartCoroutine);
            autoStartCoroutine = StartCoroutine(StartTutorialWhenReady(tabIndex));
        }

        private IEnumerator StartTutorialWhenReady(int tabIndex)
        {
            // Managers initialize after tab activation. Never choose a recipe on the player's behalf.
            while (openTab == tabIndex)
            {
                yield return null;
                if (openTab != tabIndex) break;
                ResolveTutorialReferences();
                bool ready = StatusManager.Instance != null && MaterialManager.Instance != null &&
                    (tabIndex == CraftTab ? craftManager != null && craftManager.IsReadyForTutorial
                        : upgradeShopManager != null && upgradeGridManager != null && upgradeGridManager.GridSize > 0);
                if (!ready) continue;
                autoStartCoroutine = null;
                StartTutorial(tabIndex);
                break;
            }
            autoStartCoroutine = null;
        }

        private static string MasteryKey(int tabIndex) => tabIndex == CraftTab
            ? CraftTutorialMetrics.CraftMasteryKey : CraftTutorialMetrics.WeaponMasteryKey;

        private void ShowConfirm()
        {
            EnsureLayout();
            confirmRoot.SetActive(true);
            confirmText.text = PlayerPrefs.GetInt(MasteryKey(currentTab), 0) == 1
                ? "作成や強化の手順を、もう一度確認しますか？"
                : "次の戦いに向けて、作成と強化の手順を確かめましょう。\n中断したところから続けられます。";
        }

        private void StartTutorial(int tabIndex)
        {
            if (autoStartCoroutine != null) { StopCoroutine(autoStartCoroutine); autoStartCoroutine = null; }
            currentTab = tabIndex;
            autoStarted[tabIndex] = true;
            ResolveTutorialReferences();
            currentSteps = GetSteps(tabIndex);
            progress = tabIndex == CraftTab ? RunProgress.craft : RunProgress.weaponCustom;
            // Only ownership is reconciled on resume. Retain the original upgrade baseline so
            // removing and replacing an existing part cannot pass an independent challenge.
            RememberOwnedRecipes();
            referenceOnly = progress.mastered || PlayerPrefs.GetInt(MasteryKey(tabIndex), 0) == 1 ||
                (tabIndex == CraftTab && AllRecipesOwned());
            referenceStepIndex = 0;
            compactTask = false;
            HideConfirm();
            tutorialRoot.SetActive(true);
            if (!referenceOnly) SubscribeTutorialEvents();
            else UnsubscribeTutorialEvents();
            notice = "";
            segmentSeconds = 0f;
            if (!referenceOnly && !progress.showingFeedback) PrepareCurrentStep();
            ShowCurrentStep();
        }

        private CraftTutorialStepData[] GetSteps(int tabIndex)
        {
            if (tutorialData == null) tutorialData = Resources.Load<CraftTutorialDataSO>(DefaultTutorialDataResourcePath);
            var steps = tabIndex == WeaponCustomTab ? tutorialData?.WeaponCustomSteps : tutorialData?.CraftSteps;
            if (steps != null && steps.Length > 0) return steps;
            return tabIndex == WeaponCustomTab ? CraftTutorialDataSO.CreateDefaultWeaponCustomSteps()
                : CraftTutorialDataSO.CreateDefaultCraftSteps();
        }

        private void ResolveTutorialReferences()
        {
            craftManager = craftManager != null ? craftManager : FindFirstObjectByType<CraftManager>();
            upgradeShopManager = upgradeShopManager != null ? upgradeShopManager : FindFirstObjectByType<UpgradeShopManager>();
            upgradeGridManager = UpgradeGridManager.Instance;
            tutorialScrapModuleRecipe = craftManager != null ? craftManager.FindUnlockedRecipe("ScrapModule") : null;
            tutorialDamagePart = upgradeShopManager != null ? upgradeShopManager.FindPart("Damage+1") : null;
        }

        private void PrepareCurrentStep()
        {
            var status = StatusManager.Instance;
            if (Step == null || status == null) return;
            // Completed real outcomes survive leaving the workbench, even if they happened while guidance was paused.
            if (currentTab == CraftTab && progress.stepIndex < 2 && status.module_scrap)
            {
                progress.stepIndex = 2;
                ResetAttempt();
                notice = "素材を集める装置は、すでに稼働しています。次の備えを考えましょう。";
            }
            if (currentTab == WeaponCustomTab && progress.stepIndex == 0 && upgradeGridManager != null)
            {
                var placed = upgradeGridManager.PlacedParts.Find(p => HasEffect(p.partData, UpgradeEffectType.DamageFlat));
                if (placed != null)
                {
                    progress.practicePartName = placed.partData.name;
                    progress.stepIndex = 1;
                    ResetAttempt();
                    notice = "一撃の威力は上がっています。別のパーツで、さらに改良しましょう。";
                }
            }
            if (currentTab == WeaponCustomTab)
            {
                upgradeShopManager.EnsureLineupContains(tutorialDamagePart);
                if (Step.Independent)
                {
                    upgradeShopManager.EnsureLineupContains(upgradeShopManager.FindPart("Damage+2"));
                    upgradeShopManager.EnsureLineupContains(upgradeShopManager.FindPart("SpeedGear"));
                }
                bool supplied = Step.Independent ? progress.challengeSuppliesGranted : progress.practiceSuppliesGranted;
                if (!supplied)
                {
                    int amount = Step.Independent ? 6 : 3;
                    string suppliedText = GrantMissingCosts(new[] {new CraftCost {type = MaterialManager.MaterialType.Scrap, amount = amount}});
                    if (Step.Independent) progress.challengeSuppliesGranted = true;
                    else progress.practiceSuppliesGranted = true;
                    notice = suppliedText;
                }
            }
            else if (Step.Independent && !progress.challengeSuppliesGranted)
            {
                string suppliedText = GrantMissingCosts(new[] {new CraftCost {type = MaterialManager.MaterialType.Gear, amount = 4}});
                progress.challengeSuppliesGranted = true;
                notice = suppliedText;
            }
            if (!progress.baselineCaptured)
            {
                CaptureBaseline();
                Record("started");
            }
        }

        private void CaptureBaseline()
        {
            progress.baselineCaptured = true;
            var status = StatusManager.Instance;
            progress.baselineDamage = status != null ? status.bonusDamage : 0f;
            progress.baselineAttackSpeed = status != null ? status.attackSpeedMult : 1f;
            progress.baselineParts.Clear();
            if (upgradeGridManager != null)
                foreach (var item in upgradeGridManager.PlacedParts) progress.baselineParts.Add(item.partData);
            RememberOwnedRecipes();
        }

        private void RememberOwnedRecipes()
        {
            if (craftManager != null && craftManager.recipes != null)
                foreach (var recipe in craftManager.recipes)
                    if (RecipeAlreadyOwned(recipe) && !progress.craftedRecipeIds.Contains(RecipeId(recipe)))
                        progress.craftedRecipeIds.Add(RecipeId(recipe));
        }

        private bool AllRecipesOwned()
        {
            if (craftManager == null || craftManager.recipes == null || craftManager.recipes.Count == 0) return false;
            foreach (var recipe in craftManager.recipes)
                if (recipe != null && !progress.craftedRecipeIds.Contains(RecipeId(recipe))) return false;
            return true;
        }

        private static bool RecipeAlreadyOwned(CraftRecipeSO recipe)
        {
            var status = StatusManager.Instance;
            if (recipe == null || status == null) return false;
            if (recipe.resultType == CraftResultType.PunkDrive) return status.punkDrive;
            if (recipe.resultType == CraftResultType.Weapon) return recipe.weaponResult != null && status.ownedWeapons.Contains(recipe.weaponResult);
            switch (recipe.moduleFlag)
            {
                case "module_scrap": return status.module_scrap;
                case "module_repair": return status.module_repair;
                case "module_barrier": return status.module_barrier;
                default: return false;
            }
        }

        private static string RecipeId(CraftRecipeSO recipe) => recipe != null ? recipe.UnlockId : "";

        private void ShowCurrentStep()
        {
            if (Step == null) { HideTutorial(); return; }
            if (referenceOnly)
            {
                stateText.text = "必要なときに、ここで手順を確認できます";
                titleText.text = Step.Title;
                bodyText.text = Step.Hint1 + "\n" + Step.Hint2;
                pageText.text = $"手引き {referenceStepIndex + 1}/{currentSteps.Length}";
                affordableButton.gameObject.SetActive(false);
                shortageButton.gameObject.SetActive(false);
                hintButton.gameObject.SetActive(false);
                practiceButton.gameObject.SetActive(false);
                closeButton.gameObject.SetActive(true);
                closeButton.GetComponentInChildren<TMP_Text>().text = "閉じる";
                closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-184f, 20f);
                nextButton.gameObject.SetActive(true);
                nextButton.GetComponentInChildren<TMP_Text>().text = referenceStepIndex + 1 < currentSteps.Length ? "次へ" : "閉じる";
                FitMessagePanel();
                UpdateHighlight();
                return;
            }
            closeButton.GetComponentInChildren<TMP_Text>().text = "あとで";
            stateText.text = progress.showingFeedback ? "次の備えにつなげましょう" : compactTask
                ? "作業を進めてください。終わったら、結果を確認しましょう" : "話を聞きながら、そのまま作業を進めて構いません";
            titleText.text = progress.showingFeedback ? "変化を確かめましょう" : Step.Title;
            string body = progress.showingFeedback ? progress.feedback : Step.Body;
            if (!progress.showingFeedback)
            {
                string hint = progress.hintLevel == 1 ? Step.Hint1 : progress.hintLevel == 2 ? Step.Hint2 : progress.hintLevel >= 3 ? Step.Hint3 : "";
                if (!string.IsNullOrEmpty(hint)) body = hint;
                if (!string.IsNullOrEmpty(notice))
                    body = progress.invalidAttempts > 0 ? notice : body + "\n" + notice;
                if (progress.hintLevel > 0 && currentTab == WeaponCustomTab && !HasSpaceForUpgrade())
                    body += "\n空きがなければ、装着済みのパーツを押して外せます。材料も戻ります。";
                if (Step.Independent && currentTab == CraftTab && !HasNewRecipeAvailable())
                    body = "今の手持ちでは、次に作れるものがありません。防衛で新しいレシピや武器を得てから、続きを進めましょう。";
            }
            bodyText.text = body;
            pageText.text = $"{progress.stepIndex + 1}/{currentSteps.Length}";
            bool assess = !progress.showingFeedback && Step.WaitCondition == CraftTutorialWaitCondition.AssessCraftMaterials;
            affordableButton.gameObject.SetActive(assess);
            shortageButton.gameObject.SetActive(assess);
            closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(assess ? -392f : progress.showingFeedback ? -184f : -256f, 14f);
            hintButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(assess ? -552f : -416f, 14f);
            practiceButton.gameObject.SetActive(!progress.showingFeedback && !assess);
            practiceButton.GetComponentInChildren<TMP_Text>().text = compactTask ? "もう一度読む" : "閉じて作業する";
            nextButton.gameObject.SetActive(progress.showingFeedback);
            nextButton.GetComponentInChildren<TMP_Text>().text = progress.mastered ? "作業に戻る" : "次へ";
            hintButton.gameObject.SetActive(!progress.showingFeedback && progress.elapsedSeconds >= HintOfferDelay);
            hintButton.interactable = progress.hintLevel < 3;
            hintButton.GetComponentInChildren<TMP_Text>().text = progress.hintLevel == 0 ? "ヒント" : $"ヒント {progress.hintLevel}/3";
            FitMessagePanel();
            UpdateHighlight();
        }

        private void FitMessagePanel()
        {
            bool compact = compactTask && !referenceOnly && !progress.showingFeedback;
            bodyText.gameObject.SetActive(!compact);
            bodyText.fontSize = 24f;
            float maxBodyHeight = !referenceOnly && !progress.showingFeedback ? (currentTab == CraftTab ? 96f : 120f) : 180f;
            float bodyHeight = Mathf.Clamp(bodyText.GetPreferredValues(bodyText.text, messagePanel.rect.width - 56f, Mathf.Infinity).y + 4f, 56f, maxBodyHeight);
            float panelHeight = compact ? 154f : bodyHeight + 158f;
            messagePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
            bodyText.rectTransform.offsetMin = new Vector2(28f, -90f - bodyHeight);
            pageText.rectTransform.offsetMin = new Vector2(28f, -panelHeight + 16f);
            pageText.rectTransform.offsetMax = new Vector2(-724f, -panelHeight + 52f);
        }

        private void TogglePracticeView()
        {
            if (!IsTutorialActive() || referenceOnly || progress.showingFeedback) return;
            compactTask = !compactTask;
            ShowCurrentStep();
        }

        public void BeginPractice()
        {
            if (!IsTutorialActive() || referenceOnly || progress.showingFeedback || compactTask ||
                Step == null || Step.WaitCondition == CraftTutorialWaitCondition.AssessCraftMaterials) return;
            compactTask = true;
            ShowCurrentStep();
        }

        private bool HasNewRecipeAvailable()
        {
            if (craftManager == null || craftManager.recipes == null) return false;
            foreach (var recipe in craftManager.recipes)
                if (recipe != null && recipe != tutorialScrapModuleRecipe && !progress.craftedRecipeIds.Contains(RecipeId(recipe)) &&
                    craftManager.FindUnlockedRecipe(RecipeId(recipe)) != null && CraftManager.MeetsRequiredWeapon(recipe)) return true;
            return false;
        }

        private bool HasSpaceForUpgrade()
        {
            if (upgradeGridManager == null || upgradeShopManager == null) return true;
            var candidates = Step.Independent ? new[] { upgradeShopManager.FindPart("Damage+2"), upgradeShopManager.FindPart("SpeedGear") }
                : new[] { tutorialDamagePart };
            foreach (var part in candidates)
            {
                if (part == null || (Step.Independent && part.name == progress.practicePartName)) continue;
                for (int y = 0; y < upgradeGridManager.GridSize; y++)
                    for (int x = 0; x < upgradeGridManager.GridSize; x++)
                        for (int rotation = 0; rotation < 4; rotation++)
                            if (upgradeGridManager.CanPlace(part, x, y, rotation)) return true;
            }
            return false;
        }

        private void ShowNextHint()
        {
            if (!IsTutorialActive() || referenceOnly || Step == null || progress.showingFeedback || progress.hintLevel >= 3) return;
            progress.hintLevel++;
            progress.usedHint = true;
            progress.hintRequests++;
            compactTask = false;
            notice = "";
            Record("hint");
            ShowCurrentStep();
        }

        private void AssessMaterials(bool canCraft)
        {
            if (!IsWaitingFor(CraftTutorialWaitCondition.AssessCraftMaterials)) return;
            if (craftManager.SelectedRecipe != tutorialScrapModuleRecipe || tutorialScrapModuleRecipe == null)
            {
                Fail("まずは、毎日素材を集めるScrapModuleを見てください。この装置の材料が足りるか確かめましょう。");
                return;
            }
            bool enough = CraftManager.HasEnoughMaterials(tutorialScrapModuleRecipe);
            if (canCraft != enough)
            {
                Fail("もう一度、手持ちの数と必要な数を比べてみてください。全ての材料がそろっていますか？");
                return;
            }
            string result = enough ? "材料はそろっています。作成すると、その分の材料を使います。"
                : "まだ材料が足りません。普段は防衛で得た素材を、次の備えに回します。";
            if (!progress.practiceSuppliesGranted)
            {
                string grant = GrantMissingCosts(tutorialScrapModuleRecipe.costs);
                progress.practiceSuppliesGranted = true;
                if (!enough) result += "\n" + grant;
            }
            Succeed(result);
        }

        private void OnRecipeCrafted(CraftRecipeSO recipe)
        {
            if (!IsTutorialActive() || Step == null || progress.showingFeedback || currentTab != CraftTab) return;
            if (IsWaitingFor(CraftTutorialWaitCondition.CraftScrapModule) && recipe == tutorialScrapModuleRecipe)
            {
                progress.craftedRecipeIds.Add(RecipeId(recipe));
                Succeed("装置が動き始めました。これからは毎日、歯車を5個ずつ確保できます。");
            }
            else if (IsWaitingFor(CraftTutorialWaitCondition.CraftDifferentRecipe))
            {
                if (recipe == tutorialScrapModuleRecipe || progress.craftedRecipeIds.Contains(RecipeId(recipe)))
                { Fail("それはすでに持っています。今回は別のものを作り、備えを増やしましょう。"); return; }
                progress.craftedRecipeIds.Add(RecipeId(recipe));
                string effect = recipe.resultType == CraftResultType.PunkDrive ? "回転する歯車が周囲の敵を攻撃します。接近されたときの助けになるでしょう。"
                    : recipe.resultType == CraftResultType.Weapon ? "完成した武器は、すでに装備しています。次の戦いで使い心地を確かめてください。" : "新しい機能が使えるようになりました。次の防衛に役立ててください。";
                Succeed($"{recipe.DisplayName}が完成しました。{effect}");
            }
            else if (IsWaitingFor(CraftTutorialWaitCondition.AssessCraftMaterials))
            {
                notice = "完成しました。次は作る前に、材料が足りるかも確かめておきましょう。";
                // Reconcile on resume rather than interpreting an unassessed action as mastery.
                if (recipe == tutorialScrapModuleRecipe) { PrepareCurrentStep(); ShowCurrentStep(); }
            }
        }

        private void OnPartPlaced(UpgradePartSO part)
        {
            if (!IsTutorialActive() || Step == null || progress.showingFeedback || currentTab != WeaponCustomTab) return;
            var status = StatusManager.Instance;
            if (status == null || part == null) return;
            bool damageUp = status.bonusDamage > progress.baselineDamage + 0.001f;
            bool faster = status.attackSpeedMult < progress.baselineAttackSpeed - 0.001f;
            if (IsWaitingFor(CraftTutorialWaitCondition.PlaceDamagePart))
            {
                if (!HasEffect(part, UpgradeEffectType.DamageFlat) || !damageUp)
                { Fail("パーツは組み込めました。今回は一撃の威力を高めたいので、攻撃力が上がるものを探しましょう。"); return; }
                progress.practicePartName = part.name;
            }
            else if (IsWaitingFor(CraftTutorialWaitCondition.ImproveWeapon))
            {
                int before = progress.baselineParts.FindAll(p => p == part).Count;
                int after = upgradeGridManager.PlacedParts.FindAll(p => p.partData == part).Count;
                if (part.name == progress.practicePartName || after <= before ||
                    !(damageUp && HasEffect(part, UpgradeEffectType.DamageFlat) || faster && HasEffect(part, UpgradeEffectType.AttackSpeedMult)))
                { Fail("先ほどとは違うパーツで、今より威力か速さを伸ばしましょう。効果を見比べてみてください。"); return; }
            }
            else return;
            string result = damageUp
                ? $"パーツが組み込まれ、一撃のダメージが{status.bonusDamage - progress.baselineDamage:0.##}増えました。"
                : "パーツが組み込まれ、次の攻撃までの待ち時間が短くなりました。";
            if (damageUp && faster) result += "\n次の攻撃までの待ち時間も短くなっています。";
            Succeed(result);
        }

        private static bool HasEffect(UpgradePartSO part, UpgradeEffectType type)
        {
            if (part == null || part.effects == null) return false;
            foreach (var effect in part.effects)
                if (effect.type == type && (type == UpgradeEffectType.AttackSpeedMult ? effect.value > 0f && effect.value < 1f : effect.value > 0f)) return true;
            return false;
        }

        private void OnCraftFailed(CraftRecipeSO recipe)
        {
            if (IsTutorialActive() && currentTab == CraftTab && !progress.showingFeedback)
                Fail(recipe == null ? "まずは、次の戦いに必要なものを選びましょう。" :
                    !CraftManager.MeetsRequiredWeapon(recipe) ? "この作成には、元になる武器の装備が必要です。指定の武器が手元にあるか確認しましょう。" : DescribeShortage(recipe.costs));
        }
        private void OnPlacementFailed(UpgradePartSO part)
        {
            if (IsTutorialActive() && currentTab == WeaponCustomTab && !progress.showingFeedback)
                Fail(MaterialManager.Instance != null && part != null && !MaterialManager.Instance.CanAfford(part.costs)
                    ? DescribeShortage(part.costs) : "その位置には収まりません。形を見て、重ならずにはめられる場所を探しましょう。");
        }
        private void Fail(string message)
        {
            compactTask = false;
            progress.invalidAttempts++;
            notice = message;
            Record("invalid");
            ShowCurrentStep();
        }

        private void Succeed(string message)
        {
            compactTask = false;
            Record("success");
            segmentSeconds = 0f;
            progress.showingFeedback = true;
            if (Step.Independent && !progress.usedHint)
            {
                progress.mastered = true;
                PlayerPrefs.SetInt(MasteryKey(currentTab), 1);
                PlayerPrefs.Save();
                message += "\n今後も、戦況に合わせて装備を整えていきましょう。";
            }
            else if (Step.Independent)
            {
                message += "\n次は案内を頼らず、別のものを選んでみてください。準備ができてからで構いません。";
            }
            progress.feedback = message;
            ShowCurrentStep();
        }

        private void NextStep()
        {
            if (IsTutorialActive() && referenceOnly)
            {
                if (++referenceStepIndex >= currentSteps.Length) HideTutorial();
                else ShowCurrentStep();
                return;
            }
            if (!IsTutorialActive() || !progress.showingFeedback) return;
            if (progress.mastered) { HideTutorial(); return; }
            if (!Step.Independent) progress.stepIndex++;
            progress.showingFeedback = false;
            notice = "";
            ResetAttempt();
            PrepareCurrentStep();
            ShowCurrentStep();
        }
        private void ResetAttempt()
        {
            compactTask = false;
            progress.hintLevel = 0;
            progress.usedHint = false;
            progress.hintRequests = 0;
            progress.invalidAttempts = 0;
            progress.elapsedSeconds = 0f;
            progress.baselineCaptured = false;
            segmentSeconds = 0f;
        }

        private void PauseTutorial()
        {
            if (autoStartCoroutine != null) { StopCoroutine(autoStartCoroutine); autoStartCoroutine = null; }
            if (IsTutorialActive() && !referenceOnly && progress != null && !progress.mastered)
            {
                Record("paused");
                segmentSeconds = 0f;
            }
            HideTutorial();
        }
        private void HideTutorial()
        {
            if (tutorialRoot != null) tutorialRoot.SetActive(false);
            UnsubscribeTutorialEvents();
            referenceOnly = false;
        }
        private void HideConfirm() { if (confirmRoot != null) confirmRoot.SetActive(false); }
        private bool IsTutorialActive() => tutorialRoot != null && tutorialRoot.activeSelf;
        private bool IsWaitingFor(CraftTutorialWaitCondition condition) => IsTutorialActive() && !referenceOnly && !progress.showingFeedback && Step != null && Step.WaitCondition == condition;
        private void Record(string outcome)
        {
            if (Step != null) CraftTutorialMetrics.Record(currentTab, Step.LearningGoal, outcome, segmentSeconds,
                progress.hintRequests, progress.invalidAttempts, Step.Independent);
        }
        private void SubscribeTutorialEvents()
        {
            UnsubscribeTutorialEvents();
            if (craftManager != null) { craftManager.RecipeCrafted += OnRecipeCrafted; craftManager.CraftFailed += OnCraftFailed; }
            if (upgradeGridManager != null) { upgradeGridManager.PartPlaced += OnPartPlaced; upgradeGridManager.PlacementFailed += OnPlacementFailed; }
        }
        private void UnsubscribeTutorialEvents()
        {
            if (craftManager != null) { craftManager.RecipeCrafted -= OnRecipeCrafted; craftManager.CraftFailed -= OnCraftFailed; }
            if (upgradeGridManager != null) { upgradeGridManager.PartPlaced -= OnPartPlaced; upgradeGridManager.PlacementFailed -= OnPlacementFailed; }
        }

        private static Dictionary<MaterialManager.MaterialType, int> SumCosts(CraftCost[] costs)
        {
            var sums = new Dictionary<MaterialManager.MaterialType, int>();
            if (costs != null) foreach (var cost in costs)
                if (cost != null && cost.amount > 0) { sums.TryGetValue(cost.type, out int amount); sums[cost.type] = amount + cost.amount; }
            return sums;
        }
        private static string DescribeShortage(CraftCost[] costs)
        {
            foreach (var cost in SumCosts(costs))
            {
                int count = MaterialManager.Instance != null ? MaterialManager.Instance.GetMaterial(cost.Key) : 0;
                if (count < cost.Value)
                    return "まだ材料が足りません。防衛で素材を集めてから、作業を続けましょう。";
            }
            return "材料はそろっています。選んだものを、もう一度確かめてください。";
        }
        private static string GrantMissingCosts(CraftCost[] costs)
        {
            bool granted = false;
            if (MaterialManager.Instance == null) return "材料を用意できませんでした。一度作業台を離れて、入り直してください。";
            foreach (var cost in SumCosts(costs))
            {
                int missing = cost.Value - MaterialManager.Instance.GetMaterial(cost.Key);
                if (missing <= 0) continue;
                MaterialManager.Instance.AddMaterial(cost.Key, missing);
                granted = true;
            }
            return granted ? "今回は、不足分の材料を用意しました。これで作業を進められます。" : "手持ちの材料で進められます。";
        }

        private void UpdateHighlight()
        {
            highlightBounds.Clear();
            if (!referenceOnly && progress != null && !progress.showingFeedback && Step != null)
            {
                string targets = progress.hintLevel >= 3 ? Step.TargetName : progress.hintLevel == 2 ? Step.AreaTargetName : "";
                ResolveHighlightBounds(targets);
                if (highlightBounds.Count == 0 && progress.hintLevel >= 3) ResolveHighlightBounds(Step.AreaTargetName);
            }
            for (int i = 0; i < highlightBounds.Count; i++)
            {
                if (i == highlightFrames.Count)
                {
                    var edges = new RectTransform[4];
                    for (int edge = 0; edge < 4; edge++)
                    {
                        var image = CreateImage($"TutorialHighlight_{i}_{edge}", tutorialRoot.transform, new Color(1f, 0.82f, 0.22f, 1f));
                        image.raycastTarget = false;
                        image.transform.SetAsFirstSibling();
                        edges[edge] = image.rectTransform;
                    }
                    highlightFrames.Add(edges);
                }
                var bounds = highlightBounds[i];
                var frame = highlightFrames[i];
                foreach (var edge in frame) edge.gameObject.SetActive(true);
                const float thickness = 4f;
                SetPanel(frame[0], bounds.xMin, bounds.yMax - thickness, bounds.width, thickness);
                SetPanel(frame[1], bounds.xMin, bounds.yMin, bounds.width, thickness);
                SetPanel(frame[2], bounds.xMin, bounds.yMin, thickness, bounds.height);
                SetPanel(frame[3], bounds.xMax - thickness, bounds.yMin, thickness, bounds.height);
            }
            for (int i = highlightBounds.Count; i < highlightFrames.Count; i++)
                foreach (var edge in highlightFrames[i]) edge.gameObject.SetActive(false);
            PlaceMessagePanel();
        }

        private void ResolveHighlightBounds(string targets)
        {
            if (canvasRect == null || string.IsNullOrWhiteSpace(targets)) return;
            foreach (string name in targets.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                RectTransform target = ResolveHighlightTarget(name.Trim());
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                Rect bounds = RectInTutorialCanvas(target);
                if (name.Trim() == "GridArea")
                {
                    bool foundCell = false;
                    foreach (RectTransform cell in target)
                    {
                        if (cell.name != "UpgradeGrid" || !cell.gameObject.activeInHierarchy) continue;
                        Rect cellBounds = RectInTutorialCanvas(cell);
                        bounds = foundCell ? Union(bounds, cellBounds) : cellBounds;
                        foundCell = true;
                    }
                    if (!foundCell) continue;
                }
                if (target.name.StartsWith("Material_", StringComparison.Ordinal))
                    foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>())
                        if (text.name == "MaterialRequirementText") bounds = Union(bounds, RectInTutorialCanvas(text.rectTransform));

                // Clip in the same canvas coordinate space as the frame, including scroll masks.
                Rect clip = canvasRect.rect;
                Canvas sourceCanvas = target.GetComponentInParent<Canvas>()?.rootCanvas;
                if (sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay && sourceCanvas.worldCamera != null)
                {
                    Rect pixels = sourceCanvas.worldCamera.pixelRect;
                    clip = Intersection(clip, RectFromPoints(ScreenPointToCanvas(pixels.min), ScreenPointToCanvas(pixels.max)));
                }
                for (Transform node = target; node != null; node = node.parent)
                {
                    bool masks = node.TryGetComponent<RectMask2D>(out var rectMask) && rectMask.isActiveAndEnabled;
                    masks |= node.TryGetComponent<Mask>(out var mask) && mask.isActiveAndEnabled;
                    if (masks && node is RectTransform maskRect) clip = Intersection(clip, RectInTutorialCanvas(maskRect));
                }
                bounds = Intersection(bounds, clip);
                if (bounds.width <= 0f || bounds.height <= 0f) continue;
                const float padding = 8f;
                bounds = Intersection(Rect.MinMaxRect(bounds.xMin - padding, bounds.yMin - padding, bounds.xMax + padding, bounds.yMax + padding), clip);
                if (bounds.width < 4f || bounds.height < 4f) continue;
                if (!highlightBounds.Contains(bounds)) highlightBounds.Add(bounds);
            }
        }

        private RectTransform ResolveHighlightTarget(string name)
        {
            if (name == "GridArea") return FindFirstObjectByType<UpgradeGridUI>()?.gridParent;
            if (name == "CraftRecipeScrollView" || name == "ShopArea")
            {
                var scroll = FindRectTransform(name == "ShopArea" ? "ShopScrollView" : name)?.GetComponent<ScrollRect>();
                if (scroll != null && scroll.viewport != null) return scroll.viewport;
            }
            return ResolveTarget(name);
        }

        private Rect RectInTutorialCanvas(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = ScreenToCanvas(target, corners[0]);
            Vector2 max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = ScreenToCanvas(target, corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        private static Rect Intersection(Rect a, Rect b)
        {
            float x = Mathf.Max(a.xMin, b.xMin), y = Mathf.Max(a.yMin, b.yMin);
            return new Rect(x, y, Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - x), Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - y));
        }
        private static Rect RectFromPoints(Vector2 a, Vector2 b) => Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        private Vector2 ScreenToCanvas(RectTransform source, Vector3 worldPosition)
        {
            Canvas sourceCanvas = source.GetComponentInParent<Canvas>()?.rootCanvas;
            Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
            return ScreenPointToCanvas(RectTransformUtility.WorldToScreenPoint(sourceCamera, worldPosition));
        }

        private Vector2 ScreenPointToCanvas(Vector2 screenPoint)
        {
            Camera tutorialCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, tutorialCamera, out Vector2 localPoint);
            return localPoint;
        }

        private void ClearHighlight()
        {
            highlightBounds.Clear();
            foreach (var frame in highlightFrames)
                foreach (var edge in frame) if (edge != null) edge.gameObject.SetActive(false);
            PlaceMessagePanel();
        }

        private void PlaceMessagePanel()
        {
            if (messagePanel == null)
            {
                return;
            }

            if (currentTab == CraftTab)
            {
                // The upper-left gap leaves recipe rows, result tooltip and material counts accessible.
                messagePanel.anchorMin = messagePanel.anchorMax = new Vector2(0f, 1f);
                messagePanel.pivot = new Vector2(0f, 1f);
                messagePanel.anchoredPosition = new Vector2(24f, -24f);
                return;
            }

            PlaceMessagePanelAtBottom();
        }

        private void PlaceMessagePanelAtBottom()
        {
            if (messagePanel == null)
            {
                return;
            }

            messagePanel.anchorMin = new Vector2(0.5f, 0f);
            messagePanel.anchorMax = new Vector2(0.5f, 0f);
            messagePanel.pivot = new Vector2(0.5f, 0f);
            messagePanel.anchoredPosition = new Vector2(0f, currentTab == WeaponCustomTab ? 90f : 36f);
        }

        private static void SetPanel(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null)
            {
                return;
            }

            width = Mathf.Max(0f, width);
            height = Mathf.Max(0f, height);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform FindRectTransform(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            RectTransform inactiveMatch = null;
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform rect = rects[i];
                if (rect == null || rect.gameObject.name != objectName)
                {
                    continue;
                }

                if (rect.gameObject.activeInHierarchy)
                {
                    return rect;
                }

                inactiveMatch ??= rect;
            }

            return inactiveMatch;
        }

        private RectTransform ResolveTarget(string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            if (targetName == "question")
            {
                return FindQuestionRectTransform(currentTab);
            }

            return FindRectTransform(targetName);
        }

        private static RectTransform FindQuestionRectTransform(int tabIndex)
        {
            RectTransform[] questions = FindRectTransforms("question");
            RectTransform inactiveMatch = null;
            for (int i = 0; i < questions.Length; i++)
            {
                RectTransform question = questions[i];
                if (question == null)
                {
                    continue;
                }

                bool isCraftQuestion = IsChildOfNamedParent(question, "CraftPanel");
                if ((tabIndex == CraftTab) != isCraftQuestion)
                {
                    continue;
                }

                if (question.gameObject.activeInHierarchy)
                {
                    return question;
                }

                inactiveMatch ??= question;
            }

            return inactiveMatch;
        }

        private static RectTransform FindActiveRectTransform(string objectName)
        {
            RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform rect = rects[i];
                if (rect != null && rect.gameObject.name == objectName && rect.gameObject.activeInHierarchy)
                {
                    return rect;
                }
            }

            return null;
        }

        private static RectTransform[] FindRectTransforms(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return Array.Empty<RectTransform>();
            }

            RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i] != null && rects[i].gameObject.name == objectName)
                {
                    count++;
                }
            }

            RectTransform[] matches = new RectTransform[count];
            int index = 0;
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i] != null && rects[i].gameObject.name == objectName)
                {
                    matches[index] = rects[i];
                    index++;
                }
            }

            return matches;
        }

        private static bool IsChildOfNamedParent(Transform child, string parentName)
        {
            Transform current = child;
            while (current != null)
            {
                if (current.gameObject.name == parentName)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void EnsureLayout()
        {
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("CraftTutorialCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            fontAsset = ResolveDotGothicFont();
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            canvasRect = canvasObject.GetComponent<RectTransform>();

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RegisterExistingQuestionButton();
            CreateTutorialRoot(canvasObject.transform);
            CreateConfirmRoot(canvasObject.transform);
        }

        private void RegisterExistingQuestionButton()
        {
            RectTransform[] questions = FindRectTransforms("question");
            for (int i = 0; i < questions.Length; i++)
            {
                RectTransform question = questions[i];
                Button button = question != null ? question.GetComponent<Button>() : null;
                if (button == null)
                {
                    continue;
                }

                int resolvedTabIndex = IsChildOfNamedParent(question, "CraftPanel") ? CraftTab : WeaponCustomTab;
                button.onClick.AddListener(() => ShowConfirm(resolvedTabIndex));
            }
        }

        private void ShowConfirm(int tabIndex)
        {
            PauseTutorial();
            currentTab = tabIndex;
            ShowConfirm();
        }

        private void CreateTutorialRoot(Transform parent)
        {
            tutorialRoot = new GameObject("CraftTutorialRoot", typeof(RectTransform));
            tutorialRoot.transform.SetParent(parent, false);
            Stretch(tutorialRoot.GetComponent<RectTransform>());

            messagePanel = CreateImage("TutorialMessagePanel", tutorialRoot.transform, new Color(0.04f, 0.04f, 0.045f, 0.96f)).rectTransform;
            messagePanel.GetComponent<Image>().raycastTarget = false;
            messageGroup = messagePanel.gameObject.AddComponent<CanvasGroup>();
            messagePanel.anchorMin = new Vector2(0.5f, 0f);
            messagePanel.anchorMax = new Vector2(0.5f, 0f);
            messagePanel.pivot = new Vector2(0.5f, 0f);
            messagePanel.anchoredPosition = new Vector2(0f, 36f);
            messagePanel.sizeDelta = new Vector2(1060f, 330f);

            stateText = CreateText("OperationState", messagePanel, 20f, FontStyles.Normal, new Vector2(28f, -10f), new Vector2(-28f, -40f));
            stateText.color = new Color(0.55f, 0.95f, 0.7f, 1f);
            titleText = CreateText("Title", messagePanel, 28f, FontStyles.Bold, new Vector2(28f, -42f), new Vector2(-28f, -86f));
            bodyText = CreateText("Body", messagePanel, 24f, FontStyles.Normal, new Vector2(28f, -90f), new Vector2(-28f, -244f));
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 20f;
            bodyText.fontSizeMax = 24f;
            pageText = CreateText("Page", messagePanel, 20f, FontStyles.Normal, new Vector2(28f, -278f), new Vector2(-724f, -314f));
            stateText.raycastTarget = titleText.raycastTarget = bodyText.raycastTarget = pageText.raycastTarget = false;

            nextButton = CreateButton("NextButton", messagePanel, "次へ", new Vector2(-24f, 14f), new Vector2(144f, 48f));
            closeButton = CreateButton("CloseButton", messagePanel, "あとで", new Vector2(-184f, 14f), new Vector2(144f, 48f));
            hintButton = CreateButton("HintButton", messagePanel, "ヒント", new Vector2(-416f, 14f), new Vector2(144f, 48f));
            practiceButton = CreateButton("PracticeButton", messagePanel, "閉じて作業する", new Vector2(-24f, 14f), new Vector2(216f, 48f));
            affordableButton = CreateButton("AffordableButton", messagePanel, "足りている", new Vector2(-24f, 14f), new Vector2(144f, 48f));
            shortageButton = CreateButton("ShortageButton", messagePanel, "足りない", new Vector2(-184f, 14f), new Vector2(192f, 48f));
            nextButton.onClick.AddListener(NextStep);
            closeButton.onClick.AddListener(PauseTutorial);
            hintButton.onClick.AddListener(ShowNextHint);
            practiceButton.onClick.AddListener(TogglePracticeView);
            affordableButton.onClick.AddListener(() => AssessMaterials(true));
            shortageButton.onClick.AddListener(() => AssessMaterials(false));
        }

        private void CreateConfirmRoot(Transform parent)
        {
            confirmRoot = new GameObject("CraftTutorialConfirmRoot", typeof(RectTransform), typeof(Image));
            confirmRoot.transform.SetParent(parent, false);
            Stretch(confirmRoot.GetComponent<RectTransform>());
            confirmRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.64f);

            RectTransform panel = CreateImage("ConfirmPanel", confirmRoot.transform, new Color(0.04f, 0.04f, 0.045f, 0.98f)).rectTransform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(820f, 300f);

            confirmText = CreateText("ConfirmText", panel, 28f, FontStyles.Bold, new Vector2(40f, -32f), new Vector2(-40f, -150f));
            confirmText.alignment = TextAlignmentOptions.Center;

            Button yesButton = CreateButton("YesButton", panel, "確認する", new Vector2(-164f, 36f), new Vector2(144f, 58f));
            Button noButton = CreateButton("NoButton", panel, "あとで", new Vector2(164f, 36f), new Vector2(144f, 58f));
            CenterButton(yesButton, new Vector2(-90f, -72f));
            CenterButton(noButton, new Vector2(90f, -72f));
            yesButton.onClick.AddListener(() => StartTutorial(currentTab));
            noButton.onClick.AddListener(HideConfirm);
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string objectName, Transform parent, float fontSize, FontStyles fontStyle, Vector2 topLeft, Vector2 bottomRight)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(topLeft.x, bottomRight.y);
            rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);

            TMP_Text text = gameObject.GetComponent<TMP_Text>();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            ApplyFont(text);
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            return text;
        }

        private static TMP_FontAsset ResolveDotGothicFont()
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_FontAsset font = texts[i] != null ? texts[i].font : null;
                if (font != null && font.name.Contains("DotGothic", StringComparison.OrdinalIgnoreCase))
                {
                    return font;
                }
            }

            return Resources.Load<TMP_FontAsset>("DotGothic16-Regular SDF");
        }

        private static void ApplyFont(TMP_Text text)
        {
            CraftTutorialController controller = FindFirstObjectByType<CraftTutorialController>();
            if (text != null && controller != null && controller.fontAsset != null)
            {
                text.font = controller.fontAsset;
            }
        }

        private static Button CreateButton(string objectName, Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = gameObject.GetComponent<Image>();
            image.color = new Color(0.86f, 0.64f, 0.18f, 1f);

            Button button = gameObject.GetComponent<Button>();
            TMP_Text text = CreateText("Text", gameObject.transform, 22f, FontStyles.Bold, Vector2.zero, Vector2.zero);
            RectTransform textRect = text.GetComponent<RectTransform>();
            Stretch(textRect);
            text.text = label;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static void CenterButton(Button button, Vector2 anchoredPosition)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

    }
}
