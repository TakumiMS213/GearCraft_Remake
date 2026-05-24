# アップグレードパーツ一覧

参照元: `Assets/GearCraft/ScriptableObject/UpgradeParts`

## 一覧

| パーツ名 | レアリティ | サイズ | 形状 | 効果 | コスト | 制限 |
| --- | --- | --- | --- | --- | --- | --- |
| Damage Plate | コモン | 1x1 | `■` | ダメージ増加 小 | Scrap x3 | なし |
| Speed Gear | コモン | 1x2 | `■` / `■` | 攻撃速度10%増加 | Scrap x4 | なし |
| Speed Gear+ | アンコモン | 1x2 | `■` / `■` | 攻撃速度15%増加 | Scrap x6, Gear x1 | なし |
| Speed Gear++ | レア | 1x2 | `■` / `■` | 攻撃速度20%増加 | Scrap x8, Gear x2, Upgrade Core x1 | なし |
| Precision Nozzle | コモン | 1x2 | `■` / `■` | 拡散低下 大 | Scrap x4, Gear x1 | 遠距離武器のみ |
| Reinforced Frame | コモン | 2x3 | `■■` / `■□` / `■■` | 最大耐久増加 中 | Scrap x4, Gear x3 | なし |
| Heavy Slug | アンコモン | 2x2 | `■■` / `■■` | 弾サイズ25%増加, ダメージ増加 小 | Gear x4 | 遠距離武器のみ |
| Heavy Slug+ | レア | 2x2 | `■■` / `■■` | 弾サイズ50%増加, ダメージ増加 中 | Gear x5, Upgrade Core x1 | 遠距離武器のみ |
| Heavy Slug++ | エピック | 2x2 | `■■` / `■■` | 弾サイズ75%増加, ダメージ増加 中 | Gear x7, Upgrade Core x2 | 遠距離武器のみ |
| Junk Magnet | アンコモン | 3x1 | `■■■` | 素材ドロップ率35%増加, 回収範囲増加 中 | Scrap x6 | なし |
| Junk Magnet+ | レア | 3x1 | `■■■` | 素材ドロップ率60%増加, 回収範囲増加 中 | Scrap x8, Upgrade Core x1 | なし |
| Junk Magnet++ | エピック | 3x1 | `■■■` | 素材ドロップ率90%増加, 回収範囲増加 大 | Scrap x10, Upgrade Core x2 | なし |
| Unstable Chamber | アンコモン | 2x2 | `■■` / `■■` | ダメージ増加 中, 拡散増加 大 | Scrap x5, Gear x2 | 遠距離武器のみ |
| Ricochet Cog | レア | 2x2 | `■□` / `■■` | 跳弾増加 小 | Gear x3, Upgrade Core x1 | 遠距離武器のみ |
| Twin Barrel | レア | 2x1 | `■■` | 弾数100%増加 | Gear x2, Upgrade Core x1 | 遠距離武器のみ |
| Drain Valve | エピック | 1x3 | `■` / `■` / `■` | 耐久吸収率15%増加 | Scrap x2, Upgrade Core x2 | なし |
| Drain Valve+ | エピック | 1x3 | `■` / `■` / `■` | 耐久吸収率25%増加 | Scrap x3, Upgrade Core x3 | なし |
| Drain Valve++ | エピック | 1x3 | `■` / `■` / `■` | 耐久吸収率40%増加 | Scrap x4, Upgrade Core x4 | なし |

## メモ

- レアリティ定義: コモン = 0、アンコモン = 1、レア = 2、エピック = 3。
- 形状は `■` が占有マス、`□` が空きマス。
- 固定値系の増減は、値の大きさに応じて小・中・大・超・極で表記。
- 倍率・確率系の増減は、%増加/%低下で表記。
- コストの素材名は `MaterialManager.MaterialType` の正式名称に合わせて表記。
- ショップ候補に登録されているアップグレードパーツ数: 18種類。
