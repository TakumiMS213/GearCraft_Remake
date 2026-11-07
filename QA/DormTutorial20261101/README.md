# 宿舎チュートリアル変更（2026-11-01）

## 実装した挙動

- ゲーム開始直後、DAY1完了前はベッド以外の家具に近づいても説明・操作案内を自動表示しない。冒頭の宿舎到着メッセージは既存のベッド案内を維持する。家具の利用機能そのものを禁止する変更ではない。
- `StageFlowManager` が防衛を終えて休憩先のCraftSpaceへ遷移するときに `DormTasks.hasCompletedFirstDay` を立てる。単にMainへ入る、最初のウェーブを始める、Craftから戻るだけでは立たない。現設定では1日3ステージなので、DAY1終了の帰還はステージ3の終了後。
- 初回帰還のロード演出完了後に「門修復 → 食事 → 治療 → クラフト」を案内。各SpriteRendererの `bounds.center` にカメラのX/Yを移動し、カメラのZは保持する。移動中はメッセージを隠し、移動完了後に表示する。
- 移動1.2秒、表示はフェード完了後8秒で自動送り。クリック・Space・Enterでも進行する。移動中の入力は移動の完了だけ、フェード中の入力はフェードの完了だけを行い、その家具の説明を飛ばさない。
- 案内中は歩行・家具操作・メニューのキーボード操作を既存の共通フラグで停止。シーン配置の透明な全画面Imageで背後のメニューボタンへのクリックも遮断する。
- 最後まで説明した後、開始前のカメラ位置・ズームへ戻して入力を再開する。この時に `hasSeenFurnitureTutorial` を立てる。Craft往復・次の日の帰還では繰り返さない。途中中断は未完了のままとし、次の宿舎入室で最初から案内する。
- 両フラグは既存の周回状態 `DormTaskProgress` に保持。通常のニューゲーム・プレイヤーデータリセットで初期化。PlayerPrefsへ新しい恒久キーは追加していない。

## シーン・文書

- `CraftSpace.unity` にArrivalMessageCanvas / ArrivalMessageWindow / Card / Portrait / MessageTextを配置（22シリアライズブロック）。既存メッセージViewの実行時UI生成を撤去し、シーン参照だけで表示する。
- 既存ブロックの変更はArrivalMessagePresenter、ArrivalMessageView、SceneRootsの3件のみ。家具・カメラ・既存の他UIは移動していない。
- 説明文は `Assets/GearCraft/ScriptableObject/Story/DormFurnitureTutorial.asset` に保存。カメラの注視先はPresenterに4件のシーン参照を指定。
- 門修復は既存の `World_Flower`（GateRepair）、食事は `World_Freeze`、治療は `World_Medical`、クラフトは `World_CraftTable` を対応させる。見た目の名前だけでなく各InteractionZoneのactionTypeを照合した。
- Unityオブジェクトをスクリプトから生成する処理は追加していない。

## 検証

実行コマンド：`python3 QA/DormTutorial20261101/run_checks.py`

- Unity 6000.0.67f1のRoslynで、現行の全ランタイムソースをEditor／Player両定義でコンパイル成功。一時ディレクトリへ出力し、Libraryのアセンブリ・レスポンスファイルは変更していない。
- 本番のPresenter、Model、SequenceData、DormTaskProgress、StoryPlaybackRequestを使用した36アサーション成功。カメラ・Tween・Viewは純粋なマネージド代替実装であり、Unity実機テストではない。
- 確認したケース：初回訪問、DAY1開始のみ、既存ベッド案内、帰還ロード待機、4家具の順序・中心座標、移動中クリックで説明を飛ばさない、フェード完了、カメラ復帰、再入室、中断後再開、新周回リセット、自動送り、UI参照不足時のロック回避。
- シーンIDの一意性・親子参照・Presenter/View接続、4つの注視先とactionType、説明アセットの順序、静的フォントの全必要文字を照合。`git diff --check` 成功。
- `UIRangeDisplay` のDAY1未完了ガードと、正常な日終了の帰還分岐だけが完了フラグを立てることをコードで確認した。

## 未検証

Unity MCPは接続インスタンス0件。実画面での家具の見え方、カメラ移動、文章の折り返し、入力と音、Playerの通しプレイは未検証。実セーブ・実PlayerPrefsは操作していない。

実Editorでは新規ゲームからベッド→DAY1完了→帰還の流れを確認し、順序・カメラ中心・メッセージ位置・入力復帰を確認する。続けてCraft往復で再発しないこと、別の新規ゲームで再び案内されることを確認する。
