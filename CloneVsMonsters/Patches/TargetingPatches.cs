using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CloneVsMonsters.Config;
using CloneVsMonsters.Utils;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace CloneVsMonsters.Patches
{
    /// <summary>
    /// AIターゲット誘導パッチ。
    /// 偽物のAIがプレイヤーをターゲットに選んだ場合、最も近いモンスターへターゲットを差し替える。
    /// </summary>
    internal static class TargetingPatches
    {
        private static readonly List<MethodInfo> PatchedMethods = new();

        public static void Apply(Harmony harmony)
        {
            TryPatchKnownTypes(harmony);
            ApplyDynamicPatches(harmony);
            MelonLogger.Msg($"[CloneVsMonsters] TargetingPatches applied to {PatchedMethods.Count} method(s)");
        }

        private static void TryPatchKnownTypes(Harmony harmony)
        {
            var typeNames = new[]
            {
                "Mimic.Actors.VMimic",
                "VMimic",
                "MimicAI",
                "MimicController",
                "FakeAI",
                "CloneAI",
                "MonsterAI",
                "EnemyAI",
                "AIController",
                "VActor",
                "Mimic.VActor",
                "Mimic.Actors.VActor",
                "VMonster",
            };

            var methodNames = TypeResolver.TargetMethodCandidates;

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
            foreach (var methodName in TypeResolver.TargetMethodCandidates)
            {
                var types = TypeResolver.FindTypesWithMethod(methodName);
                foreach (var type in types)
                {
                    // Mimic/AI/Monster/Actor 関連の型のみ対象
                    var fullName = type.FullName ?? type.Name;
                    if (!fullName.Contains("Mimic", StringComparison.OrdinalIgnoreCase) &&
                        !fullName.Contains("Monster", StringComparison.OrdinalIgnoreCase) &&
                        !fullName.Contains("Enemy", StringComparison.OrdinalIgnoreCase) &&
                        !fullName.Contains("Actor", StringComparison.OrdinalIgnoreCase) &&
                        !fullName.Contains("AI", StringComparison.OrdinalIgnoreCase) &&
                        !fullName.Contains("VActor") &&
                        !fullName.Contains("VMonster") &&
                        !fullName.Contains("ProtoActor"))
                        continue;

                    var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (method == null || PatchedMethods.Contains(method)) continue;

                    TryPatchMethod(harmony, method);
                }
            }
        }

        private static void TryPatchMethod(Harmony harmony, MethodInfo method)
        {
            try
            {
                if (PatchedMethods.Contains(method)) return;

                var postfix = typeof(TargetingPatches).GetMethod(nameof(UniversalTargetPostfix), BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                PatchedMethods.Add(method);

                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Msg($"[TargetingPatches] Patched (Postfix): {method.DeclaringType?.FullName}.{method.Name}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TargetingPatches] Failed to patch {method.DeclaringType?.FullName}.{method.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// ターゲット選択メソッドの Postfix。
        /// 偽物がプレイヤーをターゲットに選んだ場合、モンスターへ差し替える。
        /// </summary>
        private static void UniversalTargetPostfix(object? __instance, ref object? __result)
        {
            if (!CloneVsMonstersConfig.Enabled) return;
            if (!CloneVsMonstersConfig.MakeFakeAttackMonster) return;
            if (__instance == null || __result == null) return;

            try
            {
                // __instance が偽物でなければ何もしない
                if (!TypeResolver.IsMimic(__instance)) return;

                // __result がプレイヤーなら、モンスターへ差し替え
                if (TypeResolver.IsPlayer(__result))
                {
                    var nearestMonster = FindNearestMonster(__instance);
                    if (nearestMonster != null)
                    {
                        if (CloneVsMonstersConfig.VerboseLog)
                            MelonLogger.Msg($"[TargetingPatches] Redirected Mimic target: {TypeResolver.DescribeActor(__result)} -> {TypeResolver.DescribeActor(nearestMonster)} (Mimic: {TypeResolver.DescribeActor(__instance)})");

                        __result = nearestMonster;
                    }
                }
            }
            catch (Exception ex)
            {
                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Warning($"[TargetingPatches] Error in UniversalTargetPostfix: {ex.Message}");
            }
        }

        /// <summary>
        /// 最も近いモンスターを探す。
        /// MimicAPI があればそれを使い、なければリフレクションで VRoom 内の VMonster を列挙する。
        /// </summary>
        public static object? FindNearestMonster(object mimicActor)
        {
            try
            {
                // 位置取得を試みる
                Vector3 mimicPos = GetActorPosition(mimicActor);
                if (mimicPos == Vector3.zero && !(mimicActor is UnityEngine.Component))
                {
                    // VActor の場合は ProtoActor 経由で位置を取る必要がある
                    mimicPos = GetActorPositionViaRoom(mimicActor);
                }

                // 部屋内のモンスターを列挙
                var monsters = GetMonstersInCurrentRoom();
                if (monsters.Count == 0) return null;

                object? nearest = null;
                float nearestDist = float.MaxValue;

                foreach (var monster in monsters)
                {
                    if (monster == null) continue;
                    // 生存チェック
                    if (!IsAlive(monster)) continue;

                    Vector3 monsterPos = GetActorPosition(monster);
                    if (monsterPos == Vector3.zero) continue;

                    float dist = Vector3.Distance(mimicPos, monsterPos);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = monster;
                    }
                }

                return nearest;
            }
            catch (Exception ex)
            {
                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Warning($"[TargetingPatches] FindNearestMonster error: {ex.Message}");
                return null;
            }
        }

        private static Vector3 GetActorPosition(object? actor)
        {
            if (actor == null) return Vector3.zero;

            // Unity Component なら transform.position
            if (actor is UnityEngine.Component comp)
                return comp.transform.position;

            if (actor is GameObject go)
                return go.transform.position;

            // リフレクションで transform / position を探す
            try
            {
                var transform = ReflectionHelper.GetPropertyValue(actor, "transform") ?? ReflectionHelper.GetFieldValue(actor, "transform");
                if (transform is Transform t) return t.position;

                var pos = ReflectionHelper.GetPropertyValue(actor, "position") ?? ReflectionHelper.GetFieldValue(actor, "position");
                if (pos is Vector3 v) return v;
            }
            catch { }

            return Vector3.zero;
        }

        private static Vector3 GetActorPositionViaRoom(object vActor)
        {
            try
            {
                // VActor -> ProtoActor -> transform.position のパスを試す
                var protoActor = ReflectionHelper.GetFieldValue(vActor, "_protoActor") ?? ReflectionHelper.GetFieldValue(vActor, "protoActor") ?? ReflectionHelper.GetFieldValue(vActor, "ProtoActor");
                if (protoActor != null) return GetActorPosition(protoActor);

                // VRoomManager 経由で部屋を取得し、そこから位置を再解決
                var room = ReflectionHelper.GetFieldValue(vActor, "VRoom") ?? ReflectionHelper.GetPropertyValue(vActor, "VRoom");
                if (room != null)
                {
                    var actorId = ReflectionHelper.GetFieldValue(vActor, "ObjectID") ?? ReflectionHelper.GetFieldValue(vActor, "ActorID");
                    // 部屋内の全アクターから対応する ProtoActor を探すのはコストが高いのでスキップ
                }
            }
            catch { }
            return Vector3.zero;
        }

        private static List<object> GetMonstersInCurrentRoom()
        {
            var result = new List<object>();
            try
            {
                // MimicAPI があればそれを使う（リフレクションで呼び出し）
                var mimicApiType = ReflectionHelper.FindType("MimicAPI.GameAPI.ActorAPI");
                if (mimicApiType != null)
                {
                    var room = GetCurrentRoom();
                    if (room != null)
                    {
                        var method = mimicApiType.GetMethod("GetMonstersInRoom", BindingFlags.Public | BindingFlags.Static);
                        var monsters = method?.Invoke(null, new[] { room }) as List<object>;
                        if (monsters != null) return monsters;
                    }
                }

                // フォールバック: VRoomManager から直接取得
                var roomManager = GetVRoomManager();
                if (roomManager == null) return result;

                var rooms = GetAllRooms(roomManager);
                foreach (var room in rooms)
                {
                    if (room == null) continue;
                    var isPlayable = ReflectionHelper.InvokeMethod(room, "IsPlayable") as bool?;
                    if (isPlayable == false) continue;

                    var dict = ReflectionHelper.GetFieldValue(room, "_vActorDict") as IDictionary;
                    if (dict == null) continue;

                    foreach (var val in dict.Values)
                    {
                        if (val != null && TypeResolver.IsMonster(val))
                            result.Add(val);
                    }
                }
            }
            catch (Exception ex)
            {
                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Warning($"[TargetingPatches] GetMonstersInCurrentRoom error: {ex.Message}");
            }
            return result;
        }

        private static object? GetCurrentRoom()
        {
            try
            {
                var mimicApiRoomType = ReflectionHelper.FindType("MimicAPI.GameAPI.RoomAPI");
                if (mimicApiRoomType != null)
                {
                    var method = mimicApiRoomType.GetMethod("GetCurrentRoom", BindingFlags.Public | BindingFlags.Static);
                    return method?.Invoke(null, null);
                }

                // フォールバック
                var vRoomManager = GetVRoomManager();
                if (vRoomManager == null) return null;
                var rooms = GetAllRooms(vRoomManager);
                foreach (var room in rooms)
                {
                    if (room != null && ReflectionHelper.InvokeMethod(room, "IsPlayable") is bool b && b)
                        return room;
                }
            }
            catch { }
            return null;
        }

        private static object? GetVRoomManager()
        {
            try
            {
                var hubType = ReflectionHelper.FindType("Mimic.Hub") ?? ReflectionHelper.FindTypeByName("Hub");
                if (hubType == null) return null;
                var hub = hubType.GetField("s", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (hub == null) return null;
                var vworld = ReflectionHelper.GetFieldValue(hub, "<vworld>k__BackingField");
                if (vworld == null) return null;
                return ReflectionHelper.GetFieldValue(vworld, "_vRoomManager");
            }
            catch { return null; }
        }

        private static List<object> GetAllRooms(object vRoomManager)
        {
            var result = new List<object>();
            try
            {
                var dict = ReflectionHelper.GetFieldValue(vRoomManager, "_vrooms") as IDictionary;
                if (dict == null) return result;
                foreach (var val in dict.Values)
                    if (val != null) result.Add(val);
            }
            catch { }
            return result;
        }

        private static bool IsAlive(object actor)
        {
            try
            {
                var result = ReflectionHelper.InvokeMethod(actor, "IsAliveStatus");
                if (result is bool b) return b;
                var dead = ReflectionHelper.GetFieldValue(actor, "dead");
                if (dead is bool d) return !d;
            }
            catch { }
            return true; // 不明なら生存扱い
        }
    }
}
