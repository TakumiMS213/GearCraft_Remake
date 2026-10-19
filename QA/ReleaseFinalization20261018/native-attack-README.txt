Native attack acceptance / projectile configuration probe

実行: 現セッションのcanonical 16-key manifestを保持したTitle Playで、entry fade完了後にProbeNativeAttack.cs.txtをexecute_codeへ渡す。
SessionStateのIDと10ファイルのSHA256が一致しない場合は起動しない。別probeがRUNNINGなら起動しない。
保存状態に触れるStatusのAwakeやPlayerのStartは走らせず、非activeの一時GameObject上にコンポーネントを作る。
既存Status/Materialのsingletonは置換しない。元オブジェクトと16キーのprobe開始時状態が終了時に同じか確認する。

48ケース: 実4武器×初期/終盤/BOOST設定×dt 30/60/120/jitter。
初期はSTR/ACC0・module0・パーツなし。終盤はSTR/ACC20・module20＋既に購入/配置検証済みの実パーツ構成をRecalculateUpgradeEffectsへ渡す。
構成はnative-attack-loadouts.csv。BOOST設定はmax(speed*.5,.05)とPlayer.isBoostActiveを適用する。Q操作の素材消費や5秒寿命は試さない。
各ケースでProcessAttackInput(bool,bool,float,Vector3)に0秒の初押下を渡し、共通10秒終点まで指定dtを与える。
初押下と10秒時点の発射を含む。Count期待値は武器別既知60Hz周期（初期60/120/2/5tick、終盤9/17/1/1、BOOST5/9/1/1）から独立に算出する。
各呼出しの後、実際に生成されたBullet/SteamThrower/LineHitboxのGameObjectを列挙し、発射受付数×Twin倍率、設定ダメージ、発射速度、貫通/跳弾設定を検査する。
ダメージ期待値は初期57/300/10/.3、終盤とBOOST102.6/532/31.6/4.564。カード追加なし。
各回の弾は検査直後に破棄する。これはnative Startや物理命中を含まない。描画fpsを実際に変更する試験でもない。

追加チェック: 本編TimeScale0とStageFlow.BlockPlayerInputForCurrentFrameによる発射拒否、解除後1回のみの再開、同時刻の再入力でCT短縮なし。
4アセットはuseLineHitbox=falseのため、LineHitboxは一時RailCraftクローンのflagだけtrueにして単発チェック。Twinありでも本編通り1判定、damage532、collider/visual生成を確認する。実アセットは変更しない。

出力: native-attack-cases.csv / native-attack-progress.txt / native-attack-result.txt。
ケースはEditor callbackごとに1件。180秒watchdog。成功/失敗とも一時物を破棄し、Random/Time/Input/Stage入力停止を復帰してPlayを自動停止する。
終了後はrootがcanonical backupを使いEditor/セーブを最終復元する。既存結果を自動上書きしない。

準備者はMCP実行なし。C#6静的コンパイルPASS: ProbeNativeAttack.compile.log。
