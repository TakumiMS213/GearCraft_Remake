SteamThrower倍率変更の独立検証probe（2026-10-15）

VerifyWeaponScaling.cs.txtはUnity MCP execute_codeへ渡すC#6メソッド本文です。このファイルを作成した担当はMCP実行していません。Playを止め、再コンパイル・import・他QA callbackの終了を確認してから実行してください。Play/切替中、コンパイル/import中、RUNNING記録がある場合は変更せず停止します。

検証内容
・8武器（旧GearCraft、剣、斧、Cannon、Rail、Gatling、AR、Katana）の基礎値・ACC/flat係数・旧式ダメージを維持。追加module係数が未記載assetでも1として読み込まれることを確認。
・SteamThrowerはACC 0/20 × flat 0/16 × module 0/20/40 × STR 0/20の24条件で、変更前の式の0.3倍を確認。STRを独立に変えてもダメージが変化しないことを含む。8武器各12条件と合わせ計120条件。
・実Steam弾Prefabを非アクティブのまま複製し、PlayerController.ConfigureSpawnedProjectileをreflectionで呼ぶ。上記24条件×BOOST有無の48条件で実Configure後のdamageと1.2倍カードを確認。currentDamageは通常HandleAttackと同じく武器baseDamageをセットする。
・同じ実弾でslip0.35、重力1.15、減速2.8秒/0.55、個別hit間隔0.18、寿命2.4、BOOST時のみ素材焼却が有効になることを確認。別の一発でNoFallカードの重力0を確認。
・null武器の0、Steamのcooldown0.08/弾速13/attackRange2.4、武器9asset・元Prefab・既存Status singleton/フィールド・PlayerPrefs8キー・元scene/dirty・時間設定の不変を確認。

安全性と片付け
非アクティブのHideAndDontSaveオブジェクトをPreview Sceneへ置き、その後でStatusManager/PlayerControllerを追加します。Awake/Startを走らせず、既存singletonを注入・変更せず、Equip/Acquire/Save/PlayerPrefs書き込みを呼びません。一時Statusのフィールドだけで実計算メソッドを呼びます。既存singletonが元から存在しないEdit状態でも動作できます。
本体・弾とも一度も有効化しないのでUpdate/FixedUpdate/衝突/敵ダメージ/寿命Destroyは走りません。最後にfinallyで全一時オブジェクトとPreview Sceneを除去します。想定外にsingleton参照またはRandom stateが変わった場合は復元し、成功にはせずFAILを記録します。既存Statusフィールド・Prefs・sceneが不変でない場合もFAILです。
通常入力、衝突実ダメージ、オーバーヒートの時系列、撃破時間やバランスの実測を代替する試験ではありません。

出力
毎回未使用のweapon-scaling-UTC-GUID接頭辞を作り、-matrix.csv/-projectiles.csv/-checks.csv/-summary.txtへ出力します。weapon-scaling-latest.txtは最新接頭辞の参照です。既存の比較結果・バックアップ・元READMEは上書きしません。summaryのPASSは全検証と片付け後の不変確認を通った場合のみです。

読み取りレビュー
WeaponDataSO.craftModuleDamageMultiplierの初期値1により、未指定の既存8assetは従来加算を維持します。StatusManager.GetWeaponDamageBonusは工房武装だけmodule×武器係数とし、AR/Katanaは引き続きmodule対象外。SteamThrower.assetのbase0.3/acc0.024/flat0.024/module0.3により通常直撃は旧式の0.3倍になります。InventoryUIもGetWeaponDamageを使うため同じ値を表示します。BOOST専用倍率はその後の全直撃へ掛かります。実Prefabの減速/継続ダメージ等は別経路で今回の変更対象に入っていません。
