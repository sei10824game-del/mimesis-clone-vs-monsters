# Changelog

全ての注目すべき変更はこのファイルに記録されます。

## [1.0.0] - 2026-11-02

### 追加
- 初回リリース
- 偽物がプレイヤーに敵対しないようにする機能 (`PreventFakeAttackPlayer`)
- 偽物同士が敵対しないようにする機能 (`PreventFakeAttackFake`)
- プレイヤーが偽物を誤爆しないようにする保護機能 (`PreventPlayerAttackFake`)
- 偽物がモンスターに敵対するようにする機能 (`MakeFakeAttackMonster`)
- モンスターが偽物を襲う囮モード (`MakeMonsterAttackFake`, デフォルト無効)
- 毎フレーム補正コントローラ `MimicAllyController` によるフォールバック
- ゲームバージョン差異に対応する動的型解決 (`TypeResolver`)
- 詳細ログ出力 (`VerboseLog`, F3でデバッグ情報表示)
- `MelonPreferences.cfg` による設定管理
- MimicAPI 対応（任意依存）

### 技術
- MelonLoader 0.7.3 / HarmonyX 対応
- netstandard2.1 / MIMESIS 0.3.x - 0.4.x 対応
