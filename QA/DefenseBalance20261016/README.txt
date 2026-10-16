防衛バランス計測：本編の一日区間

範囲
取得日からEDまでを連続で走る試験ではない。IC当日wave7→9、BB当日16→18、AR当日28→30、EK当日31を、各朝の指定ビルドから検証する。
StageFlowManager、EnemySpawner、敵AI、敵弾、門・プレイヤーの被害処理は本編のまま動かす。HP/GATEの補充や敵の消去、スポーンの再生置換、waveリセットはしない。
既存の報酬カードは実Buttonから選択し、HP60以下なら回復、門耐久が初期値の半分以下なら修理を優先。その後は対象武器に合う能力を選ぶ。本編の報酬・Repair/Barrier等による自然な回復は許容し記録する。

準備と実行（rootがUnity MCPで実施）
1. 今回セッションのplayerprefs-before.txt、editor-before.txt、gameview-before.txt、必要に応じlogger-before.txtを新規退避する。旧QAの退避ファイルは流用しない。
2. native-survival-config.csvを weapon,startStage,profile,seed の1行へ設定。native-survival-name.txtに固有のcase名を設定する。
3. Title PlayでNativeSurvivalBootstrap.cs.txtを実行し、native-survival-bootstrap-progress.txtのREADYを待つ。
4. READY時にrootが条件ごとのScrap/Repair/Barrier等をnative statusへ設定し、NativeSurvivalRoute.cs.txtを実行する。初期状態JSONはこの設定後の実状態を保存する。
5. COMPLETEでPlay終了後、rootがRestoreSession.cs.txt等で退避状態を復元・照合する。RouteはPlayerPrefsを書かず、bootstrap異常時も復元はrootが行う。
PrepareControlledMainを実行しない。これはnativeスポーンを止める別の処理能力試験用であり、本試験には不要。

出力
case-events.csv：本編のwave開始・完了、敵出現・撃破、実報酬、日末Continue、終了理由。
case-waves.csv：wave開始/終了の時刻、HP/GATE、観測被害/回復、攻撃回数。成功・死亡・門破壊などの終了理由。
case-bosses.csv：IC/BB/AR/EK出現時と終了時に1行ずつ。ボス出現からの経過秒、開始/終了HP/GATE、被害/回復、ボス残HP、位置、攻撃回数。撃破を観測できなかった消失は撃破とみなさない。
case-samples.csv：1ゲーム秒ごとに生存敵1体につき1行。プレイヤー/敵の位置、速度、敵bounds、近接距離、対象ID、入力ブロック、cooldown、攻撃抑止理由、native敵AI状態/phase。敵0体時も1行。終了時には追加の強制記録を行う。
case-result.txt：初期/終端HP/GATE、区間全体の時間と被害/回復、終了理由。
case-initial-status.json / final-status.json：各ビルドと報酬後の状態。moduleやpassiveの実設定も含む。
case-restore-pending.txt：rootによるセーブ復元が必要なことを示す。

注意
被害・回復総量は各フレームの差分なので、同じフレームに回復と被害が重なった内訳は分離できない。開始/終端耐久とwave/ボスごとの値を優先して読む。
標準botは門に最も近い可視敵を狙い、通常の武器弾・近接・native cooldownを使う。視認域/射程/照準とGearの5秒ごとの形態交替は既存試験を維持。初期版はdash/BOOSTなしで、人間の生存率を証明するものではない。
別担当のReactiveBotSnippet.cs.txtを統合した場合は、基準botとは別名で結果を保存し、その実装条件と比較する。
上限は900ゲーム秒 / 300実秒。本編死亡リトライの前に失敗記録を残してPlayを終了する。取得日からの購入・宿舎設備選択・全日連続キャンペーンは今回の対象外。

対応操作版（ReactiveSurvivalRoute.cs.txt）
NativeSurvivalRoute.cs.txtは基準版として変更せず、別ファイルへReactiveBotSnippetを統合済み。bot-mode.txt=reactive、allow-boost.txt=trueで対応操作を有効にする。実行前にnative-survival-name.txtを基準版とは異なるcase名へ変更し、同じbootstrap READYからReactiveSurvivalRoute.cs.txtを実行する。
0.2秒ごとに画面内8m以内の敵弾を判断する。EnemyProjectileDamageableだけを通常攻撃で迎撃し、通常EnemyBulletControllerと落下爆弾は回避対象にする。移動は敵本体への距離、照準だけは迎撃対象のaimBoundsを使い、弾へ近づく追跡移動は行わない。
回避は実StartDashと本編の速度・持続・壁判定を使用し、Update停止分のdashTimerを各対象フレーム1回だけ進める。入力停止、報酬、日末報告では残ったダッシュ入力を解除する。
BOOSTは実在庫の歯車をnative BoostActiveが消費する。試験ごと最大2回、間隔5.5秒以上、EK装甲中の新規発動は見送る。素材追加やHP/GATE補充は行わない。
bot_mode、reactive_intercept_target、reactive_dash、reactive_dash_blocked、reactive_boostをevents.csvに記録。迎撃の照準選択や攻撃は試行であり、撃墜成功数とはみなさない。通常射撃・命中処理・cooldownは基準版と同じ。
Reactive版は約46KBのC#6実行body。静的な射撃ブロック一致と基準版ファイル未変更を確認済み。Unityでのコンパイルと実測はrootが行う。
