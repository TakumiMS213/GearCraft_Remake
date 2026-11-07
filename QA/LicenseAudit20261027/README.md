# 素材ライセンス確認（2026-10-27 / Asia/Tokyo）

## 結論

確認できた配布元の規約では、通常の商用ゲームへの組み込みを禁止する素材は見つからなかった。ただし、**ゲーム全体を商用利用可能と確定することはできない**。実際に参照されている `gear2.ogg`、画像・イラスト、EmojiOneサンプルの適用ライセンスが未確認。

出典表示を追加しても、出典不明素材の利用許諾が得られるわけではない。素材の削除・音の差し替えは行っていない。

## 確認範囲と方法

- `Assets/` 内の音源18点、フォント2点、PNG/JPG251点を棚卸し。パス・SHA-256・直接参照元を [asset-inventory.json](asset-inventory.json) に記録。
- 有効なBuild Settingsの7シーンとEditor以外のResourcesを起点に、GUID参照を再帰追跡。音源13点、フォント2点、出典未確認の画像181点、EmojiOne1点がPlayerに含まれる候補。実際のPlayerビルドによる収録確認ではない。
- 画像251点の内訳：一般画像222点、開発スクリーンショット23点、DOTweenのEditor画像5点、EmojiOne1点。一般画像のPNGメタデータ・同梱文書から出典を確定できなかった。外観から作者・AI生成・購入権利を推測していない。
- 音源のファイル名、ID3、公式配布ページ、規約を照合。取得できた公式音源7点はSHA-256一致を確認。[比較結果](official-audio-matches.json)。効果音ラボ2点は配布ページと名称が一致するが直接ダウンロードがHTTP 403となり、ハッシュ比較は未実施。
- 素材に加え、使用中のDOTweenとUniTaskのライセンス・同梱通知を確認。Unity本体、全PackageCache、外部SDKすべての完全なライセンス監査や契約状況の確認までは含まない。

## 音源

パスはすべて `Assets/GearCraft/Audio/`。表の「可」は、記載の配布元の通常の商用ゲーム向け利用条件を満たす場合を指す。ファイル名だけで判定したものと、実ファイルを照合できたものを区別する。

| ファイル | 配布元・確認根拠 | 商用利用・表記条件 | 参照状況 |
| --- | --- | --- | --- |
| Steam-and-Steel-Skies.mp3 | [OtoSound / クリエイター【オト】](https://creatoroto-blog.com/freebgmsteamandsteelskies/)。公式DLとSHA-256一致 | 可、クレジット必須。[規約](https://creatoroto-blog.com/musiclicense/) | Main |
| PerituneMaterial_Steam_Fortress.mp3 | [PeriTune / むつき醒](https://peritune.com/blog/2020/05/15/steam_fortress/)。公式DLとSHA-256一致、2020-05-15公開 | 可、CC BY 4.0。作者・出典・ライセンス・改変有無を表記。[規約](https://peritune.com/about/)では2026年2月以前の曲にCC BY 4.0が継続適用 | Title |
| inishienohiseki.mp3 | [甘茶の音楽工房「古の碑石」](https://amachamusic.chagasi.com/music_inishienohiseki.html)。ID3の曲名・作者が一致 | 可、表記任意。[規約](https://amachamusic.chagasi.com/terms.html) | CraftSpace |
| maou_bgm_acoustic49.mp3 | [魔王魂「アコースティック49／悠久の地」](https://maou.audio/bgm_acoustic49/)。公式の配布名と一致 | 可、クレジット必須。[規約](https://maou.audio/rule/) | Craft |
| maou_se_system17.mp3 | [魔王魂「システム17」](https://maou.audio/se_system17/)。公式の配布名と一致 | 可、クレジット必須、同上 | Craft・CraftSpace |
| 決定ボタンを押す7.mp3 / 決定ボタンを押す38.mp3 | [効果音ラボ](https://soundeffect-lab.info/sound/button/)。配布名一致、ハッシュ未照合 | 可、表記任意。[規約](https://soundeffect-lab.info/agreement/) | 7はCraft、38は直接参照なし |
| 決定1.mp3 / 決定5.mp3 | [Springin’ Sound Stock](https://www.springin.org/sound-stock/subcategory/systemsound/)。両方とも公式DLとSHA-256一致 | 可、通常は表記任意、指定の表記を求められた場合は従う。[規約](https://www.springin.org/sound-stock/guideline/) | Craft・CraftSpace |
| 大型ロボットの駆動音2.mp3 / 3.mp3 / 7.mp3 | [Springin’ Sound Stock](https://www.springin.org/sound-stock/subcategory/big_robot/)。3点とも公式DLとSHA-256一致 | 可、同上 | 2はMain、7はCraft、3は直接参照なし |
| clean-machine-gun-burst-98224.mp3 | [Pixabay、qubodup (Freesound) / freesound_community](https://pixabay.com/sound-effects/film-special-effects-clean-machine-gun-burst-98224/)。名称と素材ID一致 | 可、表記任意。[Content License](https://pixabay.com/service/license-summary/)・[規約](https://pixabay.com/service/terms/) | Main |
| 080997_bullet-39735.mp3 | [Pixabay / freesound_community](https://pixabay.com/pt/sound-effects/filme-e-efeitos-especiais-080997-bullet-39735/)。名称と素材ID一致 | 可、同上 | Main |
| gunfire-single-shot-colt-peacemaker-94951.mp3 | [Pixabay、johnyridgeback (Freesound) / freesound_community](https://pixabay.com/sound-effects/film-special-effects-gunfire-single-shot-colt-peacemaker-94951/)。名称と素材ID一致 | 可、同上 | 直接参照なし |
| handgun-sound-effect-432110.mp3 | [Pixabay / u_b6hqzcpu1z](https://pixabay.com/sound-effects/film-special-effects-handgun-sound-effect-432110/)。名称と素材ID一致 | 可、同上 | 直接参照なし |
| gear2.ogg | 出典不明。ユーザー回答：ネット上から入手、自作・自己生成AI素材ではない | **未確認**。作者・配布URL・利用条件の確認、または許諾の明確な素材への差し替えが必要 | **Title、GearBlackoutTransition.prefabで使用** |
| gear3.ogg | 同上。OGGにはエンコーダ情報のみ | **未確認**。同名のネット検索結果を出典とは認定していない | 直接参照なし。Resources外。静的参照ではPlayer候補外 |

Pixabay等の音源単体の再配布・素材販売とゲームへの組み込みは別。購入履歴・当初のダウンロード証明はリポジトリ内にない。今後の素材集配布やContent ID登録まで許可されたという判断ではない。

## フォント・画像・コード

| 対象 | 判断・根拠 | 対応 |
| --- | --- | --- |
| DotGothic16-Regular.ttf、派生TMPフォント | TTF内著作権・ライセンス名を確認。[公式OFL](https://raw.githubusercontent.com/fontworks-fonts/DotGothic16/master/OFL.txt)。商用ゲームへの組み込み可。著作権とOFL全文を添付 | ライセンス全文を同梱。UIは既存の静的TMPフォントを参照。全文の文字収録を再確認し、動的フォント生成は撤去した |
| LiberationSans.ttf、派生TMPフォント | 同梱 `TextMesh Pro/Fonts/LiberationSans - OFL.txt` とTTF内表記。OFL 1.1 | 著作権と同梱OFL全文をUIから閲覧可能にした |
| Images以下・SofumeLogoなど一般画像222点 | 出典・自作権利・購入権利の根拠なし。181点がPlayer候補 | **未確認**。ファイルごとの作者・配布元・利用許諾を確認する必要あり |
| TextMesh Pro/Sprites/EmojiOne.png | 同梱Attributionあり。ただし版・対応する商用条件を特定できず。TMP Settings → Resources/Sprite Assets/EmojiOne.asset → PNGという参照が存在 | **未確認**。旧版のCC BYと現行JoyPixelsの条件を混同しない。同梱Attributionを保持・表示したが、これで許諾確認済みとはしない |
| DOTween | 標準版DLL・Modules・readmeを確認。[標準版ライセンス](https://dotween.demigiant.com/license.php)は商用利用可。Proの購入権利が確認できたという意味ではない | 既存readmeをそのまま同梱・閲覧可能にした |
| UniTask 2.5.10 | `Packages/packages-lock.json` のcommit `a9e27c03d411d2fca01cc7410c24c97cd77cb539` の[MIT全文](https://raw.githubusercontent.com/Cysharp/UniTask/a9e27c03d411d2fca01cc7410c24c97cd77cb539/LICENSE)を確認 | 著作権・MIT全文を同梱・閲覧可能にした |

## 実装

- タイトルSETTINGSの左側に、標準UI Imageを組み合わせたメールボックスと「LICENSES」ラベルをシーン配置。追加アイコンに第三者素材は使用していない。
- メールボックス、モーダル、スクロール本文、スクロールバー、前へ／次へ／閉じるはすべて `Title.unity` に保存。`TitleLicenseController` は既存オブジェクトへの参照を持ち、開閉・ページ切替・操作制御のみを行う。各ButtonのOnClickもシーンに保存。
- 文書は `Assets/GearCraft/Licenses/*.txt` をシーンから直接参照。インターネット接続なしで読め、リンク先を開く必要なくOFL・MIT全文を確認できる。
- 開いている間は背後のSelectableを操作不可にし、閉じた際に元の状態へ戻す。Escはゲーム終了より先にライセンス画面を閉じる。ホイール・ドラッグ・スクロールバー・PageUp/PageDown/Home/Endで本文を閲覧できる。
- クレジットにはプロジェクト内に存在する任意表記素材も含めた。未確認素材について架空の作者・ライセンスは記載していない。

## 未完了の確認

1. gear2.ogg、gear3.ogg、一般画像、EmojiOneの出典・権利を確認する。gear2は現在のゲームで実際に使用中。
2. Unity上でタイトル表示、メールボックスの位置と見た目、長文の最下部、ページ切替、文字欠け、Esc、Settings/Galleryとの重なり、Playerでのオフライン表示を確認する。Unity MCPは接続インスタンス0のため実操作未検証。

クレジット追加だけで上記の未確認事項を解消済みとは扱わない。

## 初回実装時の検証結果

- Unity 6000.0.67f1付属Roslynと既存のEditor/Playerレスポンスファイルを用いて、追加スクリプトを含む全ランタイムC#を一時ディレクトリへコンパイル：両方成功。Library内の既存アセンブリは置換していない。
- `python3 QA/PlayerDataReset20261027/run_checks.py`：上記2構成のコンパイルおよび既存リセット処理の50アサーション成功。実セーブには触れていない。このテストはライセンスUIの実操作検証ではない。
- 新規コントローラーのシーン接続1件、TextAsset6件とGUIDの対応、TTF参照、OFL2件とMIT全文の原文一致を確認。
- DotGothic16 TTFのcmapを確認し、6文書に含まれる印字文字の未収録は0。動的TMPアトラスによる画面描画そのものは未検証。
- 棚卸しした271ファイルのSHA-256はすべて保持。既存素材の置換・削除なし。
- 対象シーン・既存スクリプトの `git diff --check` 成功。Unity MCPの接続インスタンス数0を確認し、実Editor/Player表示は未検証として残した。

## シーン配置への修正（2026-10-27）

- ユーザーの指示に従い、実行時のUI生成を撤去。Titleシーンに116シリアライズブロックを追加し、メールボックスとライセンス画面を編集時から確認・調整できるようにした。既存ブロックの変更はCanvasの子一覧とライセンスコントローラー参照の2件のみ。
- 既存 `TitleLicenseController.cs` を操作処理だけに縮小。不要になった前回追加の `TitleMailboxGraphic.cs` とmetaを削除し、アイコンは標準Imageの配置へ置換。新しいスクリプトは追加していない。
- 既存の静的TMPフォント `DotGothic16-Regular SDF.asset` の文字テーブルを照合し、6文書とUIラベル・エラーメッセージの全印字文字が収録されていることを確認。「静的アトラスの文字不足」という前回の説明は誤りだった。今回、専用フォント生成とその破棄処理も撤去した。
- シーン内IDの一意性、親子の双方向参照、各コンポーネント型、4ボタンのOnClick接続、初期表示するクレジット全文、ライセンス文書6件の参照保持を確認。
- 既存の検証ランナーをそのまま実行すると、Unityが未更新のため生成済みレスポンスファイルが削除した旧アイコンソースを指してコンパイルに失敗。一時コピーからその古い参照だけを除き、現在の全ランタイムC#をEditor/Playerの両定義でコンパイルして成功。Libraryのレスポンスファイル・アセンブリは変更していない。
- 実Editor/Playerでの見た目・操作は引き続き未検証。上記「未完了の確認」の実画面チェックを継続する。棚卸しJSONの参照到達性は初回ライセンス調査時の記録であり、このUI修正後の再ビルド結果ではない。
