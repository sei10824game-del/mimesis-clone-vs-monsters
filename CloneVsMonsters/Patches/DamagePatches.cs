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
    /// ダメージ無効化パッチ。
    /// 偽物→プレイヤー、偽物→偽物、プレイヤー→偽物 のダメージをブロックする。
    /// ゲームバージョンごとにメソッド名が異なる可能性があるため、複数の候補を動的にパッチする。
    /// </summary>
    internal static class DamagePatches
    {
        private static readonly List<MethodInfo> PatchedMethods = new();

        public static void Apply(Harmony harmony)
        {
            // 1) 既知の型（ProtoActor, VActor, StatManagerなど）に対する直接パッチを試す
            TryPatchKnownTypes(harmony);

            // 2) 動的スキャン：Assembly-CSharp 内の全てのダメージ系メソッドをパッチ
            ApplyDynamicPatches(harmony);

            MelonLogger.Msg($"[CloneVsMonsters] DamagePatches applied to {PatchedMethods.Count} method(s)");
        }

        private static void TryPatchKnownTypes(Harmony harmony)
        {
            // 候補となる型名（フルネーム）
            var typeNames = new[]
            {
                "Mimic.Actors.ProtoActor",
                "Mimic.ProtoActor",
                "ProtoActor",
                "VActor",
                "Mimic.VActor",
                "VPlayer",
                "VMonster",
                "Mimic.Actors.VActor",
                "StatManager",
                "Mimic.StatManager",
                "DamageHandler",
                "HitHandler",
                "CombatManager",
            };

            var methodNames = TypeResolver.DamageMethodCandidates;

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
            foreach (var methodName in TypeResolver.DamageMethodCandidates)
            {
                var types = TypeResolver.FindTypesWithMethod(methodName);
                foreach (var type in types)
                {
                    var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (method == null) continue;

                    // 既にパッチ済みならスキップ
                    if (PatchedMethods.Contains(method)) continue;

                    // パラメータに attacker/victim が含まれそうなメソッドのみ対象
                    var parameters = method.GetParameters();
                    bool looksLikeDamageMethod = false;
                    foreach (var p in parameters)
                    {
                        var pn = p.Name?.ToLower() ?? "";
                        var pt = p.ParameterType.Name.ToLower();
                        if (pn.Contains("attacker") || pn.Contains("victim") || pn.Contains("target") || pn.Contains("damage") || pn.Contains("hit") ||
                            pt.Contains("actor") || pt.Contains("player") || pt.Contains("monster") || pt.Contains("vactor") || pt.Contains("protoactor"))
                        {
                            looksLikeDamageMethod = true;
                            break;
                        }
                    }
                    // パラメータが1-3個で、ダメージ量を含む場合は対象
                    if (!looksLikeDamageMethod && parameters.Length >= 1 && parameters.Length <= 4)
                    {
                        // フォールバック: 名前がダメージ系なら一応パッチ（慎重に）
                        // ただし誤爆を避けるため、少なくとも1つのパラメータが actor/数値型であること
                        bool hasRelevantParam = false;
                        foreach (var p in parameters)
                        {
                            if (p.ParameterType == typeof(int) || p.ParameterType == typeof(float) || p.ParameterType == typeof(long) ||
                                p.ParameterType.Name.Contains("Actor") || p.ParameterType.Name.Contains("Player"))
                            {
                                hasRelevantParam = true;
                                break;
                            }
                        }
                        if (!hasRelevantParam) continue;
                    }

                    TryPatchMethod(harmony, method);
                }
            }
        }

        private static void TryPatchMethod(Harmony harmony, MethodInfo method)
        {
            try
            {
                if (PatchedMethods.Contains(method)) return;

                var prefix = typeof(DamagePatches).GetMethod(nameof(UniversalDamagePrefix), BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                PatchedMethods.Add(method);

                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Msg($"[DamagePatches] Patched: {method.DeclaringType?.FullName}.{method.Name}({string.Join(", ", Array.ConvertAll(method.GetParameters(), p => p.ParameterType.Name + " " + p.Name))})");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[DamagePatches] Failed to patch {method.DeclaringType?.FullName}.{method.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 汎用ダメージ Prefix。呼び出し元の attacker と target をリフレクションで特定し、
        /// 設定に応じてダメージをキャンセルする。
        /// </summary>
        private static bool UniversalDamagePrefix(MethodBase __originalMethod, object? __instance, object[]? __args)
        {
            if (!CloneVsMonstersConfig.Enabled) return true;

            try
            {
                // __instance が attacker の場合と、__args に attacker/victim が含まれる場合の両方を考慮
                object? attacker = null;
                object? victim = null;

                // ケース1: __instance が attacker
                if (__instance != null)
                {
                    attacker = __instance;
                }

                // ケース2: __args から attacker/victim を推定
                if (__args != null)
                {
                    foreach (var arg in __args)
                    {
                        if (arg == null) continue;
                        var typeName = arg.GetType().Name;
                        // Actor系のオブジェクトを attacker/victim として扱う
                        if (typeName.Contains("Actor") || typeName.Contains("Player") || typeName.Contains("Monster") || typeName.Contains("ProtoActor") || typeName.Contains("VActor") || typeName.Contains("VPlayer") || typeName.Contains("VMonster"))
                        {
                            if (attacker == null || attacker == __instance)
                            {
                                // 最初の Actor が attacker、2番目が victim という想定
                                if (victim == null && attacker != null && attacker != arg)
                                    victim = arg;
                                else if (attacker == null)
                                    attacker = arg;
                            }
                            else
                            {
                                victim = arg;
                            }
                        }
                    }

                    // 数値のみのダメージ呼び出しの場合、__instance が victim の可能性
                    if (victim == null && __instance != null && __args.Length == 1)
                    {
                        // 例: victim.TakeDamage(amount) — __instance が victim
                        victim = __instance;
                        attacker = null; // 攻撃者不明
                    }
                }

                // 攻撃者・被害者が特定できない場合はスキップ（誤爆防止）
                if (attacker == null && victim == null) return true;

                // 判定ロジック
                bool shouldBlock = ShouldBlockDamage(attacker, victim);

                if (shouldBlock)
                {
                    if (CloneVsMonstersConfig.VerboseLog)
                    {
                        var orig = __originalMethod.DeclaringType?.Name + "." + __originalMethod.Name;
                        MelonLogger.Msg($"[DamagePatches] BLOCKED damage: {orig} | Attacker={TypeResolver.DescribeActor(attacker)} -> Victim={TypeResolver.DescribeActor(victim)}");
                    }

                    // ダメージ処理をスキップ（Prefixで false を返すと元メソッドが実行されない）
                    // 戻り値が void の場合はそのままスキップ、bool の場合は適切に処理
                    // Harmony の Prefix で false を返すと元メソッドがスキップされる
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Warning($"[DamagePatches] Error in UniversalDamagePrefix: {ex.Message}");
            }

            return true; // 通常通り実行
        }

        /// <summary>
        /// ダメージをブロックすべきか判定する。
        /// </summary>
        public static bool ShouldBlockDamage(object? attacker, object? victim)
        {
            if (attacker == null || victim == null) return false;

            bool attackerIsMimic = TypeResolver.IsMimic(attacker);
            bool victimIsMimic = TypeResolver.IsMimic(victim);
            bool attackerIsPlayer = TypeResolver.IsPlayer(attacker);
            bool victimIsPlayer = TypeResolver.IsPlayer(victim);
            bool attackerIsMonster = TypeResolver.IsMonster(attacker);
            bool victimIsMonster = TypeResolver.IsMonster(victim);

            // 偽物 → プレイヤー
            if (attackerIsMimic && victimIsPlayer && CloneVsMonstersConfig.PreventFakeAttackPlayer)
                return true;

            // 偽物 → 偽物
            if (attackerIsMimic && victimIsMimic && CloneVsMonstersConfig.PreventFakeAttackFake)
                return true;

            // プレイヤー → 偽物
            if (attackerIsPlayer && victimIsMimic && CloneVsMonstersConfig.PreventPlayerAttackFake)
                return true;

            // モンスター → 偽物（設定によりブロック可能だが、デフォルトではモンスターは偽物を襲わない方が自然）
            // MakeMonsterAttackFake が false ならブロック、true なら通す
            if (attackerIsMonster && victimIsMimic && !CloneVsMonstersConfig.MakeMonsterAttackFake)
                return true;

            // 偽物 → モンスター は常に通す（敵対させる）
            // モンスター → 偽物（MakeMonsterAttackFake=true の時のみ通す。上でブロック判定済み）

            return false;
        }

        /// <summary>
        /// 個別の具体的なダメージ系メソッド用パッチ（型が見つかった場合に有効）。
        /// 汎用パッチのフォールバックとして、代表的なシグネチャに対応。
        /// </summary>

        // 例: StatManager.TakeDamage 系の具体的なパッチ（リフレクションで解決できた場合）
        // これらは上記の TryPatchKnownTypes で自動的に UniversalDamagePrefix が適用されるため、
        // 個別のメソッド定義は不要だが、将来的に特定シグネチャで特殊処理が必要ならここに追加する。
    }
}
