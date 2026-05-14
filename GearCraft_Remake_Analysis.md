# GearCraft Remake 解析資料

解析対象: `C:\Users\owner\Desktop\Unity\GearCraft_Remake`  
作成日: 2026-05-14  
目的: 既存Unityプロジェクトから、リメイク制作時に参照できるゲーム仕様・構成要素・データ設計を復元する。

## 1. 読み取り方針

- 主な根拠は `Assets/GearCraft` 配下の C#、Scene、ScriptableObject、Prefab、画像/音声ファイル名。
- C#コメントは日本語が文字化けしている箇所が多いため、クラス名、フィールド名、メソッド名、ScriptableObjectの値を優先した。
- 画像内テキストはOCR未実行。ファイル名に含まれる語句から推測した箇所は「推測」と明記する。
- `Library` 配下はUnity生成物なので仕様資料の主対象から除外。

## 2. プロジェクト概要

`GearCraft` は、横スクロール/固定レーン寄りの2D防衛アクションとクラフト・強化要素を組み合わせたゲームと推測される。

プレイヤーは拠点/ゲートを守りながら敵ウェーブを撃破し、素材を集める。一定ステージごとにクラフトスペースへ戻り、武器、モジュール、強化パーツを整えて次の戦闘に向かう。最終的に31ステージ到達でエンディングに分岐する。

主要テーマはスチームパンク/歯車/機械兵器。

## 3. 技術構成

| 項目 | 内容 |
|---|---|
| Unity | `6000.0.67f1` |
| 描画 | URP 17.0.4、2D系パッケージあり |
| 入力 | Unity Input System 1.18.0。ただし実装上は `Input.GetKeyDown` / `Input.GetAxisRaw` も多用 |
| 非同期 | UniTask |
| Tween | DOTween |
| UI | uGUI、TextMesh Pro |
| シーン | `Title`, `Main`, `Ending`, `CraftSpace`, `Craft` |
| フォント | `DotGothic16-Regular.ttf` |

`Packages/manifest.json` 上の主な依存:

- `com.cysharp.unitask`
- `com.unity.inputsystem`
- `com.unity.render-pipelines.universal`
- `com.unity.ugui`
- `com.unity.timeline`
- DOTweenは `Assets/GearCraft/Plugins/Demigiant/DOTween` として同梱。

## 4. Build Settings

`ProjectSettings/EditorBuildSettings.asset` に登録されているシーン:

1. `Assets/GearCraft/Scenes/Title.unity`
2. `Assets/GearCraft/Scenes/Main.unity`
3. `Assets/GearCraft/Scenes/Ending.unity`
4. `Assets/GearCraft/Scenes/CraftSpace.unity`
5. `Assets/GearCraft/Scenes/Craft.unity`

`Assets/Scenes/SampleScene.unity` も存在するが、Build Settings対象外。

## 5. タグ・レイヤー

`ProjectSettings/TagManager.asset` より:

### Tags

| Tag | 用途 |
|---|---|
| `Enemy` | 敵判定 |
| `Bullet` | 弾判定 |
| `Wall` | 壁判定 |
| `AR_returnPoint` | AR系敵または弾の戻り地点用と推測 |
| `gate` | ゲート |
| `Arm` | プレイヤー腕/武器アーム |
| `Ground` | 接地判定 |
| `Spawner` | 敵スポナー |

### Layers

- `Enemy`
- `Wall`
- `gate`
- `UI`

## 6. 入力仕様

`Assets/InputSystem_Actions.inputactions` と実装から推定。

| 操作 | キー/入力 | 実装上の用途 |
|---|---|---|
| 移動 | A/D、左右キー、Gamepad leftStick | 戦闘中の左右移動、UIナビゲーション |
| ジャンプ | Space、Gamepad South | 戦闘中ジャンプ |
| 攻撃 | 左クリック、Enter、Gamepad West | 近接/遠距離攻撃 |
| インタラクト | E、Gamepad North | バリア使用、クラフトスペース操作にも使用される可能性 |
| ブースト | Q | ギアを消費して攻撃速度ブースト |
| 武器変形 | LeftShift | `GearCraft_Sword` から `GearCraft_Axe` へ一時変形 |
| ポーズ | Tab | `PauseManager` でメニュー開閉 |
| UI戻る/閉じる | Escape系/ボタン | `UISwitcher` や各パネル制御 |

実装はInput System定義と旧Input APIが混在している。リメイクでは入力層を一本化した方が保守しやすい。

## 7. 全体ゲームループ

推定される基本ループ:

1. `Title` でスタート。
2. `CraftSpace` に遷移。
3. `CraftSpace` で休憩/回復/クラフト/強化の導線を選ぶ。
4. `Main` で敵ウェーブと戦闘。
5. ステージ最後の敵を倒すとステージクリア。
6. 報酬カード3択を表示。
7. 一定ステージごとに `CraftSpace` へ戻る。
8. 最終到達時に `Ending` へ遷移。

永続状態は主に以下が保持する:

- `StatusManager`: HP、SAN、STR、ACC、GATE、武器、モジュール、強化効果、エンディング判定
- `MaterialManager`: 素材所持数
- `StageCounter`: 現在ステージ数

## 8. シーン別仕様

### Title

主なオブジェクト/資産名:

- `StartButton`
- `SettingButton`
- `ManualPanel`
- `StoryPanel`
- `SettingPanel`
- `TitleBGM`
- `BGMslider`
- `SEslider`
- `StatusManager`
- `MaterialManager`

役割:

- タイトル画面。
- スタートで `CraftSpace` に遷移する設定が確認できる。
- マニュアル、ストーリー、設定画面がある。
- BGM/SEスライダーあり。
- `StatusManager` と `MaterialManager` がここで生成され、`DontDestroyOnLoad` により以降のシーンへ残る設計。

タイトル画像から推測できる説明要素:

- `wasd`, `wasd_yajirusi`, `wasdText`: 移動説明。
- `mouse`, `attacktext`: 攻撃説明。
- `SPACE`, `Jumptext`: ジャンプ説明。
- `Q`, `BoostManual_text`, `boostText`: ブースト説明。
- `SHIFT`, `shiftext`: GearCraft変形/特殊アクション説明。
- `E`, `barriertext`: バリア/インタラクト説明。
- `STRandACC_manulaText`: STR/ACCの説明。

### CraftSpace

主な画像/要素:

- `gearcraft_craftspace_bed`
- `gearcraft_craftspace_crafter`
- `gearcraft_craftspace_flower`
- `gearcraft_craftspace_freeze`
- `gearcraft_craftspace_medi`
- `gearcraft_craftspace_wall`
- `gs_cs_gate`
- `gc_cs_spacekey`
- `StatusUpWIndow_HP`
- `StatusUpWIndow_STR`
- `StatusUpWIndow_ACC`
- `StatusUpWIndow_SAN`
- `statusup_notion`

推定される役割:

- 拠点/休憩所。
- ベッド、クラフター、医療、冷凍/保存装置、花、ゲートなどのインタラクションポイントがある。
- ステータスアップ用の表示UIがある。
- `PlayerMovement` により左右移動と背景パララックスがある。
- `UIRangeDisplay`, `UIHotkeyController`, `PauseOnPanelActive` が存在し、近づくとUI表示、ホットキーでパネルを開く構成と推測。
- `TransitionManager` がクラフト/メイン等への遷移演出を担当。

### Main

主なシステム:

- `PlayerController`
- `EnemySpawner`
- `StageFlowManager`
- `GateManager`
- `MaterialDropper`
- `BonusCardsManager`
- `PauseManager`
- `StatusDisplay`
- `ModuleStatusManager`
- `CameraShake`

役割:

- 戦闘本編。
- プレイヤーがゲートを守りながら敵を倒す。
- 敵がゲートに到達するとゲートHPが減り、PERFECT判定が失われる。
- 敵撃破で素材ドロップ。
- ステージ最後の敵撃破でクリア処理へ進む。
- クリア後、武器耐久度が1減少。
- 報酬カード3択が表示される。

### Craft

主な要素:

- `CraftManager`
- `CraftPanel`
- `CraftTabController`
- `MaterialDisplay`
- `UpgradeGridManager`
- `UpgradeGridUI`
- `UpgradeShopManager`
- `EffectList`
- `ShopContent`
- `CraftTransitionManager`

役割:

- クラフト専用UIシーンまたはクラフトパネル。
- レシピ選択、素材消費、完成演出。
- 強化パーツ購入/配置も同じシーン内に存在。
- タブでクラフトとアップグレードを切り替える。

### Ending

主な画像:

- `Ending_bad.png`
- `Ending_true.png`
- `EndingText/Bad/Bad_1` から `Bad_8`
- `EndingText/Normal/Normal_1` から `Normal_6`
- `EndingText/True/True_1` から `True_10`

役割:

- Bad / Normal / True の3分岐。
- `EndingManager.PendingEndingNum` で表示対象を決める。
- `ImageSequenceViewer` が画像シーケンスを順送りする。
- `ScoreManager` がスコア表示を担当。

## 9. 永続ステータス仕様

`StatusManager` が `DontDestroyOnLoad` で永続化。

| 項目 | 初期値 | 意味 |
|---|---:|---|
| `HP` | 100 | プレイヤーHP |
| `SAN` | 100 | 正気度/精神値と推測。現状コード上の戦闘影響は薄い |
| `STR` | 0 | 近接ダメージ加算 |
| `ACC` | 0 | 遠距離ダメージ加算 |
| `GATE` | 100 | ゲートHP |
| `currentWeapon` | defaultWeapon | 装備中武器 |
| `ownedWeapons` | defaultWeapon含む | 所持武器 |
| `module_scrap` | false | スクラップ/ギア獲得系モジュール |
| `module_repair` | false | ステージ後HP回復 |
| `module_barrier` | false | バリア使用可能 |
| `punkDrive` | false | PunkDrive常時/任意攻撃装置 |
| `craftWeaponDamagebuff` | 0 | クラフト由来の武器ダメージ強化 |
| `killAllEnemies` | true | Trueエンド判定。ゲート到達を許すとfalse |
| `bossKillCount` | 0 | 強化グリッド拡張用。ただし現コードでは増加箇所未確認 |

### 強化効果キャッシュ

| 項目 | 初期値 | 意味 |
|---|---:|---|
| `bonusDamage` | 0 | ダメージ固定加算 |
| `attackSpeedMult` | 1 | クールタイム倍率。小さいほど速い |
| `hasBulletDouble` | false | 弾を2発発射 |
| `spreadModifier` | 0 | 拡散角補正 |
| `durabilityDrainChance` | 0 | 敵撃破時に耐久回復する確率 |
| `ricochetCount` | 0 | 弾の跳弾回数 |
| `junkCollectorMult` | 1 | ドロップ量倍率 |
| `bulletSizeMult` | 1 | 弾サイズ倍率 |
| `magnetRange` | 3 | ドロップ吸引範囲 |

## 10. プレイヤー仕様

`PlayerController` より。

### 移動

- 左右移動: `moveSpeed = 5`
- ジャンプ: `jumpForce = 10`
- 接地判定: `Ground` タグへの衝突。
- 壁判定: `Wall` タグへの接触中、壁方向へ進む入力を止める。
- アニメーション:
  - `run`
  - `jump`

### 攻撃

武器タイプ:

- `WeaponType.Melee`
- `WeaponType.Ranged`

近接:

- 左クリック押下で攻撃。
- `ArmRotation.RotateArmOnce(currentWeapon.meleeRotateDuration)` を実行。
- 実ダメージは `MeleeWeapon` 側で判定。
- ダメージ式: `武器baseDamage + STR + bonusDamage`
- 武器名が `GearCraft` を含む場合、さらに `craftWeaponDamagebuff` 加算。
- 近接中は敵弾を破壊できる。

遠距離:

- 左クリック押しっぱなしで連射。
- マウス方向へ弾を生成。
- 角度は `-90` から `90` 度にClamp。
- ダメージ式: `武器baseDamage + ACC + bonusDamage`
- `spreadAngle + spreadModifier` の範囲でランダム散布。
- `hasBulletDouble` なら2発目を少し角度差つきで発射。
- `isPiercing` なら弾が敵に当たっても消えない。

### 被ダメージ

- 敵接触ダメージ定数: `5`
- 敵弾ダメージは弾側の `bulletDamage`。
- HPが0未満でBadエンドへ。
- HP0時に敵と弾を全破壊し、プレイヤーRigidbodyをFreezeしてから `EndingManager.LoadEndingScene(1)`。

注意:

- `TakeDamageByBullet` 内でHPを減らした後、`DamageCooldown` でも `module_barrier == false` の場合さらに接触ダメージ定数分HPを減らしている。弾被弾時も追加5ダメージが入る可能性がある。

### バリア

- 使用キー: `E`
- 条件: `module_barrier == true` かつ `CanBarrierUse == true`
- 効果時間: `5秒`
- クールダウン: `10秒`
- 効果中は `isInvincible = true`、バリアオブジェクト表示。

### ブースト

- 使用キー: `Q`
- 消費素材: `Gear` 1個。
- 効果時間: 最大5秒。再使用で残り時間を最大5秒まで更新。
- 効果: `attackSpeedMult *= 0.5`、下限 `0.1`。
- 終了時: `attackSpeedMult = 1` に戻す。
- ブーストUI: `BoostPanel`

### GearCraft変形

- 使用キー: `LeftShift`
- 条件: 現在武器名が `GearCraft_Sword` かつ `CanUseGearCraft == true`
- 5秒間 `GearCraft_Axe` に切り替え、さらに5秒後再使用可能。
- 現在の `WeaponData` には `GearCraft_Sword` / `GearCraft_Axe` 名のアセットは確認できず、`GearCraft` のみ存在する。アニメーションや画像には剣/斧が存在するため、実装途中または命名不整合の可能性が高い。

## 11. ゲート仕様

`GateManager` より。

| 項目 | 値 |
|---|---:|
| 初期HP | `StatusManager.GATE` |
| 敵到達ダメージ | 10 |
| 点滅回数 | 3 |
| 点滅間隔 | 0.1秒 |
| 無敵時間 | 0.5秒 |

挙動:

- `Enemy` タグとトリガー接触するとゲートダメージ。
- ただし `EnemyAIType.CircleMove` の敵はゲート衝突対象外。
- ゲートに敵が到達すると:
  - `StatusManager.killAllEnemies = false`
  - `StageFlowManager.OnGateHit()` 呼び出し
  - PERFECT不可
  - 接触した敵は死亡処理
- `GATE <= 0` でBadエンド。

## 12. ステージ進行仕様

`StageFlowManager` と `StageGeneratorSO` より。

### 基本設定

| 項目 | 値 |
|---|---:|
| 総ステージ数 | 31 |
| 休憩間隔 | 3ステージ |
| 基本敵数 | 3 |
| 最大敵数 | 6 |
| 基本スポーン間隔 | 3秒 |
| 最小スポーン間隔 | 1秒 |
| ドロップ倍率 | ステージ0で1、ステージ31で3 |

### Boss Stages

コード初期値では `{10, 20, 30, 31}`。  
実際の `StageGeneratorConfig.asset` には `9, 18, 30, 31` とシリアライズされている。

Unityではアセット値が優先されるため、実プレイ上は `9, 18, 30, 31` と見るのが自然。

### ステージクリア条件

- 敵スポーン時に最後の敵へ `isLastEnemy = true` を付与。
- `EnemyController.Die()` が `StageFlowManager.OnEnemyKilled(this, isLastEnemy)` を呼ぶ。
- `wasLastEnemy == true` で `OnStageComplete()`。

### クリア時処理

1. 戦闘停止。
2. `Bullet` タグのオブジェクト全削除。
3. ゲート未被弾なら `PERFECT` 表示。
4. 現在武器の耐久度を1減少。
5. クリア演出。
6. `module_repair` が有効ならHP +20。
7. 最終ステージ以上ならエンディング判定。
8. それ以外はボーナスカード表示。
9. ステージ数を+1。
10. 休憩間隔に達していれば `CraftSpace` へ遷移。
11. 休憩でなければ次ステージ開始。

### 休憩への遷移

- 3ステージごとに `CraftSpace` へ移動。
- `module_scrap` が有効なら休憩遷移時に `Gear +1`。

### エンディング判定

- `currentStage >= totalStages` でゲームクリア処理。
- `killAllEnemies == true`: Trueエンド。
- `killAllEnemies == false`: Normalエンド。
- HP/GATEが0以下: Badエンド。

## 13. 敵スポーン仕様

`EnemySpawner` より。

### 自動生成モード

`useAutoGeneration = true` の場合、`StageGeneratorSO` に基づき自動生成。

通常ステージ:

- `enemyCount = Lerp(baseEnemyCount, maxEnemyCount, difficulty)`
- `spawnInterval = Lerp(baseSpawnInterval, minSpawnInterval, difficulty)`
- 敵プールから難易度に応じて選択。
- 最後にスポーンした敵を `isLastEnemy = true`。

ボスステージ:

- まず雑魚を `max(2, baseEnemyCount)` 体スポーン。
- その後2秒待ってボスをスポーン。
- ボスが `isLastEnemy = true`。

### 敵プール解放

`GetAvailablePoolMax()` はボス到達段階に応じて通常敵プールの使用範囲を広げる。

- 序盤: 通常敵プールの先頭側のみ。
- ボスステージを越えるごとに使用可能範囲が増える。
- 最低3体分は使用可能にする設計。

### 重要な不整合

`StageGeneratorConfig.asset` の `bossStages` は4つあるが、`bossPool` は3体分のみ。

- Boss Stage 9: `IC-408` と推測。
- Boss Stage 18: `BB-413` と推測。
- Boss Stage 30: `AR-227` と推測。
- Boss Stage 31: `bossPool[3]` が存在しない。

`SpawnBossStage()` はボスがいない場合でも雑魚を `isLastEnemy` にしないため、ステージ31で進行不能になる可能性がある。リメイク時は「最終ボスを追加する」か「ステージ31を通常ステージ/エンディング直行にする」かを決める必要がある。

## 14. 敵データ

`Assets/GearCraft/ScriptableObject/EnemyData/*.asset` より。  
MaterialType: `0=Scrap`, `1=Gear`, `2=UpgradeCore`, `3=ModuleCore_lv1`, `4=ModuleCore_lv2`, `5=ModuleCore_lv3`

| 敵 | Lv | HP | Speed | AI | Boss | Highlight | 主なドロップ |
|---|---:|---:|---:|---|---|---|---|
| A-1 | 1 | 50 | 5 | Straight | false | false | Scrap 1-3 70%、ModuleCore_lv1 1 10%、Gear 1 20% |
| B-1 | 1 | 10 | 10 | Bomber | false | true | Scrap 2-3 70%、ModuleCore_lv1 1 10%、Gear 1 20% |
| G-1 | 1 | 100 | 1 | Straight | false | false | Gear 1-2 70%、ModuleCore_lv1 1-2 10%、Gear 1-2 20% |
| BB-12 | 2 | 10 | 2 | Bomber | false | true | ModuleCore_lv2 1 20%、Scrap 6-9 70%、UpgradeCore 1 10% |
| EA-21 | 2 | 75 | 5 | Sniper | false | false | Scrap 3-6 50%、ModuleCore_lv2 1 10%、Gear 2-3 30%、UpgradeCore 1 20% |
| EG-33 | 2 | 150 | 1 | Straight | false | false | ModuleCore_lv2 1 20%、Gear 2-3 50%、UpgradeCore 1 30% |
| AR-227 | 3 | 4000 | 5 | Boss_Speed | true | false | ModuleCore_lv3 1 100%、Gear 5-15 100%、Scrap 15-20 100% |
| BB-413 | 3 | 5000 | 2 | Boss_Artillery | true | false | ModuleCore_lv3 1 100%、Gear 5-15 100%、Scrap 15-20 100% |
| IC-408 | 3 | 8000 | 2 | Boss_Tank | true | false | ModuleCore_lv3 1 100%、Gear 5-15 100%、Scrap 15-20 100% |

敵HPは実行時にステージ補正が入る:

`runtimeHP = baseHP + baseHP * (StageCount / 10f)`

例:

- Stage 10では `baseHP * 2.0`
- Stage 30では `baseHP * 4.0`

## 15. Enemy AI 種別

`EnemyAIType`:

| 種別 | 推定仕様 |
|---|---|
| `Straight` | ゲート方向へ直進 |
| `CircleMove` | 円運動しながら射撃。ゲート衝突ダメージ対象外 |
| `Sniper` | 遠距離から精密射撃 |
| `Charger` | 突進 |
| `Bomber` | 自爆/爆発 |
| `Boss_Tank` | 高HP、広範囲弾幕系 |
| `Boss_Speed` | 高速移動、突進と射撃 |
| `Boss_Artillery` | 遠距離ミサイル/爆撃 |
| `Boss_Final` | 全パターン複合。ただし対応EnemyData未確認 |

実装上、`EnemyController` が敵生成時に `enemyData.aiType` を見て該当AIコンポーネントをAddComponentする。

## 16. 弾・被弾・爆発仕様

### プレイヤー弾 `BulletController`

| 項目 | 初期値/仕様 |
|---|---|
| `bulletDamage` | 10 |
| `bulletLifeTime` | 2秒 |
| `destroyOnHit` | true |
| `ricochetCount` | 0 |
| `isCannonBullet` | false |
| 煙 | `DoSmoke` がtrueなら毎Updateで煙Prefab生成 |

敵に当たると:

- `EnemyController.TakeDamage(bulletDamage)`
- HitEffect再生
- `isCannonBullet` なら範囲爆発
- `destroyOnHit` なら弾破壊

壁/地面に当たると:

- `ricochetCount > 0` なら反射して継続。
- 0なら破壊。

爆発:

- 半径 `5`
- 範囲内の `Enemy` に `bulletDamage`

### 敵弾 `EnemyBulletController`

主な仕様:

- `bulletDamage = 10`
- `bulletLifeTime = 2秒`
- `destroyOnHit = true`
- 爆弾弾は `bombBullet = true`
- 爆発半径 `bombRadius = 3`
- プレイヤー/ゲートへのダメージ用途。

## 17. 武器データ

`Assets/GearCraft/ScriptableObject/WeaponData/*.asset` より。

WeaponType: `0=Melee`, `1=Ranged`

| 武器 | Type | CT | Damage | Range | BulletSpeed | Spread | Durability | 特性 |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Katana | 0 | 0.5 | 20 | 1 | 25 | 0 | 999 | default |
| GearCraft | 0 | 0.5 | 40 | 1 | 25 | 0 | 10 | 近接クラフト武器 |
| AR | 1 | 0.1 | 10 | 1 | 25 | 3 | 20 | 高連射 |
| SteamCannon | 1 | 1 | 50 | 3 | 25 | 0 | 20 | 砲弾Prefab |
| SteamGatling | 1 | 0.05 | 10 | 3 | 100 | 6 | 15 | 超連射、散布大 |
| RailCraft | 1 | 2 | 150 | 10 | 1000 | 0 | 15 | 貫通 |

武器耐久:

- ステージクリアごとに現在武器の耐久-1。
- `isDefault == true` の武器は耐久減少対象外。
- 耐久0で所持リストから削除し、defaultWeaponへ戻る。
- `RepairDurability(amount)` で耐久回復。

## 18. 素材仕様

`MaterialManager.MaterialType`:

| ID | 素材 |
|---:|---|
| 0 | Scrap |
| 1 | Gear |
| 2 | UpgradeCore |
| 3 | ModuleCore_lv1 |
| 4 | ModuleCore_lv2 |
| 5 | ModuleCore_lv3 |

所持数は `MaterialManager` が永続保持。

素材ドロップ:

- 敵死亡時に `EnemyDataSO.drops` を参照。
- `dropChance` を通過した場合、`minAmount` から `maxAmount` のランダム個数を生成。
- ステージ倍率 `StageGeneratorSO.dropMultiplierCurve` が乗る。
- 強化効果 `junkCollectorMult` が乗る。
- ドロップは個別Prefabとして散らばり、プレイヤーが近づくと吸引される。

ドロップ吸引:

- 基本吸引範囲 `3`
- 吸引速度 `12`
- 取得距離 `0.5`
- 寿命 `30秒`
- `MagnetRangeUp` で吸引範囲増加。

## 19. クラフト仕様

`CraftManager` と `CraftRecipeSO` より。

### クラフト処理

1. レシピを選択。
2. `MaterialManager.CanAfford(costs)` で素材確認。
3. `requiredWeapon` がある場合、所持武器に含まれているか確認。
4. 素材を消費。
5. クラフト演出。
6. 成果を `StatusManager` に反映。

### クラフト結果タイプ

| Type | 結果 |
|---|---|
| `Weapon` | 武器獲得、即装備 |
| `Module` | `moduleFlag` に応じてモジュール有効化 |
| `PunkDrive` | `status.punkDrive = true` |

### レシピデータ

MaterialType: `1=Gear`

| レシピ | Cost | Result | Required | 備考 |
|---|---|---|---|---|
| GearCraft | Gear x10 | Weapon: GearCraft | Katana | KatanaからGearCraftへ更新する設計 |
| PunkDrive | Gear x4 | PunkDrive有効化 | なし | 追従/周囲攻撃装置 |
| SteamCannon | Gear x50 | Weapon: SteamCannon | AR | AR所持が前提 |
| ScrapModule | なし | Module | なし | `recipeName`, `moduleFlag`, icon等が空。未完成データの可能性 |

画像には `RepairModule_text`, `BarrierModule_text`, `UpgradeModule_text`, `ScrapModule_text` があるため、実装予定のモジュールは少なくとも以下と推測:

- Scrap Module
- Repair Module
- Barrier Module
- Upgrade Module

ただし現在確認できる `CraftRecipe` は4件のみで、Repair/Barrier/Upgrade Moduleのレシピアセットは未確認。

## 20. モジュール仕様

コード上で確認できるモジュール:

| Flag | 効果 |
|---|---|
| `module_scrap` | 休憩遷移時に `Gear +1` |
| `module_repair` | ステージクリア後に `HP +20` |
| `module_barrier` | `E` キーで5秒無敵バリア、10秒CD |
| `punkDrive` | PunkDriveオブジェクトを有効化 |

`ModuleStatusManager` は有効なモジュールのアイコン表示を切り替える。

### PunkDrive

`PunkDriveManager` より:

- 接触中の敵へ継続ダメージ。
- 基本ダメージ `0.1`
- ダメージ間隔 `0.5秒`
- ダメージ式: `0.1 + STR / 10`
- HitEffectあり。

## 21. 強化パーツ仕様

`UpgradePartSO`, `UpgradeGridManager` より。

### 基本設計

- テトリス風のグリッド配置型強化システム。
- パーツは `width`, `height`, `shapeData` を持つ。
- 0/90/180/270度回転に対応。
- グリッド外、重複配置は不可。
- `rangedOnly == true` のパーツは遠距離武器装備時のみ配置可能。
- 配置中パーツの効果を合算して `StatusManager` に反映。

### グリッドサイズ

`StatusManager.GetUpgradeGridSize()`:

`3 + bossKillCount`

意図としてはボス撃破数に応じて:

- 初期: 3x3
- ボス1体撃破: 4x4
- ボス2体撃破: 5x5
- ボス3体撃破: 6x6

ただし `bossKillCount` を増やす処理は確認できなかった。

### 強化効果タイプ

| Type | 効果 |
|---|---|
| `DamageFlat` | `bonusDamage += value` |
| `AttackSpeedMult` | `attackSpeedMult *= value` |
| `BulletDouble` | 2発化 |
| `SpreadReduction` | 拡散角減少 |
| `SpreadIncrease` | 拡散角増加 |
| `DurabilityDrain` | 敵撃破時に確率で耐久+1 |
| `Ricochet` | 跳弾回数加算 |
| `JunkCollector` | ドロップ倍率加算 |
| `BulletSizeUp` | 弾サイズ倍率加算 |
| `MaxDurabilityUp` | 最大耐久増加予定。ただし現状反映処理が未完成 |
| `MagnetRangeUp` | 素材吸引範囲増加 |

### 確認済みパーツ

| パーツ | Rarity | Shape | Cost | 効果 |
|---|---:|---|---|---|
| Damage+1 | Common | 1x1 | Scrap x3 | DamageFlat +1 |

画像やUI名からはショップ/インベントリ/効果一覧の土台があるが、データとしては1種類のみ確認。

## 22. ボーナスカード仕様

`BonusCardsManager` より。

### 表示条件

- ステージクリア後に3枚表示。
- `cardDataList` から重み付きランダムで重複なし3枚を選択。
- 選択すると効果適用しカードを消す。

### カード効果タイプ

| Type | 効果 |
|---|---|
| `AddHP` | HP加算 |
| `AddGateHP` | GATE加算 |
| `AddSTR` | STR加算 |
| `AddACC` | ACC加算 |
| `AddMaterial` | 素材加算 |
| `GrantWeapon` | 武器獲得/装備 |
| `GrantUpgradePart` | 強化パーツ付与 |
| `RepairDurability` | 現在武器耐久回復 |
| `MagnetRangeUp` | 吸引範囲増加 |
| `ConvertSTRtoACC` | STRをACCへ変換 |
| `ConvertACCtoSTR` | ACCをSTRへ変換 |

### 確認済みカードデータ

`Assets/GearCraft/ScriptableObject/BonusCard/STR_1.asset`

| 項目 | 値 |
|---|---|
| cardName | 筋力上昇 |
| weight | 0.1 |
| effect | `AddSTR` value 1 |

### 画像/Prefabから推測されるカード群

Prefabは多数存在するが、対応する `BonusCardDataSO` は1件のみ確認。

推測されるカード:

- `BonusCard_acc_1/2/3`
- `BonusCard_str_1/2/3`
- `BonusCard_hp_20`
- `BonusCard_hp_100`
- `BonusCard_Gear_1/2/3`
- `BonusCard_Scrap_2/4`
- `BonusCard_ModuleCore_1/2/3`
- `BonusCard_ar`
- `BonusCard_katana`
- `BonusCard_gateRepair`
- `BonusCard_accTostr`
- `BonusCard_strToacc`

リメイク時はこれらを正式なカードデータとして復元すると、既存アート資産を活かせる。

## 23. エンディング仕様

### 分岐

| EndNum | エンディング | 条件 |
|---:|---|---|
| 1 | Bad | プレイヤーHP0未満、またはGATE 0以下 |
| 2 | Normal | 最終ステージ到達、ただし `killAllEnemies == false` |
| 3 | True | 最終ステージ到達、かつ `killAllEnemies == true` |

`killAllEnemies` は名前上「全敵撃破」だが、実際には「ゲートに敵を通したかどうか」の意味が強い。敵がゲートに到達するとfalseになる。

### スコア

`ScoreManager`:

- True条件: `(STR * 1000 + ACC * 1000 + stageCount * 500) * 2`
- それ以外: `STR * 1000 + ACC * 1000 + stageCount * 500`

HP、GATE、素材、武器、クリアタイムはスコアに入っていない。

## 24. アセット構成

### フォルダ

| Folder | 内容 |
|---|---|
| `Animation` | プレイヤー、腕、敵、タイトル、PunkDrive、Barrier等 |
| `Audio` | BGM/SE |
| `EnemyData` | 旧WaveData/ランダム敵/LastEnemyデータ |
| `Images` | Title、Main、Craft、CraftSpace、Ending、Character |
| `Paticles` | Explosion, Smoke, Burn等。フォルダ名は `Particles` の誤字 |
| `Plugins` | DOTween |
| `Resources` | DOTweenSettings、BillingMode |
| `Scenes` | 実シーン |
| `ScriptableObject` | Weapon/Enemy/Craft/Upgrade/Prefab |
| `Scripts` | 全ロジック |
| `Settings` | URP/Renderer/SceneTemplate |
| `TextMesh Pro` | TMP標準資産 |

### 画像カテゴリ

- Character: プレイヤー立ち/歩き/ジャンプ/ダメージ。
- Title: タイトルUI、マニュアル、ストーリー、設定。
- Main: 戦闘背景、ゲート、敵、弾、カード、ブーストUI。
- Craft: クラフトUI、素材、結果画像、テキスト画像。
- CraftSpace: 拠点背景、ベッド、医療、クラフター、ゲート、ステータスアップUI。
- Ending: Bad/Normal/Trueのシーケンス画像。

### 音声

主な音声名:

- `Steam-and-Steel-Skies.mp3`
- `PerituneMaterial_Steam_Fortress.mp3`
- `maou_bgm_acoustic49.mp3`
- `inishienohiseki.mp3`
- `gear2.ogg`
- `gear3.ogg`
- `決定1.mp3`, `決定5.mp3`, `決定ボタンを押す7.mp3`, `決定ボタンを押す38.mp3`
- `大型ロボットの駆動音2/3/7.mp3`
- `handgun-sound-effect-432110.mp3`
- `gunfire-single-shot-colt-peacemaker-94951.mp3`
- `clean-machine-gun-burst-98224.mp3`
- `080997_bullet-39735.mp3`

用途推測:

- タイトル/メイン/クラフト/エンディングBGM。
- UI決定SE。
- 歯車/機械駆動音。
- 銃撃/弾/爆発系SE。

## 25. ScriptableObject一覧

### WeaponData

- `AR.asset`
- `GearCraft.asset`
- `Katana.asset`
- `RailCraft.asset`
- `SteamCannon.asset`
- `SteamGatling.asset`

### EnemyData

- `lv1_A-1.asset`
- `lv1_B-1.asset`
- `lv1_G-1.asset`
- `lv2_BB-12.asset`
- `lv2_EA-21.asset`
- `lv2_EG-33.asset`
- `lv3_AR-227.asset`
- `lv3_BB-413.asset`
- `lv3_IC-408.asset`

### CraftRecipe

- `GearCraft.asset`
- `PunkDrive.asset`
- `ScrapModule.asset`
- `SteamCannon.asset`

### StageGenerator

- `StageGeneratorConfig.asset`

### BonusCard

- `STR_1.asset`

### UpgradeParts

- `Damage+1.asset`

## 26. 旧/未使用と思われるWaveData

`Assets/GearCraft/EnemyData` に `EnemyWaveData` 形式の手動Waveデータが残っている。

カテゴリ:

- `1Set`
- `2-1Sets`
- `3Sets`
- `LastEnemies`
- `RandomEnemy`

現在の `EnemySpawner` は `useAutoGeneration = true` の場合 `StageGeneratorSO` を使う。手動Waveは旧仕様または手動モード用と推測。

リメイク時は以下のどちらかに整理するとよい:

- 自動生成中心: `StageGeneratorSO` に統合し、旧WaveDataは参考資料化。
- 手動ステージ中心: `EnemyWaveData` を正式採用し、ステージごとの演出/配置を手作りする。

## 27. 既存実装の注意点

### 進行不能リスク

- `StageGeneratorConfig.asset` のボスステージは4つだが、ボスデータは3体。
- Stage31でボスが出ず、雑魚にもLastEnemyが設定されない可能性。

### GearCraft変形の命名不整合

- `PlayerController.ChangeGearCraft()` は `GearCraft_Sword` と `GearCraft_Axe` を探す。
- `WeaponData` には `GearCraft` のみ。
- 画像/アニメーションには剣/斧の区別があるため、データが不足している可能性。

### ボス撃破数が増えない

- `bossKillCount` はグリッドサイズ拡張に使う設計。
- 増加処理が確認できない。

### 強化パーツが未整備

- UI/システムは存在するが、データは `Damage+1` のみ確認。
- `MaxDurabilityUp` は計算されるが耐久上限へ反映されていない。

### ボーナスカードが未整備

- Prefab/画像は多数あるが、`BonusCardDataSO` は `STR_1` のみ。
- `BonusCardsManager.cardDataList` にシーン上でPrefabを直接設定している可能性はあるが、資産データとしては不足。

### HP被弾処理の二重減算疑い

- `TakeDamageByBullet()` 後に `DamageCooldown()` が呼ばれ、追加で接触ダメージ相当が減る可能性。

### コーディング規約面

- namespaceなし。
- publicフィールドが多い。
- `Update()` 内の生成や検索が一部重い:
  - `BulletController.Update()` が煙を毎フレームInstantiate。
  - `DroppedMaterialItem.Start()` がPlayerをFind。
  - `PlayerController.TakeDamageByBullet()` が敵/弾全検索。
- リメイク時はプール化、参照キャッシュ、イベント駆動化が望ましい。

## 28. リメイク時に優先して確定すべき仕様

1. ステージ数とボス配置
   - 9/18/30/31で行くのか、10/20/30/31に戻すのか。
   - Stage31の最終ボスを作るのか、Stage30クリアでエンディングにするのか。

2. GearCraft武器体系
   - `GearCraft` 単体武器にするのか。
   - `GearCraft_Sword` / `GearCraft_Axe` の2武器を正式化するのか。
   - `LeftShift` の変形を中核システムにするのか。

3. クラフト/モジュールの完成範囲
   - Scrap/Repair/Barrier/Upgrade Moduleのレシピと素材コスト。
   - ModuleCore_lv1/2/3 の使い道。

4. 強化グリッド
   - ボス撃破でグリッド拡張する仕様を採用するか。
   - パーツの種類、レアリティ、コスト、ショップ更新条件。

5. ボーナスカード
   - ステージごとの報酬として採用するか。
   - カードの出現重み、効果、上限、重複可否。

6. エンディング条件
   - True条件を「ゲート無被弾」だけにするか。
   - HP/SAN/素材/全ボス撃破/特定クラフトなどを絡めるか。

## 29. リメイク向け仕様案

既存資産とコードの意図を最大限活かすなら、以下の構成が自然。

### 推奨ゲーム構成

- 31ステージ制。
- 通常戦闘を3ステージ進めるたびに拠点へ戻る。
- ボスは 10, 20, 30, 31 に配置。
- 31はFinal Bossとして `Boss_Final` を正式実装。
- 拠点でクラフト/強化/回復/準備。
- ステージクリア後にボーナスカード3択。

### 推奨エンディング条件

- Bad: プレイヤーHP0、またはGATE0。
- Normal: Final Boss撃破。ただしゲート被弾あり。
- True: Final Boss撃破、全ステージでゲート無被弾。

### 推奨武器進行

1. Katana
2. AR
3. SteamCannon
4. SteamGatling
5. RailCraft
6. GearCraft Sword/Axe

既存レシピに合わせるなら:

- Katana + Gear x10 -> GearCraft
- AR + Gear x50 -> SteamCannon
- PunkDriveはGear x4

不足分として:

- SteamGatlingレシピ
- RailCraftレシピ
- GearCraft Sword/Axe分割データ

### 推奨素材用途

| 素材 | 用途 |
|---|---|
| Scrap | 基本強化パーツ購入、低級クラフト |
| Gear | 武器クラフト、ブースト燃料 |
| UpgradeCore | 強化パーツ購入/更新 |
| ModuleCore_lv1 | Scrap/Repairなど基礎モジュール |
| ModuleCore_lv2 | Barrier/Upgradeなど中級モジュール |
| ModuleCore_lv3 | GearCraft/RailCraft/最上位強化 |

## 30. 実装クラス対応表

| システム | 主クラス |
|---|---|
| 永続ステータス | `StatusManager` |
| 素材管理 | `MaterialManager` |
| シーン遷移 | `SceneTransitionManager`, `TransitionManager`, `CraftTransitionManager` |
| ステージ進行 | `StageFlowManager`, `StageCounter` |
| 敵生成 | `EnemySpawner`, `StageGeneratorSO`, `EnemyWaveData` |
| 敵本体 | `EnemyController`, `EnemyDataSO` |
| 敵AI | `IEnemyAI`, `EnemyAI_*` |
| プレイヤー | `PlayerController` |
| 近接武器判定 | `MeleeWeapon`, `ArmRotation` |
| 弾 | `BulletController`, `EnemyBulletController` |
| ゲート | `GateManager`, `GateWarningUI` |
| 素材ドロップ | `MaterialDropper`, `DroppedMaterialItem` |
| クラフト | `CraftManager`, `CraftRecipeSO`, `CraftCost`, `MaterialDisplay` |
| 強化グリッド | `UpgradeGridManager`, `UpgradeGridUI`, `UpgradePartSO`, `UpgradeEffectDisplay` |
| 強化ショップ | `UpgradeShopManager` |
| ボーナスカード | `BonusCardsManager`, `BonusCardDataSO`, `CardEffect` |
| UI | `StatusDisplay`, `InventoryUI`, `DurabilityDisplay`, `TooltipSystem`, `PerfectClearUI` |
| タイトル | `UISwitcher`, `SoundButton`, `Audio`, `GameExitManager` |
| エンディング | `EndingManager`, `ImageSequenceViewer`, `ScoreManager` |

## 31. リメイク時の最小データ復元チェックリスト

- [ ] `StatusManager` 相当の永続セーブ/ランタイム状態を設計する。
- [ ] 6種類の武器データを正式化する。
- [ ] 9種類の敵データを正式化する。
- [ ] Final Boss用の敵データを追加する。
- [ ] 6種類の素材を正式化する。
- [ ] 既存クラフト4件を復元し、不足レシピを追加する。
- [ ] ボーナスカードPrefab群を `BonusCardData` として整備する。
- [ ] 強化パーツを最低10種類以上追加し、グリッドシステムを成立させる。
- [ ] ボス撃破時の `bossKillCount` 増加を実装する。
- [ ] Stage31進行不能リスクを解消する。
- [ ] GearCraft Sword/Axeのデータ不整合を解消する。
- [ ] エンディング条件を仕様書として固定する。

