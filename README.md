# CloneVsMonsters — ミメシス 偽物 vs モンスター MOD

## 概要

MIMESIS（ミメシス）で**偽物（ミメシス/クローン）がプレイヤーと他の偽物に敵対せず、モンスターに敵対する**ようにするMelonLoader MODです。

- ✅ 偽物はプレイヤーを攻撃しない
- ✅ 偽物同士で戦わない（味方扱い）
- ✅ 偽物はモンスターを優先的に攻撃する
- ✅ プレイヤーが偽物を誤爆しないように保護（設定可能）
- ✅ 既存のセーブ・ワールドに影響せず、MODを外せば元に戻る

> **ホスト専用MOD**：マルチプレイではホストにだけ導入すれば全員に効果があります。ゲストは導入不要です。

---

## 動作イメージ

```
[通常]  偽物 → プレイヤー を攻撃（裏切り）
[MOD後] 偽物 → モンスター を攻撃（共闘）
        偽物 → プレイヤー 攻撃しない
        偽物 ↔ 偽物 味方
```

偽物が味方化することで、トリック要素は弱まる代わりに**「偽物と協力してモンスターを倒す」**新しい遊び方ができます。ソロ探索の護衛や、初心者案内、検証用にも便利です。

---

## 必要環境

| 項目 | バージョン |
|------|-----------|
| MIMESIS (Steam版) | 0.3.0 / 0.3.1 / 0.3.2 / 0.4.0 対応（それ以上でも動く可能性あり） |
| MelonLoader | 0.7.3 以降 (x64) |
| MimicAPI (任意) | 0.4.0 以降 — あるとより安定して動作、無くても動作します |
| .NET SDK | 8.0 以降（ビルド時のみ） |

---

## インストール

### 自動（推奨）

1. [MelonLoader 0.7.3](https://github.com/LavaGang/MelonLoader/releases) を MIMESIS フォルダにインストール
   - Steamで MIMESIS を右クリック → 管理 → ローカルファイルを閲覧
   - インストーラでそのフォルダを選択 → Install
   - 一度ゲームを起動して自動で閉じるまで待つ（初期化）
2. `CloneVsMonsters.dll` を `MIMESIS/Mods/` にコピー
3. （任意）`MimicAPI.dll` も `MIMESIS/Mods/` に入れると精度が上がります
4. ゲーム起動

### Thunderstore / r2modman / Gale

Thunderstore パッケージ `CloneVsMonsters` を追加するだけです。依存関係は自動解決されます。

### アンインストール

`MIMESIS/Mods/CloneVsMonsters.dll` を削除するだけで元に戻ります。セーブデータは汚れません。

---

## 設定

初回起動後、`MIMESIS/UserData/MelonPreferences.cfg` に以下が生成されます。

```ini
[CloneVsMonsters]
# MOD全体の有効/無効（ゲーム内で即時切り替え可能）
Enabled = true
# 偽物がプレイヤーを攻撃しないようにする
PreventFakeAttackPlayer = true
# 偽物同士が攻撃し合わないようにする
PreventFakeAttackFake = true
# プレイヤーが偽物を攻撃しても無効化（誤爆防止）
PreventPlayerAttackFake = true
# 偽物がモンスターを積極的に攻撃するようにする
MakeFakeAttackMonster = true
# モンスターが偽物を優先的に狙うようにする（偽物が囮になる）
MakeMonsterAttackFake = false
# ダメージログを出力（デバッグ用）
VerboseLog = false
```

`UserData/MelonPreferences.cfg` をテキストエディタで編集するか、MIMESIS Patches のようにゲーム内設定UIがある環境ではそちらからでも変更できます。変更は即時反映されます（再起動不要）。

---

## 仕組み（技術概要）

このMODは Harmony パッチで以下のゲーム内部ロジックに介入します。

1. **ダメージ無効化パッチ** — 偽物→プレイヤー、偽物→偽物、（設定により）プレイヤー→偽物のダメージをキャンセル
2. **敵対判定パッチ** — `IsEnemy` / `CanAttack` / `IsHostile` 系の判定を書き換え、偽物を味方、モンスターを敵として扱う
3. **AIターゲット誘導** — 偽物のAIがプレイヤーをターゲットに選んだ場合、最も近いモンスターへターゲットを差し替える
4. **毎フレーム補正（フォールバック）** — 上記パッチがヒットしないゲームバージョンでも、毎フレーム監視して偽物のターゲットをモンスターへ寄せる

ゲームのクラス名がバージョンで変わっても動くように、**実行時に Assembly-CSharp.dll をスキャンして候補となる型・メソッドを自動解決**します。見つからない場合は警告ログを出して残りのパッチだけで動作します。

詳細は [`docs/DESIGN.md`](docs/DESIGN.md) を参照。

---

## ビルド方法（開発者向け）

```bash
# 方法1: GamePath を直接指定
dotnet build CloneVsMonsters/CloneVsMonsters.csproj -c Release -p:GamePath="C:/Program Files (x86)/Steam/steamapps/common/MIMESIS"

# 方法2: 環境変数
export MIMESIS_PATH="C:/Program Files (x86)/Steam/steamapps/common/MIMESIS"
dotnet build CloneVsMonsters/CloneVsMonsters.csproj -c Release

# 方法3: Directory.Build.user.props を作成（gitignore済み）
# 後述のテンプレートを CloneVsMonsters/Directory.Build.user.props に保存

# 出力: CloneVsMonsters/bin/Release/netstandard2.1/CloneVsMonsters.dll
# ゲームフォルダへ自動コピーされる場合は _Test/MIMESIS/Mods/ にも配置されます
```

ゲーム本体が手元にない環境でも `dotnet build -p:SkipGameRefs=true` でスタブ参照のみでビルドできます（動作検証は不可、CI用）。

---

## 互換性

- **MimicAPI** があれば `PlayerAPI` / `ActorAPI` / `RoomAPI` を使ってより正確に味方/敵を判定します。無くてもリフレクションで同等の処理を行います。
- **BiggerLobby / MorePlayers** などのロビー拡張MODと併用可能です。
- **InfiniteStamina / MarkerMod / InventoryExpansion** など他MODとも競合しません（同一メソッドをパッチしていない限り）。
- ホストが本MODを入れていれば、ゲストはバニラでも効果を受けられます（サーバーサイド判定のため）。

---

## トラブルシューティング

| 症状 | 対処 |
|------|------|
| MODが読み込まれない（0 Mods loaded） | MelonLoaderが正しくインストールされているか確認。`Mods/CloneVsMonsters.dll` のパスが正しいか、`Latest.log` を確認。DLLがブロックされている場合は右クリック→プロパティ→「ブロックの解除」 |
| 偽物が依然プレイヤーを攻撃する | `VerboseLog=true` にしてログを確認。ゲームバージョンが新しくクラス名が変わっている可能性があります。Issueで `MelonLoader/Latest.log` を添えて報告してください |
| 他MODと競合してクラッシュ | 一時的に他MODを外して本MOD単体で起動できるか確認。競合する場合はロード順を変えるか、設定で一部パッチを無効化してください |
| 設定が反映されない | `MelonPreferences.cfg` のカテゴリ名が `CloneVsMonsters` になっているか確認。ゲーム起動中に編集した場合は一度ロビーに戻るか再起動してください |

ログは `MIMESIS/MelonLoader/Latest.log` に出力されます。報告時はこのファイルを添付してください。

---

## ロードマップ

- [x] v1.0.0 — 基本機能（偽物味方化 + モンスター敵対化）
- [ ] v1.1.0 — ゲーム内設定UI（Patchesチップ連携）
- [ ] v1.2.0 — 偽物の強化/弱体化スライダー
- [ ] v1.3.0 — 特定モンスターのみ敵対などの詳細フィルタ

---

## クレジット

- **ReLU Games, Inc. / KRAFTON** — 原作 MIMESIS
- **LavaGang / MelonLoader** — MODローダー
- **Harmony / HarmonyX** — パッチライブラリ
- **DooDesch / NeoMimicry / MimicAPI** — ゲームAPI解析と設計参考
- **medovanx / mimesis-patches** — ビルド構成参考
- 開発: sei10824game-del

## ライセンス

MIT License — 詳細は [LICENSE](LICENSE) を参照。ゲーム本体のファイルは含まれません。
