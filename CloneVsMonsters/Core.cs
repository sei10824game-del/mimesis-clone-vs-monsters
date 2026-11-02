using System;
using System.Reflection;
using CloneVsMonsters.Config;
using CloneVsMonsters.Patches;
using CloneVsMonsters.Services;
using CloneVsMonsters.Utils;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CloneVsMonsters.Core), "CloneVsMonsters", "1.0.0", "sei10824game-del", "https://github.com/sei10824game-del/mimesis-clone-vs-monsters")]
[assembly: MelonGame("ReLUGames", "MIMESIS")]
[assembly: MelonColor(255, 100, 200, 255)]
[assembly: MelonAuthorColor(255, 200, 100, 255)]

namespace CloneVsMonsters
{
    /// <summary>
    /// CloneVsMonsters MOD エントリーポイント。
    /// 偽物（ミメシス/クローン）がプレイヤーと他の偽物に敵対せず、モンスターに敵対するようにする。
    /// </summary>
    public sealed class Core : MelonMod
    {
        private Harmony? _harmony;
        private GameObject? _controllerObject;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("=================================================");
            MelonLogger.Msg(" CloneVsMonsters v1.0.0 - Initializing...");
            MelonLogger.Msg(" 偽物がプレイヤーと他の偽物に敵対せずモンスターに敵対するMOD");
            MelonLogger.Msg("=================================================");
            MelonLogger.Msg(" Author: sei10824game-del");
            MelonLogger.Msg(" Game: MIMESIS (ReLUGames)");
            MelonLogger.Msg(" MelonLoader: 0.7.3+");
            MelonLogger.Msg("=================================================");

            // 1) 設定の初期化
            CloneVsMonstersConfig.Initialize();
            MelonLogger.Msg($"[CloneVsMonsters] Config: Enabled={CloneVsMonstersConfig.Enabled}");

            // 2) Harmony パッチの適用
            try
            {
                _harmony = new Harmony("com.sei10824.clonevsmonsters");

                MelonLogger.Msg("[CloneVsMonsters] Applying Harmony patches...");

                // ゲームの型情報をスキャン（ログ出力）
                if (CloneVsMonstersConfig.VerboseLog)
                {
                    TypeResolver.LogAllFoundTypes();
                }

                DamagePatches.Apply(_harmony);
                FactionPatches.Apply(_harmony);
                TargetingPatches.Apply(_harmony);

                MelonLogger.Msg("=================================================");
                MelonLogger.Msg(" SUCCESS: All Harmony patches applied!");
                MelonLogger.Msg("=================================================");
                MelonLogger.Msg(" Active patches:");
                MelonLogger.Msg("  [1] Damage Block   - 偽物→プレイヤー/偽物 のダメージを無効化");
                MelonLogger.Msg("  [2] Faction        - 敵対判定を書き換え（偽物は味方、モンスターは敵）");
                MelonLogger.Msg("  [3] Targeting      - 偽物のAIターゲットをモンスターへ誘導");
                MelonLogger.Msg("  [4] AllyController - 毎フレーム補正（フォールバック）");
                MelonLogger.Msg("=================================================");
                MelonLogger.Msg(" 偽物はモンスターと戦います！共闘をお楽しみください。");
                MelonLogger.Msg("=================================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("=================================================");
                MelonLogger.Error(" FAILED to apply Harmony patches!");
                MelonLogger.Error($" Exception: {ex.Message}");
                MelonLogger.Error($" Stack: {ex.StackTrace}");
                MelonLogger.Error("=================================================");
                MelonLogger.Error(" MODは無効化されますが、ゲーム自体は通常通り動作します。");
                MelonLogger.Error(" Latest.log を添えて Issue を報告してください。");
                MelonLogger.Error(" https://github.com/sei10824game-del/mimesis-clone-vs-monsters/issues");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            // シーン遷移時に AllyController を再配置
            // メインメニューでは不要、ゲームプレイシーン（Dungeon, Maintenanceなど）でのみ有効
            if (!CloneVsMonstersConfig.Enabled) return;

            // Dungeon/Level/Gameplay 系のシーンか判定
            bool isGameplayScene = IsGameplayScene(sceneName);
            if (!isGameplayScene && sceneName != null)
            {
                // 汚染された雨のステージやダンジョンは通常 "Level" や "Dungeon" を含む
                // シーン名が不明でも、一定時間後にチェックするのでここでは厳密に判定しない
                // メニュー以外の全シーンで有効にする（軽量なので問題なし）
                if (sceneName.Contains("Menu") || sceneName.Contains("Title") || sceneName.Contains("Lobby"))
                    return;
            }

            EnsureController();
            MelonLogger.Msg($"[CloneVsMonsters] Scene loaded: {sceneName} (buildIndex={buildIndex}) - AllyController ready");
        }

        public override void OnDeinitializeMelon()
        {
            try
            {
                _harmony?.UnpatchSelf();
                MelonLogger.Msg("[CloneVsMonsters] Harmony patches removed.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CloneVsMonsters] Unpatch failed: {ex.Message}");
            }

            if (_controllerObject != null)
            {
                UnityEngine.Object.Destroy(_controllerObject);
                _controllerObject = null;
            }
        }

        private void EnsureController()
        {
            if (_controllerObject != null) return;

            try
            {
                _controllerObject = new GameObject("CloneVsMonsters_AllyController");
                UnityEngine.Object.DontDestroyOnLoad(_controllerObject);
                _controllerObject.hideFlags = HideFlags.HideAndDontSave;
                _controllerObject.AddComponent<MimicAllyController>();
                MelonLogger.Msg("[CloneVsMonsters] AllyController spawned (MimicAllyController)");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CloneVsMonsters] Failed to spawn AllyController: {ex.Message}");
            }
        }

        private static bool IsGameplayScene(string? sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            var lower = sceneName.ToLower();
            return lower.Contains("dungeon") ||
                   lower.Contains("level") ||
                   lower.Contains("game") ||
                   lower.Contains("stage") ||
                   lower.Contains("room") ||
                   lower.Contains("maintenance") ||
                   lower.Contains("tram");
        }

        public override void OnUpdate()
        {
            // デバッグ用ホットキー（VerboseLogが有効な時のみ）
            if (!CloneVsMonstersConfig.VerboseLog) return;

            // F3: 現在の偽物/モンスター数をログ出力
            if (Input.GetKeyDown(KeyCode.F3))
            {
                MelonLogger.Msg("[CloneVsMonsters] === Debug Info (F3) ===");
                MelonLogger.Msg($"  Enabled: {CloneVsMonstersConfig.Enabled}");
                MelonLogger.Msg($"  PreventFake->Player: {CloneVsMonstersConfig.PreventFakeAttackPlayer}");
                MelonLogger.Msg($"  PreventFake->Fake: {CloneVsMonstersConfig.PreventFakeAttackFake}");
                MelonLogger.Msg($"  PreventPlayer->Fake: {CloneVsMonstersConfig.PreventPlayerAttackFake}");
                MelonLogger.Msg($"  MakeFake->Monster: {CloneVsMonstersConfig.MakeFakeAttackMonster}");
                MelonLogger.Msg($"  MakeMonster->Fake: {CloneVsMonstersConfig.MakeMonsterAttackFake}");
                MelonLogger.Msg($"  Harmony ID: com.sei10824.clonevsmonsters");
                MelonLogger.Msg($"  Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

                if (_controllerObject != null)
                {
                    var ctrl = _controllerObject.GetComponent<MimicAllyController>();
                    if (ctrl != null)
                    {
                        var (mimics, monsters) = ctrl.GetLastScanResult();
                        MelonLogger.Msg($"  Last scan: Mimics={mimics}, Monsters={monsters}");
                    }
                }
            }
        }
    }
}
