# GearCraft Remake 不足素材リスト

作成日: 2026-05-19

対象: `Assets/GearCraft` 配下の現行Unityプロジェクト

## 調査サマリ

- 参照切れのObjectReferenceは検出なし。
- `BonusCardDataSO` はMCP実装後に21件まで作成済み。
- クラフトレシピはMCP実装後に9件まで作成済み。
- `MaterialManager.MaterialType` 6種類に対して、地面に落ちるドロップPrefabも6種類まで作成済み。
- 最終ボスはデータと専用Prefabを追加済み。現時点では既存AR-227画像を流用している。
- 旧RandomEnemyアセット3件は、対応スクリプトが見つからずUnity上でロードできない。

## 最優先で足りない素材

| 優先度 | 不足素材 | 現状 | 必要なもの |
|---|---|---|---|
| 一部済 | Final Boss専用敵素材 | `FinalBoss.prefab` はMCP実装済み。見た目は既存AR-227素材を流用 | 残りは専用スプライト/専用Animator/Animation |
| 済 | ModuleCoreドロップPrefab | MCP実装済み | `Drop_ModuleCore_lv1/2/3.prefab` を作成し、`Main.unity` の `MaterialDropper` に配線済み |
| 済 | ボーナスカードDataSO | MCP実装済み | 既存カードPrefabに対応する `BonusCardDataSO` を21件まで作成し、`Main.unity` に配線済み |
| 済 | モジュール系クラフトレシピ | MCP実装済み | `ScrapModule` を補完し、`RepairModule.asset`、`BarrierModule.asset`、`UpgradeModule.asset` を作成済み |

## ボーナスカード不足

MCP実装後、`Main.unity` の `BonusCardsManager.cardDataList` は21件。
以下のPrefabは対応する `BonusCardDataSO` を作成済み。

| 作成済みDataSO | 既存Prefab |
|---|---|
| ACC +1 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_acc_1.prefab` |
| ACC +2 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_acc_2.prefab` |
| ACC +3 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_acc_3.prefab` |
| ACC -> STR変換 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_accTostr.prefab` |
| AR獲得 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_ar.prefab` |
| ゲート修復 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_gateRepair.prefab` |
| Gear +1 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_Gear_1.prefab` |
| Gear +2 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_Gear_2.prefab` |
| Gear +3 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_Gear_3.prefab` |
| HP +20 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_hp_20.prefab` |
| HP +100 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_hp_100 1.prefab` |
| Katana獲得 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_katana.prefab` |
| ModuleCore Lv1 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_ModuleCore_1.prefab` |
| ModuleCore Lv2 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_ModuleCore_2.prefab` |
| ModuleCore Lv3 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_ModuleCore_3.prefab` |
| Scrap +2 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_Scrap_2.prefab` |
| Scrap +4 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_Scrap_4.prefab` |
| STR +2 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_str_2.prefab` |
| STR +3 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_str_3.prefab` |
| STR -> ACC変換 | `Assets/GearCraft/ScriptableObject/Prehubs/BonusCards/BonusCard_strToacc.prefab` |

画像はあるがPrefab/DataSOが見当たらないカード候補:

- `BonusCard_san.png`
- `BonusCard_assault.png`
- `BonusCard_acc.png`
- `BonusCard_str.png`
- `BonusCard_hp.png`
- `BonusCard_gear.png`
- `BonusCard_scrap.png`
- `BonusCard.png`

注意: `SAN` は `StatusManager` に存在するが、`CardEffectType` にSAN増減効果がないため、SANカードを使うなら効果タイプ追加も必要。

## クラフト/モジュール不足

| 不足/未完成 | 現状 | 必要な素材/データ |
|---|---|---|
| ScrapModule | MCP実装済み | `recipeName`、`icon`、`completedImage`、`costs`、`moduleFlag=module_scrap` |
| RepairModule | MCP実装済み | `RepairModule.asset`、コスト、`moduleFlag=module_repair` |
| BarrierModule | MCP実装済み | `BarrierModule.asset`、コスト、`moduleFlag=module_barrier` |
| UpgradeModule | MCP実装済み | `UpgradeModule.asset`、`statBonus=5` |
| SteamGatlingレシピ | MCP実装済み | `SteamGatling.asset` レシピ |
| RailCraftレシピ | MCP実装済み | `RailCraft.asset` レシピ |

## 武器/変形不足

`PlayerController` は `GearCraft_Sword` から `GearCraft_Axe` への変形を前提にしているが、現行 `WeaponDataSO` は `GearCraft` のみ。

| 不足素材 | 現状 | 必要なもの |
|---|---|---|
| GearCraft_Sword WeaponDataSO | MCP実装済み | `GearCraft_Sword.asset` |
| GearCraft_Axe WeaponDataSO | MCP実装済み | `GearCraft_Axe.asset` |
| Sword/Axe用クラフト/取得導線 | MCP実装済み | `GearCraft.asset` レシピで `GearCraft_Sword` を装備し、`GearCraft_Axe` を追加所持させる |

関連画像/アニメーションは存在するため、完全な新規作画よりデータ整備が主。

## ドロップ素材不足

`MaterialManager.MaterialType`:

- `Scrap`
- `Gear`
- `UpgradeCore`
- `ModuleCore_lv1`
- `ModuleCore_lv2`
- `ModuleCore_lv3`

既存/作成済みドロップPrefab:

- `Drop_Scrap.prefab`
- `Drop_Gear.prefab`
- `Drop_UpgradeCore.prefab`
- `Drop_ModuleCore_lv1.prefab`
- `Drop_ModuleCore_lv2.prefab`
- `Drop_ModuleCore_lv3.prefab`

画像 `moduleCore_lv1.png`、`moduleCore_lv2.png`、`moduleCore_lv3.png` を使ってPrefab化し、`MaterialDropper` 側の参照追加もMCPで実装済み。

## 旧RandomEnemyアセット不足

以下3件はUnity上でロード失敗。`m_Script` のGUID `71e6b0bb4f4ce479bb52b9e3d54c093f` に対応するスクリプトが現行プロジェクトに存在しない。

- `Assets/GearCraft/EnemyData/RandomEnemy/lv1_3_random.asset`
- `Assets/GearCraft/EnemyData/RandomEnemy/lv2_3_random.asset`
- `Assets/GearCraft/EnemyData/RandomEnemy/lv3_1_random.asset`

現行の自動生成ステージでは必須ではないが、旧WaveDataを使う場合は対応ScriptableObjectクラスの復元が必要。

## 現時点で足りているもの

- 基本武器DataSO: 8件あり。
- 基本敵DataSO: Final Boss追加後、10件あり。
- 強化パーツDataSO: 10件あり。
- ボーナスカードDataSO: 21件あり。
- クラフトレシピDataSO: 9件あり。
- 主要シーン用画像: Title/Main/Craft/CraftSpace/Ending は概ね存在。
- 参照切れObjectReference: 検出なし。
