using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 報酬カードシステム（データ駆動版）。
/// BonusCardDataSOリストに基づいて動的にカード生成＆効果適用。
/// </summary>
public class BonusCardsManager : MonoBehaviour
{
    [Header("ボーナスカードデータ")]
    public List<BonusCardDataSO> cardDataList;

    [Header("カードを表示する親オブジェクト")]
    public RectTransform cardParent;
    [SerializeField] private GameObject inputBlocker;

    public bool IsChoosingReward { get; private set; }

    [Header("参照")]
    public TransitionManager transition;
    public TransitionManager transitionManager;
    public TextMeshProUGUI stageNumText;

    [Header("効果音")]
    public AudioClip clickSound;
    private AudioSource audioSource;

    private List<GameObject> spawnedCards = new List<GameObject>();
    private readonly HashSet<int> offeredCardIndices = new HashSet<int>();
    private UniTaskCompletionSource cardSelectionCompletion;
    private GameObject previousSelection;
    private readonly List<RewardCardVisual> cardVisuals = new List<RewardCardVisual>();
    private Button lastSelectedCard;
    private Button focusedCard;

    private sealed class RewardCardVisual
    {
        public Button Button;
        public Vector3 OriginalScale;
        public Tween ScaleTween;
    }

    private void Awake()
    {
        if (inputBlocker != null) inputBlocker.SetActive(false);
    }

    private void OnDisable()
    {
        UniTaskCompletionSource pending = cardSelectionCompletion;
        cardSelectionCompletion = null;
        FinishRewardSelection(false);
        pending?.TrySetCanceled();
    }

    private void LateUpdate()
    {
        if (!IsChoosingReward || GearCraft.Gallery.GalleryNavigation.IsOpen) return;

        EventSystem events = EventSystem.current;
        if (events == null || !events.isActiveAndEnabled || events.alreadySelecting) return;

        GameObject selectedObject = events.currentSelectedGameObject;
        Button selectedCard = null;
        foreach (RewardCardVisual visual in cardVisuals)
        {
            Button button = visual.Button;
            if (button == null || !button.IsActive() || !button.IsInteractable()) continue;
            if (selectedObject != null &&
                (selectedObject == button.gameObject || selectedObject.transform.IsChildOf(button.transform)))
            {
                selectedCard = button;
                break;
            }
        }

        // A click on the blocking background clears UI selection. Restore it after
        // the input module has processed the click, without taking another UI's focus.
        if (selectedObject == null)
        {
            selectedCard = lastSelectedCard;
            if (selectedCard == null || !selectedCard.IsActive() || !selectedCard.IsInteractable())
            {
                selectedCard = null;
                foreach (RewardCardVisual visual in cardVisuals)
                {
                    if (visual.Button == null || !visual.Button.IsActive() || !visual.Button.IsInteractable()) continue;
                    selectedCard = visual.Button;
                    break;
                }
            }
            if (selectedCard != null) events.SetSelectedGameObject(selectedCard.gameObject);
        }

        if (selectedCard != null) lastSelectedCard = selectedCard;
        SetFocusedCard(selectedCard);
    }

    private void SetFocusedCard(Button selectedCard)
    {
        if (focusedCard == selectedCard) return;
        focusedCard = selectedCard;
        foreach (RewardCardVisual visual in cardVisuals)
        {
            visual.ScaleTween?.Kill();
            visual.ScaleTween = null;
            if (visual.Button == null) continue;
            Vector3 targetScale = visual.OriginalScale * (visual.Button == selectedCard ? 1.05f : 1f);
            visual.ScaleTween = visual.Button.transform.DOScale(targetScale, 0.15f)
                .SetEase(Ease.OutQuad).SetUpdate(true);
        }
    }

    async void Start()
    {
        if (stageNumText != null)
        {
            stageNumText.DOFade(1, 0.1f);
            int stageCount = StageCounter.Instance != null ? StageCounter.Instance.StageCount : 1;
            stageNumText.text = $"DAY {(stageCount / 3) + 1}";
        }

        if (transition != null)
            transition.PlayTransition(1);

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3f), cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }
        ClearText();

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
    }

    /// <summary>
    /// 重み付きランダムで3枚選んで表示
    /// </summary>
    public void ShowBonusCards()
    {
        ShowBonusCardsAsync().Forget();
    }

    public async UniTask ShowBonusCardsAsync()
    {
        var destroyToken = this.GetCancellationTokenOnDestroy();
        if (cardSelectionCompletion != null)
        {
            await cardSelectionCompletion.Task.AttachExternalCancellation(destroyToken);
            return;
        }

        // Victory is decided by StageFlowManager after the final boss is defeated.
        ClearText();
        ClearCards();
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var completion = new UniTaskCompletionSource();
        cardSelectionCompletion = completion;
        IsChoosingReward = true;
        if (inputBlocker != null) inputBlocker.SetActive(true);

        try
        {
            cardParent.anchorMin = new Vector2(0.5f, 0.5f);
            cardParent.anchorMax = new Vector2(0.5f, 0.5f);
            cardParent.pivot = new Vector2(0.5f, 0.5f);
            cardParent.DOKill();
            cardParent.DOAnchorPos(Vector2.zero, 0.5f).SetEase(Ease.OutQuad);

            var buttons = new List<Button>();
            List<int> selectedIndices = WeightedRandomSelect(3);
            foreach (int i in selectedIndices)
            {
                var cardData = cardDataList[i];
                GameObject card = Instantiate(cardData.cardPrefab, cardParent);
                Button btn = card.GetComponent<Button>();
                int cardIndex = i;
                if (btn != null)
                {
                    offeredCardIndices.Add(cardIndex);
                    btn.onClick.AddListener(() => OnBonusCardSelected(cardIndex));
                    buttons.Add(btn);
                    cardVisuals.Add(new RewardCardVisual
                    {
                        Button = btn,
                        OriginalScale = btn.transform.localScale
                    });
                }
                spawnedCards.Add(card);
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                button.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = buttons[Mathf.Max(0, i - 1)],
                    selectOnRight = buttons[Mathf.Min(buttons.Count - 1, i + 1)],
                    selectOnUp = button,
                    selectOnDown = button
                };
            }

            if (buttons.Count > 0)
            {
                lastSelectedCard = buttons[0];
                buttons[0].Select();
                SetFocusedCard(buttons[0]);
            }
            else
                completion.TrySetResult();

            await completion.Task.AttachExternalCancellation(destroyToken);
        }
        finally
        {
            if (ReferenceEquals(cardSelectionCompletion, completion))
            {
                cardSelectionCompletion = null;
                FinishRewardSelection();
            }
        }
    }

    private List<int> WeightedRandomSelect(int n)
    {
        List<int> result = new List<int>();
        List<int> pool = new List<int>();
        for (int i = 0; i < cardDataList.Count; i++)
        {
            if (CanOfferCard(cardDataList[i]))
            {
                pool.Add(i);
            }
        }

        for (int pick = 0; pick < n && pool.Count > 0; pick++)
        {
            float totalWeight = 0f;
            foreach (int idx in pool) totalWeight += cardDataList[idx].weight;
            float r = UnityEngine.Random.Range(0f, totalWeight);
            float accum = 0f;
            foreach (int idx in pool)
            {
                accum += cardDataList[idx].weight;
                if (r <= accum)
                {
                    result.Add(idx);
                    pool.Remove(idx);
                    break;
                }
            }
        }
        return result;
    }

    private bool CanOfferCard(BonusCardDataSO cardData)
    {
        if (cardData == null || cardData.cardPrefab == null || cardData.weight <= 0f)
        {
            return false;
        }

        StatusManager status = StatusManager.Instance;
        if (cardData.selectableOnce && status != null && status.IsUniqueBonusCardSelected(cardData.uniqueSelectionId))
        {
            return false;
        }

        if (cardData.requiredWeaponNames == null || cardData.requiredWeaponNames.Length == 0)
        {
            return true;
        }

        string currentWeaponName = status != null && status.currentWeapon != null ? status.currentWeapon.weaponName : null;
        if (string.IsNullOrEmpty(currentWeaponName))
        {
            return false;
        }

        for (int i = 0; i < cardData.requiredWeaponNames.Length; i++)
        {
            if (currentWeaponName == cardData.requiredWeaponNames[i])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// カード選択時：データ駆動で効果を適用
    /// </summary>
    private void OnBonusCardSelected(int cardIndex)
    {
        if (!IsChoosingReward || cardSelectionCompletion == null || !offeredCardIndices.Contains(cardIndex))
            return;

        // Claim the choice before applying effects; Destroy is deferred until the end of the frame.
        IsChoosingReward = false;
        StageFlowManager.BlockPlayerInputForCurrentFrame();
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        var cardData = cardDataList[cardIndex];

        // 全効果を適用
        foreach (var effect in cardData.effects)
        {
            ApplyCardEffect(effect);
        }

        if (cardData.selectableOnce && StatusManager.Instance != null)
        {
            StatusManager.Instance.MarkUniqueBonusCardSelected(cardData.uniqueSelectionId);
        }

        UniTaskCompletionSource completed = cardSelectionCompletion;
        cardSelectionCompletion = null;
        FinishRewardSelection();
        completed.TrySetResult();

        // ステージ進行はStageFlowManagerに委譲
        // (StageFlowManagerが次ステージ開始 or 宿舎遷移を判断する)
    }

    /// <summary>
    /// カード効果を適用する
    /// </summary>
    private void ApplyCardEffect(CardEffect effect)
    {
        var status = StatusManager.Instance;
        if (status == null) return;

        switch (effect.type)
        {
            case CardEffectType.AddHP:
                status.HP = Mathf.Clamp(status.HP + (int)effect.value, 0, 100);
                break;

            case CardEffectType.AddGateHP:
                status.GATE += effect.value;
                break;

            case CardEffectType.AddSTR:
                status.STR += (int)effect.value;
                break;

            case CardEffectType.AddACC:
                status.ACC += (int)effect.value;
                break;

            case CardEffectType.AddMaterial:
                MaterialManager.Instance?.AddMaterial(effect.materialType, (int)effect.value);
                break;

            case CardEffectType.GrantWeapon:
                if (effect.weaponToGrant != null)
                {
                    status.AcquireWeapon(effect.weaponToGrant);
                    status.EquipWeapon(effect.weaponToGrant);
                }
                break;

            case CardEffectType.GrantUpgradePart:
                if (effect.partToGrant != null && UpgradeGridManager.Instance != null)
                {
                    int inventoryIndex = status.AcquireUpgradePart(effect.partToGrant);
                    var gridUI = UpgradeGridManager.Instance.gridUI;
                    if (gridUI != null)
                        gridUI.SelectPart(effect.partToGrant, inventoryIndex);
                    UpgradeGridManager.Instance.RefreshUI();
                }
                break;

            case CardEffectType.MagnetRangeUp:
                status.magnetRange += effect.value;
                break;

            case CardEffectType.ConvertSTRtoACC:
                int conv1 = (int)effect.value;
                if (status.STR >= conv1)
                {
                    status.STR -= conv1;
                    status.ACC += conv1;
                }
                break;

            case CardEffectType.ConvertACCtoSTR:
                int conv2 = (int)effect.value;
                if (status.ACC >= conv2)
                {
                    status.ACC -= conv2;
                    status.STR += conv2;
                }
                break;

            case CardEffectType.GearCraftAxeSizeMultiplier:
                status.gearCraftAxeSizeMultiplier *= Mathf.Max(0.01f, effect.value);
                break;

            case CardEffectType.GearCraftSwordMoveSpeedBonus:
                status.gearCraftSwordMoveSpeedBonus += effect.value;
                break;

            case CardEffectType.GearCraftTransformGainGear:
                status.gearCraftTransformGearGain += Mathf.Max(0, (int)effect.value);
                break;

            case CardEffectType.SteamCannonBulletSpeedMultiplier:
                status.steamCannonBulletSpeedMultiplier *= Mathf.Max(0.01f, effect.value);
                break;

            case CardEffectType.SteamCannonExplosionRadiusMultiplier:
                status.steamCannonExplosionRadiusMultiplier *= Mathf.Max(0.01f, effect.value);
                break;

            case CardEffectType.SteamCannonGiantBulletChance:
                status.steamCannonGiantBulletChance = Mathf.Clamp01(status.steamCannonGiantBulletChance + effect.value);
                break;

            case CardEffectType.SteamCannonDirectHitKnockback:
                status.steamCannonDirectHitKnockback += Mathf.Max(0f, effect.value);
                break;

            case CardEffectType.SteamThrowerOverheatSlipDamage:
                status.steamThrowerOverheatSlipDamage += Mathf.Max(0f, effect.value);
                break;

            case CardEffectType.SteamThrowerNoBulletGravity:
                status.steamThrowerNoBulletGravity = true;
                break;

            case CardEffectType.SteamThrowerBoostDamageMultiplier:
                status.steamThrowerBoostDamageMultiplier *= Mathf.Max(1f, effect.value);
                break;

            case CardEffectType.SteamThrowerBoostBurnDrops:
                status.steamThrowerBoostBurnDrops = true;
                break;

            case CardEffectType.RailCraftBulletSizeMultiplier:
                status.railCraftBulletSizeMultiplier *= Mathf.Max(0.01f, effect.value);
                break;

            case CardEffectType.RailCraftApplyOverheat:
                status.railCraftApplyOverheat = true;
                break;

            case CardEffectType.RailCraftRicochetMultiplier:
                status.railCraftRicochetMultiplier *= Mathf.Max(1f, effect.value);
                break;

            case CardEffectType.SteamGatlingBulletSpeedMultiplier:
                status.steamGatlingBulletSpeedMultiplier *= Mathf.Max(0.01f, effect.value);
                break;

            case CardEffectType.SteamGatlingBossDamageMultiplier:
                status.steamGatlingBossDamageMultiplier *= Mathf.Max(0.01f, effect.value);
                break;

            case CardEffectType.SteamGatlingNormalDamageMultiplier:
                status.steamGatlingNormalDamageMultiplier *= Mathf.Max(0.01f, effect.value);
                break;

            case CardEffectType.SteamGatlingDownwardRecoil:
                status.steamGatlingDownwardRecoil = true;
                break;
        }
    }

    private void FinishRewardSelection(bool animate = true)
    {
        IsChoosingReward = false;
        EventSystem events = EventSystem.current;
        if (events != null && events.isActiveAndEnabled && !events.alreadySelecting)
        {
            GameObject selected = events.currentSelectedGameObject;
            bool selectedReward = spawnedCards.Exists(card => card != null && selected != null &&
                (selected == card || selected.transform.IsChildOf(card.transform)));
            if (selected == null || selectedReward)
            {
                Selectable previous = previousSelection != null ? previousSelection.GetComponent<Selectable>() : null;
                events.SetSelectedGameObject(previous != null && previous.IsActive() && previous.IsInteractable()
                    ? previous.gameObject : null);
            }
        }
        previousSelection = null;
        ClearCards(animate);
    }

    private void ClearCards(bool animate = true)
    {
        foreach (RewardCardVisual visual in cardVisuals)
        {
            visual.ScaleTween?.Kill();
            if (visual.Button != null) visual.Button.transform.localScale = visual.OriginalScale;
        }
        cardVisuals.Clear();
        lastSelectedCard = null;
        focusedCard = null;
        foreach (var card in spawnedCards)
        {
            if (card == null) continue;
            card.SetActive(false);
            Destroy(card);
        }
        spawnedCards.Clear();
        offeredCardIndices.Clear();
        if (inputBlocker != null) inputBlocker.SetActive(false);
        if (cardParent != null)
        {
            cardParent.DOKill();
            if (animate)
                cardParent.DOAnchorPos(new Vector2(0, -10), 0.5f).SetEase(Ease.InQuad);
        }
    }

    private void ClearText()
    {
        if (stageNumText != null)
            stageNumText.text = "";
    }
}
