# CloneVsMonsters 設計資料

## 目的

MIMESIS で「偽物（ミメシス/クローン）」がプレイヤーおよび他の偽物を攻撃せず、モンスターを攻撃するように振る舞いを変更する。

## 背景

MIMESIS は最大4人協力のサバイバルホラーゲームで、以下の敵対関係がある：

- **プレイヤー**：現実の人間。スクラップを集めてトラムに帰還する。
- **偽物（ミメシス）**：プレイヤーの声・動きをAIで模倣して紛れ込む敵。通常はプレイヤーを襲う。
- **モンスター**：ダンジョンに配置された敵（罠系モンスター、巡回モンスターなど）。プレイヤーと偽物の両方を襲う。

本MODでは偽物を「味方NPC」に転換し、**偽物 vs モンスター**の構図を作る。

## 要求

> ミメシスで偽物がプレイヤーと他の偽物に敵対せずモンスターに敵対するようにする

- 偽物 → プレイヤー : 敵対しない
- 偽物 → 偽物 : 敵対しない
- 偽物 → モンスター : 敵対する（攻撃する）

追加で、プレイヤーが偽物を誤爆しないよう保護するオプションも提供する。

## アーキテクチャ

```
Core (MelonMod)
├─ CloneVsMonstersConfig (MelonPreferences)
├─ Harmony Patches
│   ├─ DamagePatches        : ダメージ無効化
│   ├─ FactionPatches       : 敵対判定書き換え
│   └─ TargetingPatches     : AIターゲット誘導
└─ Services
    └─ MimicAllyController  : 毎フレーム補正（フォールバック）
         └─ Utils/TypeResolver : 型の自動解決
         └─ Utils/ReflectionHelper : リフレクション支援
```

### なぜ複数のパッチが必要か

MIMESIS の内部実装はバージョンごとにクラス名・メソッド名が変わる可能性がある。単一のメソッドだけをパッチすると、別バージョンでは効果がなくなる。
そのため：

1. **ダメージ系**、**敵対判定系**、**ターゲット選択系**の3つのレイヤーでそれぞれパッチする
2. 各レイヤーで複数の候補メソッド名をスキャンして動的にパッチ
3. それでも漏れた場合は **MimicAllyController** が毎フレーム監視してターゲットを差し替える

これにより、どのバージョンでも少なくとも1つのレイヤーがヒットする。

## ゲーム内部構造の推定

実際の `Assembly-CSharp.dll` を dnSpy / ILSpy で解析した際の想定構造：

```
Namespace Mimic / Mimic.Actors
├── Hub (Singleton, Hub.s)
│   ├── pdata: PersistentData
│   │   └── main: MainScene
│   │       ├── GetAllPlayers() -> ProtoActor[]
│   │       └── GetMyAvatar() -> ProtoActor
│   └── vworld: VWorld
│       └── _vRoomManager: VRoomManager
│           └── _vrooms: Dictionary<long, VRoom>
│               └── VRoom (DungeonRoom, MaintenanceRoom, VWaitingRoom)
│                   ├── _vActorDict: Dictionary<int, VActor>
│                   │   ├── VPlayer (プレイヤーのサーバー側Actor)
│                   │   ├── VMonster (モンスター)
│                   │   └── VMimic / MimicActor (偽物 - 推定)
│                   ├── _vPlayerDict: Dictionary<int, VPlayer>
│                   └── IsPlayable() -> bool
├── ProtoActor : MonoBehaviour (クライアント側の見た目)
│   ├── ActorID, UID, dead
│   ├── transform.position
│   └── inventory
└── StatManager, MovementController (サーバー側VActorに紐づく)

MimicAPI はこれらをラップして PlayerAPI / ActorAPI / RoomAPI を提供する。
```

### 偽物の正体

偽物は以下のいずれかと考えられる：

- `VMonster` のサブクラス `VMimic` として存在
- `ProtoActor` に `isMimic` フラグを持つ `VPlayer` として存在
- 完全に独立した `MimicActor` 型

`TypeResolver.IsMimic()` はこれら全パターンをカバーするため、型名チェック＋フラグチェックの両方を行う。

## 各パッチの詳細

### 1. DamagePatches

**目的**: 偽物→プレイヤー等のダメージを物理的にブロック

**対象メソッド候補**:
`TakeDamage`, `ApplyDamage`, `OnDamage`, `OnHit`, `Hit`, `Damage`, `ReceiveDamage`, `ProcessHit`, `AddDamage`, `SetDamage`

**手法**:
- 既知の型（ProtoActor, VActor, StatManager等）の上記メソッドを全て探索して Prefix パッチ
- さらに Assembly-CSharp 内の全型をスキャンして同名メソッドを追加パッチ
- Prefix 内で `attacker` と `victim` を引数から推定し、`ShouldBlockDamage()` で判定
- ブロックすべきなら `return false` で元メソッドをスキップ

**ブロック条件**:
| attacker | victim | 設定 | ブロック? |
|----------|--------|------|-----------|
| 偽物 | プレイヤー | PreventFakeAttackPlayer=true | ✅ |
| 偽物 | 偽物 | PreventFakeAttackFake=true | ✅ |
| プレイヤー | 偽物 | PreventPlayerAttackFake=true | ✅ |
| モンスター | 偽物 | MakeMonsterAttackFake=false | ✅ |
| 偽物 | モンスター | - | ❌（通す） |

### 2. FactionPatches

**目的**: 敵対判定を書き換え、AIが最初から正しいターゲットを選ぶようにする

**対象メソッド候補**:
`IsEnemy`, `IsHostile`, `IsHostileTo`, `IsEnemyTo`, `CanAttack`, `CanTarget`, `IsValidTarget`, `ShouldAttack`, `IsAttackable`, `CheckHostility`

**手法**:
- 同様に既知型＋全スキャンで Prefix パッチ
- `attacker` と `target` を特定し `EvaluateFaction()` で判定
- 結果が確定すれば `__result` を上書きして `return false`

### 3. TargetingPatches

**目的**: 偽物のAIがプレイヤーをターゲットに選んだ直後に、モンスターへ差し替える

**対象メソッド候補**:
`SelectTarget`, `FindTarget`, `SearchTarget`, `ChooseTarget`, `UpdateTarget`, `SetTarget`, `FindNearestTarget`, `SearchVictim`, `ChooseVictim`, `SelectVictim`, `GetTarget`, `AcquireTarget`

**手法**:
- Postfix パッチ（元メソッドがターゲットを選んだ後に介入）
- `__instance` が偽物かつ `__result` がプレイヤーなら、`FindNearestMonster()` で最も近いモンスターを取得して `__result` を差し替え
- `FindNearestMonster()` は VRoomManager → VRoom → _vActorDict からモンスターを列挙し、距離でソート

### 4. MimicAllyController

**目的**: 上記パッチが全て外れた場合のフォールバック。ゲームのAIがバージョン差異でパッチできない場合でも、毎フレーム監視してターゲットを矯正する。

**動作**:
- 1秒ごとに `FindAllMimics()` と `FindAllMonsters()` を実行
- 各偽物について `_target` / `target` / `_currentTarget` 等のフィールドをリフレクションで取得
- ターゲットがプレイヤーなら、最も近いモンスターへフィールドを書き換え
- ターゲットが null なら、近くにモンスターがいれば `Attack` / `SetTarget` 等のメソッドを呼び出して攻撃を促す

**生存チェック**:
`IsAliveStatus()` → `dead` フィールド → `hp` フィールドの順でフォールバック

## 設定

`MelonPreferences.cfg` の `CloneVsMonsters` カテゴリで管理。`CloneVsMonstersConfig` がラップする。

- `Enabled` : 全体ON/OFF
- `PreventFakeAttackPlayer` : 偽物→プレイヤー無効化
- `PreventFakeAttackFake` : 偽物→偽物無効化
- `PreventPlayerAttackFake` : プレイヤー→偽物無効化
- `MakeFakeAttackMonster` : 偽物→モンスター有効化
- `MakeMonsterAttackFake` : モンスター→偽物有効化（囮モード）
- `VerboseLog` : 詳細ログ

## ビルド

### 前提

- .NET SDK 8.0+
- MIMESIS 本体（Steam版）
- MelonLoader 0.7.3 がインストール済みであること

### 手順

```bash
# GamePath を直接指定
dotnet build CloneVsMonsters/CloneVsMonsters.csproj -c Release -p:GamePath="C:/.../MIMESIS"

# または環境変数
export MIMESIS_PATH="C:/.../MIMESIS"
dotnet build CloneVsMonsters/CloneVsMonsters.csproj -c Release

# または Directory.Build.user.props
cat > CloneVsMonsters/Directory.Build.user.props << 'XML'
<Project><PropertyGroup><GamePath>C:/.../MIMESIS</GamePath></PropertyGroup></Project>
XML
dotnet build CloneVsMonsters/CloneVsMonsters.csproj -c Release
```

出力は `CloneVsMonsters/bin/Release/netstandard2.1/CloneVsMonsters.dll`

### CI / ゲームなし環境

```bash
dotnet build CloneVsMonsters/CloneVsMonsters.csproj -c Release -p:SkipGameRefs=true
```

スタブ参照でビルドが通るが、動作検証はできない。

## テスト

1. **単体テスト**: 各パッチの ShouldBlockDamage / EvaluateFaction ロジックはスタブでテスト可能
2. **統合テスト**: 実際の MIMESIS でホストとしてロビーを作成し、偽物が出現するステージで動作確認
3. **マルチプレイテスト**: ホストがMOD入り、ゲストがバニラで参加して効果を確認

## 既知の制限

- ゲームが大幅にアップデートされて内部構造が変わると、パッチがヒットしなくなる可能性がある。その場合は `VerboseLog=true` で `Latest.log` を確認し、新しいクラス名・メソッド名を候補リストに追加する。
- 偽物のAIが NavMesh 以外の特殊な移動ロジックを使っている場合、強制的なターゲット変更が効かないことがある。その場合は MimicAllyController の `TryForceMimicAttackMonster` で追加のメソッド呼び出しを試す。
- クライアント側でのみ動作するMOD（ホスト専用）なので、ホスト以外が導入しても効果はない（ホストのサーバー側判定が優先されるため）。

## 今後の拡張

- ゲーム内設定UI（PatchesチップのようなUI）
- 偽物の能力調整（HP/攻撃力/移動速度）
- 特定モンスターのみ敵対させるフィルタ
- 偽物を完全にプレイヤー操作キャラとして扱うモード

## 参考

- [MimicAPI](https://github.com/DooDesch-Mods/MimicAPI) - ゲームAPI解析の参考
- [Mimesis-FakePlayer](https://github.com/DooDesch-Mods/Mimesis-FakePlayer) - FakePlayer の実装参考（VirtualAcceptSession等）
- [mimesis-patches](https://github.com/medovanx/mimesis-patches) - ビルド構成参考
- [InfiniteStamina](https://github.com/ToxesFoxes/Mimesis-InfiniteStamina) - シンプルな Harmony パッチの参考
