using System;
using System.Collections.Generic;
using System.Reflection;
using CloneVsMonsters.Config;
using CloneVsMonsters.Utils;
using HarmonyLib;
using MelonLoader;

namespace CloneVsMonsters.Patches
{
    /// <summary>
    /// 敵対判定パッチ。
    /// IsEnemy / CanAttack / IsHostile 系の判定を書き換え、
    /// 偽物を味方、モンスターを敵として扱うようにする。
    /// </summary>
    internal static class FactionPatches
    {
        private static readonly List<MethodInfo> PatchedMethods = new();

        public static void Apply(Harmony harmony)
        {
            TryPatchKnownTypes(harmony);
            ApplyDynamicPatches(harmony);
            MelonLogger.Msg($"[CloneVsMonsters] FactionPatches applied to {PatchedMethods.Count} method(s)");
        }

        private static void TryPatchKnownTypes(Harmony harmony)
        {
            var typeNames = new[]
            {
                "Mimic.Actors.VMimic",
                "VMimic",
                "VActor",
                "Mimic.VActor",
                "Mimic.Actors.VActor",
                "VMonster",
                "Mimic.Actors.VMonster",
                "ProtoActor",
                "Mimic.Actors.ProtoActor",
                "FactionManager",
                "HostilityManager",
                "RelationManager",
                "TeamManager",
            };

            var methodNames = TypeResolver.HostilityMethodCandidates;

            foreach (var typeName in typeNames)
            {
                var type = ReflectionHelper.FindType(typeName) ?? ReflectionHelper.FindTypeByName(typeName);
                if (type == null) continue;

                foreach (var methodName in methodNames)
                {
                    var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (method == null) continue;
                    TryPatchMethod(harmony, method);
                }
            }
        }

        private static void ApplyDynamicPatches(Harmony harmony)
        {
            foreach (var methodName in TypeResolver.HostilityMethodCandidates)
            {
                var types = TypeResolver.FindTypesWithMethod(methodName);
                foreach (var type in types)
                {
                    var fullName = type.FullName ?? type.Name;
                    // ゲーム関連の型のみ
                    if (!fullName.Contains("Mimic", StringComparison.OrdinalIgnoreCase) &&
                        !fullName.Contains("Monster", StringComparison.OrdinalIgnoreCase) &&
                        !fullName.Contains("Player", StringComparison.OrdinalIgnoreCase) &&
                        !fullName.Contains("Actor", StringComparison.OrdinalIgnoreCase) &&
                        !fullName.Contains("VActor") &&
                        !fullName.Contains("Faction") &&
                        !fullName.Contains("Hostility") &&
                        !fullName.Contains("Team") &&
                        !fullName.Contains("ProtoActor"))
                        continue;

                    var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (method == null || PatchedMethods.Contains(method)) continue;

                    // 戻り値が bool かつ、引数に Actor が含まれるメソッドのみ
                    if (method.ReturnType != typeof(bool)) continue;

                    var parameters = method.GetParameters();
                    bool hasActorParam = false;
                    foreach (var p in parameters)
                    {
                        var pt = p.ParameterType.Name;
                        if (pt.Contains("Actor") || pt.Contains("Player") || pt.Contains("Monster") || pt.Contains("VActor") || pt.Contains("ProtoActor"))
                        {
                            hasActorParam = true;
                            break;
                        }
                    }
                    if (!hasActorParam && parameters.Length == 0) hasActorParam = true; // 例: IsEnemy() のように instance が attacker

                    if (!hasActorParam) continue;

                    TryPatchMethod(harmony, method);
                }
            }
        }

        private static void TryPatchMethod(Harmony harmony, MethodInfo method)
        {
            try
            {
                if (PatchedMethods.Contains(method)) return;

                var prefix = typeof(FactionPatches).GetMethod(nameof(UniversalHostilityPrefix), BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                PatchedMethods.Add(method);

                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Msg($"[FactionPatches] Patched: {method.DeclaringType?.FullName}.{method.Name}({string.Join(", ", Array.ConvertAll(method.GetParameters(), p => p.ParameterType.Name + " " + p.Name))}) -> {method.ReturnType.Name}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FactionPatches] Failed to patch {method.DeclaringType?.FullName}.{method.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 汎用敵対判定 Prefix。
        /// 偽物 vs プレイヤー/偽物 は味方、偽物 vs モンスター は敵として扱う。
        /// </summary>
        private static bool UniversalHostilityPrefix(MethodBase __originalMethod, object? __instance, object[]? __args, ref bool __result)
        {
            if (!CloneVsMonstersConfig.Enabled) return true;

            try
            {
                // __instance と __args から attacker/target を特定
                object? attacker = __instance;
                object? target = null;

                if (__args != null && __args.Length > 0)
                {
                    // 最初の Actor 引数が target
                    foreach (var arg in __args)
                    {
                        if (arg == null) continue;
                        var typeName = arg.GetType().Name;
                        if (typeName.Contains("Actor") || typeName.Contains("Player") || typeName.Contains("Monster") || typeName.Contains("ProtoActor") || typeName.Contains("VActor"))
                        {
                            target = arg;
                            break;
                        }
                    }

                    // 引数が2つある場合、1つ目が attacker, 2つ目が target の可能性もある（staticメソッド）
                    if (__instance == null && __args.Length >= 2)
                    {
                        attacker = __args[0];
                        target = __args[1];
                    }
                }

                if (attacker == null && target == null) return true;

                // 判定
                var factionResult = EvaluateFaction(attacker, target);
                if (factionResult.HasValue)
                {
                    var methodName = __originalMethod.Name.ToLower();
                    bool isEnemyQuery = methodName.Contains("enemy") || methodName.Contains("hostile") || methodName.Contains("attack") || methodName.Contains("target") || methodName.Contains("valid");

                    // IsEnemy / IsHostile 系は敵なら true を返す
                    // CanAttack 系も同様に攻撃可能なら true
                    __result = factionResult.Value;

                    if (CloneVsMonstersConfig.VerboseLog)
                    {
                        MelonLogger.Msg($"[FactionPatches] OVERRIDE {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}: attacker={TypeResolver.DescribeActor(attacker)} target={TypeResolver.DescribeActor(target)} -> {__result}");
                    }

                    return false; // 元メソッドをスキップ
                }
            }
            catch (Exception ex)
            {
                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Warning($"[FactionPatches] Error in UniversalHostilityPrefix: {ex.Message}");
            }

            return true;
        }

        /// <summary>
        /// 敵対判定を評価する。
        /// null を返した場合はこのパッチで判定しない（元メソッドに任せる）
        /// true/false を返した場合は敵対/味方を上書きする
        /// </summary>
        public static bool? EvaluateFaction(object? attacker, object? target)
        {
            if (attacker == null || target == null) return null;

            bool attackerIsMimic = TypeResolver.IsMimic(attacker);
            bool targetIsMimic = TypeResolver.IsMimic(target);
            bool attackerIsPlayer = TypeResolver.IsPlayer(attacker);
            bool targetIsPlayer = TypeResolver.IsPlayer(target);
            bool attackerIsMonster = TypeResolver.IsMonster(attacker);
            bool targetIsMonster = TypeResolver.IsMonster(target);

            // 偽物が攻撃者の場合
            if (attackerIsMimic)
            {
                // 偽物 → プレイヤー : 味方（敵ではない）
                if (targetIsPlayer) return false;
                // 偽物 → 偽物 : 味方
                if (targetIsMimic) return false;
                // 偽物 → モンスター : 敵
                if (targetIsMonster) return true;
            }

            // プレイヤーが攻撃者の場合
            if (attackerIsPlayer)
            {
                // プレイヤー → 偽物 : 味方（誤爆防止設定が有効なら）
                if (targetIsMimic && CloneVsMonstersConfig.PreventPlayerAttackFake)
                    return false;
            }

            // モンスターが攻撃者の場合
            if (attackerIsMonster)
            {
                // モンスター → 偽物 : 設定により敵/味方を切り替え
                if (targetIsMimic)
                    return CloneVsMonstersConfig.MakeMonsterAttackFake;
            }

            // 偽物がターゲットの場合
            if (targetIsMimic)
            {
                // モンスター → 偽物 は上で処理済み
                // プレイヤー → 偽物 も上で処理済み
            }

            return null; // このパッチでは判定しない
        }
    }
}
