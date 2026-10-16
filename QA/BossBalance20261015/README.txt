ボス調整後の比較準備（2026-10-15）

このフォルダは今回の調整後の記録専用です。以前のQA/WeaponUpgradeBalance20261015の測定・バックアップは変更していません。
コピー元はpreparation-manifest.jsonにSHA256付きで記録。C#5本は内部folder参照だけをこのフォルダへ変更し、loadouts.csvとloadout-placements.csvはバイト一致のコピーです。22構成の部品名・個数と配置表の一致を確認しました。
before系、PlayerPrefs、Editor状態、旧測定結果はコピーしていません。Unity/MCP操作・ゲーム側変更は行っていません。

実行前の今回分バックアップ（rootが取得）
・playerprefs-before.txt：この開始時点のPlayerPrefs。旧フォルダからコピーしない。
・editor-before.txt：scene path、vSyncCount、targetFrameRate、runInBackground、captureDeltaTime、fixedDeltaTimeを各行に格納する既存形式。
・gameview-before.txt：selectedSizeIndex、その後にサイズ表示一覧を各行に格納する既存形式。
・logger-before.txt：現在のDebug.unityLogger.filterLogTypeの数値。RestoreSessionで存在時に復元する。
これらは今回の状態を退避した後、検証終了まで上書きしない。ソースの比較基準も今回の調整後にrootが取得する。

初回比較：M20のみ5件
cases.csvにはfirst-pass-cases.csvと同じ5行を設定済み。
batch-name.txtはboss-m20-pass1。
全てstage31、seed77、mode=boss、maxSeconds=240。EK単体を出し、前座は出さない。

GearCraft      final_day11_melee_module20
SteamCannon    final_day11_ranged_fitted_speed_module20
SteamThrower   final_day11_thrower_fitted_damage_module20
RailCraft      final_day11_ranged_fitted_speed_module20
SteamGatling   final_day11_ranged_module20

M20はloadouts.csvのmoduleDamage=20を意味する。実際の武器ダメージは新しいStatusManager.GetWeaponDamage等のゲーム側コードで計算する。旧測定値や旧ダメージ計算を固定していない。
Stage31ではSTR/ACC=20、追加の武器専用報酬なし、Punk/BOOST/Barrier/Repairなし。パーツ効果は現行UpgradePartSOを読み、RecalculateUpgradeEffectsで計算する。敵prefab・AI・弾・衝突も現行コードを使用する。

初回の起動手順
1. 今回のバックアップ取得と、他のQA callbackが動いていないことを確認する。
2. TitleをPlay。native-survival-config.csvはGearCraft,31,final_day11_melee_module20,77を設定済み。
3. NativeSurvivalBootstrap.cs.txtをexecute_codeのC#6メソッド本文として実行し、native-survival-bootstrap-progress.txtのREADYを待つ。
4. PrepareControlledMain.cs.txtを実行し、controlled-main-progress.txtのREADYを待つ。最初のShowDayAsync→StartNextStageが終わり、初期敵が消去されたtimeScale=0のMainを用意する処理。新規Mainでは省略しない。
5. UpgradeCombatBenchmark.cs.txtを実行。benchmark-progress.txtで進行し、COMPLETEまで待つ。
6. boss-m20-pass1-results.csvと同prefixのspawns、loadout-stats/effects、meta等が出力される。errors.txtが生じた場合は内容を確認する。

比較候補
comparison-candidates.csvは初回M20の5件＋M0の3件、計8件の案。初回から全8件を実行する設定にはしていない。
followup-module0-cases.csvはCannon/Rail/Gatlingの3件のみ。Cannon/Railはfitted_speed、Gatlingはfinal_day11_ranged。moduleDamage以外は各M20の対応構成と同じ。
初回結果を見て追加する場合、callback完了後にこの3行をcases.csvへ設定し、batch-name.txtを未使用のboss-m0-pass1等へ変更する。
ランナーは同じbatch名の結果ファイルを開始時に作り直すため、過去のbatch名を再使用しない。再測定時もpass2等の別名で保存する。

通常耐久の確認を行う場合
Title Playからnative-survival-config.csvのweapon,startStage,profile,seedを1行設定し、NativeSurvivalBootstrapのREADY後にNativeSurvivalRoute.cs.txtを実行する。
この経路ではPrepareControlledMainを呼ばない。StageFlow/Spawnerをそのまま進め、HP/門耐久を補充せず、一日帰還・死亡/門破壊・Ending到達等で止める。
native-survival-name.txtを設定する場合は未使用ラベルにする。未設定なら武器・開始wave・時刻を含む名前を自動生成する。終了後のrestore-verified.txtにEditorPlaying=False・今回の退避値一致が記録されるまで次を開始しない。

読み取りと最終復元
処理能力比較はHP/門耐久を毎フレーム補充し、caseごとにリセットする。240秒以内に残HPがあれば観測上限での打切で、通常プレイで撃破不能とは判定しない。ボス出現→撃破はboss_secondsを使用し、全体secondsや演出待ちと混同しない。
旧60秒probeや前座付きwave試験と比較する際は、構成・Module・係数変更・ボスHP・観測窓を明記する。新しいボス調整が含まれるため、差を武器だけに帰さない。
全callback終了後、Playを止めてRestoreSession.cs.txtを実行し、final-restoration.txtと実際のPrefs/画面/時間設定を確認する。RestoreSessionは元のTitleがcleanであることと、退避したGameViewサイズ一覧との一致を要求する。
既存のサイズ削除処理はQAWeaponBalanceで始まるカスタムサイズを対象としている。今回追加する場合もその名前を使うか、元一覧に戻してからRestoreSessionを実行する。成功メッセージのFull HD index3部分は旧コードの固定文言なので、実際の選択はgameview-before.txtのindexと照合する。
