using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
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

    [Header("参照")]
    public TransitionManager transition;
    public TransitionManager transitionManager;
    public TextMeshProUGUI stageNumText;

    [Header("効果音")]
    public AudioClip clickSound;
    private AudioSource audioSource;

    private List<GameObject> spawnedCards = new List<GameObject>();

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

        await UniTask.Delay(TimeSpan.FromSeconds(3f));
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
    public async void ShowBonusCards()
    {
        int stageCount = StageCounter.Instance != null ? StageCounter.Instance.StageCount : 0;

        // エンディング判定
        if (stageCount >= 31)
        {
            bool trueEnd = StatusManager.Instance != null && StatusManager.Instance.killAllEnemies;
            if (transitionManager != null)
                transitionManager.PlayTransition(3);
            await UniTask.Delay(TimeSpan.FromSeconds(2f));
            EndingManager.LoadEndingScene(trueEnd ? 3 : 2);
            return;
        }

        ClearText();
        ClearCards();

        cardParent.anchorMin = new Vector2(0.5f, 0.5f);
        cardParent.anchorMax = new Vector2(0.5f, 0.5f);
        cardParent.pivot = new Vector2(0.5f, 0.5f);
        cardParent.DOAnchorPos(Vector2.zero, 0.5f).SetEase(Ease.OutQuad);

        List<int> selectedIndices = WeightedRandomSelect(3);
        foreach (int i in selectedIndices)
        {
            var cardData = cardDataList[i];
            GameObject card = Instantiate(cardData.cardPrefab, cardParent);
            Button btn = card.GetComponent<Button>();
            int cardIndex = i;
            if (btn != null)
                btn.onClick.AddListener(() => OnBonusCardSelected(cardIndex));
            spawnedCards.Add(card);
        }
    }

    private List<int> WeightedRandomSelect(int n)
    {
        List<int> result = new List<int>();
        List<int> pool = new List<int>();
        for (int i = 0; i < cardDataList.Count; i++) pool.Add(i);

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

    /// <summary>
    /// カード選択時：データ駆動で効果を適用
    /// </summary>
    private void OnBonusCardSelected(int cardIndex)
    {
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        var cardData = cardDataList[cardIndex];

        // 全効果を適用
        foreach (var effect in cardData.effects)
        {
            ApplyCardEffect(effect);
        }

        ClearCards();

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
                status.HP += (int)effect.value;
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

            case CardEffectType.RepairDurability:
                status.RepairDurability((int)effect.value);
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
        }
    }

    private void ClearCards()
    {
        foreach (var card in spawnedCards)
            Destroy(card);
        spawnedCards.Clear();
        cardParent.DOAnchorPos(new Vector2(0, -10), 0.5f).SetEase(Ease.InQuad);
    }

    private void ClearText()
    {
        if (stageNumText != null)
            stageNumText.text = "";
    }
}
