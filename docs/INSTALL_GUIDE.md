# インストールガイド（詳細）

## 初めてMODを入れる方へ

MIMESIS は Steam で配信されているゲームです。MODを入れるには `MelonLoader` というMODローダーが必要です。

### 手順1: MelonLoader をインストール

1. https://github.com/LavaGang/MelonLoader/releases から最新の `MelonLoader.Installer.exe` をダウンロード
2. 実行し、`Select` で MIMESIS のインストールフォルダを選択
   - Steam デフォルト: `C:\Program Files (x86)\Steam\steamapps\common\MIMESIS`
   - わからない場合: Steamで MIMESIS を右クリック → 管理 → ローカルファイルを閲覧
   - 中に `MIMESIS.exe` があるフォルダを選ぶ
3. `Install` をクリック
4. 一度ゲームを起動する（自動で閉じる場合あり — 正常です。MelonLoaderが初期化しています）
5. `MIMESIS/MelonLoader/` フォルダが作成されていれば成功

### 手順2: 本MODをインストール

1. 本リポジトリの [Releases](https://github.com/sei10824game-del/mimesis-clone-vs-monsters/releases) から `CloneVsMonsters.dll` をダウンロード
   - または自分でビルドした場合は `CloneVsMonsters/bin/Release/netstandard2.1/CloneVsMonsters.dll`
2. `MIMESIS/Mods/` フォルダにコピー
   - フォルダがなければ作成する
3. （推奨）MimicAPI も同様に `MIMESIS/Mods/` に入れる
   - https://thunderstore.io/c/mimesis/p/DooDesch/MimicAPI/ または https://github.com/DooDesch-Mods/MimicAPI/releases

### 手順3: 確認

1. ゲームを起動
2. `MIMESIS/MelonLoader/Latest.log` を開き、以下が表示されているか確認

```
[CloneVsMonsters] CloneVsMonsters v1.0.0 - Initializing...
SUCCESS: All Harmony patches applied!
Active patches:
  [1] Damage Block   - 偽物→プレイヤー/偽物 のダメージを無効化
  [2] Faction        - 敵対判定を書き換え
  [3] Targeting      - 偽物のAIターゲットをモンスターへ誘導
  [4] AllyController - 毎フレーム補正
```

表示されていれば成功です。

### 手順4: 遊び方

1. ホストがロビーを作成
2. 通常通りダンジョンへ出発
3. 偽物が出現しても、プレイヤーを襲わずモンスターへ向かっていきます
4. 一緒にモンスターを倒しましょう

## r2modman / Gale を使う場合

1. r2modman または Gale を起動
2. ゲーム `MIMESIS` を選択 → プロファイルを作成
3. `Online` タブで `CloneVsMonsters` を検索してインストール
4. `Start modded` で起動

依存関係は自動で解決されます。

## アップデート

新しいバージョンが出た場合、古い `CloneVsMonsters.dll` を新しいものに置き換えるだけです。

## アンインストール

`MIMESIS/Mods/CloneVsMonsters.dll` を削除してください。即座にバニラの挙動に戻ります。

## 設定変更

`MIMESIS/UserData/MelonPreferences.cfg` をメモ帳などで開き、`[CloneVsMonsters]` セクションを編集します。

```ini
[CloneVsMonsters]
Enabled = true
PreventFakeAttackPlayer = true
PreventFakeAttackFake = true
PreventPlayerAttackFake = true
MakeFakeAttackMonster = true
MakeMonsterAttackFake = false
VerboseLog = false
```

- `MakeMonsterAttackFake = true` にすると、モンスターが偽物を積極的に狙う「囮モード」になります。
- `VerboseLog = true` にすると、デバッグ用に F3 キーで偽物/モンスター数がログに出ます。

保存後、ゲームを再起動するか、ロビーに戻ると反映されます。

## トラブルシューティング

### MODが読み込まれない

- `MIMESIS/Mods/CloneVsMonsters.dll` が正しい場所にあるか確認
- MelonLoader が正しくインストールされているか `MelonLoader/Latest.log` の先頭に `MelonLoader v0.7.x Open-Beta` と表示されるか確認
- DLL がブロックされている場合: エクスプローラーで DLL を右クリック → プロパティ → 「ブロックの解除」にチェック → 適用

### 偽物がまだプレイヤーを攻撃する

- ゲームが最新バージョンで内部構造が変わっている可能性があります
- `VerboseLog = true` にして `Latest.log` を取得し、Issue に報告してください
- 暫定的に `MimicAllyController` のフォールバックが効くまで1-2秒待ってみてください

### 他MODでクラッシュする

- 一度すべてのMODを外し、本MOD単体で起動できるか確認
- 競合する場合はロード順を変更するか、設定で一部機能を無効化してください
- 特に同じダメージ系をパッチするMODとは競合する可能性があります
