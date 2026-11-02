using System;
using System.Collections;
using System.Collections.Generic;
using CloneVsMonsters.Config;
using CloneVsMonsters.Utils;
using MelonLoader;
using UnityEngine;

namespace CloneVsMonsters.Services
{
    /// <summary>
    /// 毎フレーム動作する補正コントローラ。
    /// Harmony パッチがヒットしないゲームバージョンでも、偽物のターゲットをモンスターへ誘導するフォールバック。
    /// また、偽物の位置を監視してモンスターに近づくように補助する（必要に応じて）。
    /// </summary>
    internal class MimicAllyController : MonoBehaviour
    {
        private float _nextScanTime;
        private const float ScanInterval = 1.0f; // 1秒ごとにスキャン（負荷軽減）

        // デバッグ用：前回のスキャン結果
        private int _lastMimicCount;
        private int _lastMonsterCount;

        void Update()
        {
            if (!CloneVsMonstersConfig.Enabled) return;
            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + ScanInterval;

            try
            {
                ScanAndRedirect();
            }
            catch (Exception ex)
            {
                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Warning($"[MimicAllyController] Update error: {ex.Message}");
            }
        }

        private void ScanAndRedirect()
        {
            var mimics = FindAllMimics();
            var monsters = FindAllMonsters();

            _lastMimicCount = mimics.Count;
            _lastMonsterCount = monsters.Count;

            if (mimics.Count == 0 || monsters.Count == 0) return;

            if (CloneVsMonstersConfig.VerboseLog)
                MelonLogger.Msg($"[MimicAllyController] Scan: Mimics={mimics.Count}, Monsters={monsters.Count}");

            foreach (var mimic in mimics)
            {
                if (mimic == null) continue;

                // 偽物がプレイヤーをターゲットにしている場合、モンスターへ差し替え
                TryRedirectMimicTarget(mimic, monsters);

                // 偽物がモンスターを攻撃していない場合、強制的に攻撃させる（AI介入）
                if (CloneVsMonstersConfig.MakeFakeAttackMonster)
                {
                    TryForceMimicAttackMonster(mimic, monsters);
                }
            }
        }

        private void TryRedirectMimicTarget(object mimic, List<object> monsters)
        {
            try
            {
                // 現在のターゲットを取得（フィールド名はバージョンで異なる可能性があるため複数候補を試す）
                var targetFieldNames = new[] { "_target", "target", "_currentTarget", "currentTarget", "_victim", "victim", "Target", "CurrentTarget" };
                object? currentTarget = null;
                string? foundField = null;

                foreach (var fieldName in targetFieldNames)
                {
                    currentTarget = ReflectionHelper.GetFieldValue(mimic, fieldName) ?? ReflectionHelper.GetPropertyValue(mimic, fieldName);
                    if (currentTarget != null)
                    {
                        foundField = fieldName;
                        break;
                    }
                }

                if (currentTarget == null) return;

                // ターゲットがプレイヤーなら、最も近いモンスターへ差し替え
                if (TypeResolver.IsPlayer(currentTarget))
                {
                    var nearestMonster = FindNearestMonster(mimic, monsters);
                    if (nearestMonster != null)
                    {
                        // フィールドに新しいターゲットを設定
                        if (foundField != null)
                        {
                            ReflectionHelper.SetFieldValue(mimic, foundField, nearestMonster);
                            // プロパティの場合も試す
                            try
                            {
                                var prop = mimic.GetType().GetProperty(foundField, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                prop?.SetValue(mimic, nearestMonster);
                            }
                            catch { }

                            if (CloneVsMonstersConfig.VerboseLog)
                                MelonLogger.Msg($"[MimicAllyController] Redirected mimic target {TypeResolver.DescribeActor(currentTarget)} -> {TypeResolver.DescribeActor(nearestMonster)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Warning($"[MimicAllyController] TryRedirectMimicTarget error: {ex.Message}");
            }
        }

        private void TryForceMimicAttackMonster(object mimic, List<object> monsters)
        {
            try
            {
                // 偽物に攻撃メソッドがある場合、モンスターへ攻撃を指示
                // 例: Attack(target), SetTarget(target), Chase(target) など
                var attackMethodNames = new[] { "Attack", "SetTarget", "Chase", "MoveToTarget", "StartAttack", "DoAttack", "Aggro" };

                // 現在ターゲットがモンスターでなければ、攻撃を促す
                var targetFieldNames = new[] { "_target", "target", "_currentTarget", "currentTarget" };
                object? currentTarget = null;
                foreach (var fn in targetFieldNames)
                {
                    currentTarget = ReflectionHelper.GetFieldValue(mimic, fn) ?? ReflectionHelper.GetPropertyValue(mimic, fn);
                    if (currentTarget != null) break;
                }

                // ターゲットが既にモンスターなら何もしない
                if (currentTarget != null && TypeResolver.IsMonster(currentTarget)) return;

                var nearestMonster = FindNearestMonster(mimic, monsters);
                if (nearestMonster == null) return;

                // 距離が離れすぎている場合は攻撃を強制しない（自然なAI挙動を尊重）
                float dist = GetDistance(mimic, nearestMonster);
                if (dist > 50f) return; // 50m以上離れている場合は無視

                // ターゲットが null またはプレイヤーの場合のみ強制
                if (currentTarget == null || TypeResolver.IsPlayer(currentTarget))
                {
                    // ターゲットを設定（フィールド経由）
                    foreach (var fieldName in targetFieldNames)
                    {
                        var field = mimic.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            field.SetValue(mimic, nearestMonster);
                            if (CloneVsMonstersConfig.VerboseLog)
                                MelonLogger.Msg($"[MimicAllyController] Force-set mimic target to monster (dist={dist:F1}m): {TypeResolver.DescribeActor(nearestMonster)}");
                            break;
                        }
                    }

                    // 攻撃メソッドがあれば呼び出す
                    foreach (var methodName in attackMethodNames)
                    {
                        var method = mimic.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (method != null)
                        {
                            try
                            {
                                var parameters = method.GetParameters();
                                if (parameters.Length == 0)
                                    method.Invoke(mimic, null);
                                else if (parameters.Length == 1)
                                    method.Invoke(mimic, new[] { nearestMonster });
                                if (CloneVsMonstersConfig.VerboseLog)
                                    MelonLogger.Msg($"[MimicAllyController] Invoked {methodName} on mimic -> monster");
                                break;
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Warning($"[MimicAllyController] TryForceMimicAttackMonster error: {ex.Message}");
            }
        }

        private object? FindNearestMonster(object mimic, List<object> monsters)
        {
            Vector3 mimicPos = GetPosition(mimic);
            if (mimicPos == Vector3.zero) return null;

            object? nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var monster in monsters)
            {
                if (monster == null || !IsAlive(monster)) continue;
                Vector3 monsterPos = GetPosition(monster);
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

        private float GetDistance(object a, object b)
        {
            Vector3 posA = GetPosition(a);
            Vector3 posB = GetPosition(b);
            if (posA == Vector3.zero || posB == Vector3.zero) return float.MaxValue;
            return Vector3.Distance(posA, posB);
        }

        private Vector3 GetPosition(object? actor)
        {
            if (actor == null) return Vector3.zero;
            if (actor is Component comp) return comp.transform.position;
            if (actor is GameObject go) return go.transform.position;

            try
            {
                var transform = ReflectionHelper.GetPropertyValue(actor, "transform") ?? ReflectionHelper.GetFieldValue(actor, "transform");
                if (transform is Transform t) return t.position;
                var pos = ReflectionHelper.GetPropertyValue(actor, "position") ?? ReflectionHelper.GetFieldValue(actor, "position");
                if (pos is Vector3 v) return v;

                // VActor の場合は ProtoActor 経由
                var protoActor = ReflectionHelper.GetFieldValue(actor, "_protoActor") ?? ReflectionHelper.GetFieldValue(actor, "protoActor");
                if (protoActor != null) return GetPosition(protoActor);
            }
            catch { }
            return Vector3.zero;
        }

        private bool IsAlive(object actor)
        {
            try
            {
                var result = ReflectionHelper.InvokeMethod(actor, "IsAliveStatus");
                if (result is bool b) return b;
                var dead = ReflectionHelper.GetFieldValue(actor, "dead");
                if (dead is bool d) return !d;
                var hp = ReflectionHelper.GetFieldValue(actor, "hp") ?? ReflectionHelper.GetFieldValue(actor, "_hp") ?? ReflectionHelper.GetFieldValue(actor, "health");
                if (hp is int hpi) return hpi > 0;
                if (hp is float hpf) return hpf > 0;
                if (hp is long hpl) return hpl > 0;
            }
            catch { }
            return true;
        }

        private List<object> FindAllMimics()
        {
            var result = new List<object>();
            try
            {
                // MimicAPI があれば使う
                var playerApi = ReflectionHelper.FindType("MimicAPI.GameAPI.PlayerAPI");
                if (playerApi != null)
                {
                    // 全プレイヤーを取得して Mimic をフィルタ
                    var method = playerApi.GetMethod("GetAllPlayers", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var players = method?.Invoke(null, null) as Array;
                    if (players != null)
                    {
                        foreach (var p in players)
                            if (p != null && TypeResolver.IsMimic(p))
                                result.Add(p);
                        if (result.Count > 0) return result;
                    }
                }

                // フォールバック: 全ての ProtoActor をスキャン
                var hubType = ReflectionHelper.FindType("Mimic.Hub") ?? ReflectionHelper.FindTypeByName("Hub");
                if (hubType != null)
                {
                    var hub = hubType.GetField("s", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                    if (hub != null)
                    {
                        var pdata = ReflectionHelper.GetFieldValue(hub, "pdata");
                        if (pdata != null)
                        {
                            var main = ReflectionHelper.GetFieldValue(pdata, "main");
                            if (main != null)
                            {
                                var allPlayers = ReflectionHelper.InvokeMethod(main, "GetAllPlayers") as Array;
                                if (allPlayers != null)
                                {
                                    foreach (var p in allPlayers)
                                        if (p != null && TypeResolver.IsMimic(p))
                                            result.Add(p);
                                    if (result.Count > 0) return result;
                                }
                            }
                        }
                    }
                }

                // さらにフォールバック: VRoomManager 経由で VActor を列挙
                var vRoomManager = GetVRoomManager();
                if (vRoomManager != null)
                {
                    var dict = ReflectionHelper.GetFieldValue(vRoomManager, "_vrooms") as IDictionary;
                    if (dict != null)
                    {
                        foreach (var room in dict.Values)
                        {
                            if (room == null) continue;
                            var actorDict = ReflectionHelper.GetFieldValue(room, "_vActorDict") as IDictionary;
                            if (actorDict == null) continue;
                            foreach (var actor in actorDict.Values)
                                if (actor != null && TypeResolver.IsMimic(actor))
                                    result.Add(actor);
                        }
                    }
                }

                // 最後の手段: Unityの全ProtoActorを FindObjectsOfType で探す
                // これはコストが高いので、Mimic関連のコンポーネント名でフィルタ
                TryFindViaUnity(ref result);
            }
            catch (Exception ex)
            {
                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Warning($"[MimicAllyController] FindAllMimics error: {ex.Message}");
            }
            return result;
        }

        private List<object> FindAllMonsters()
        {
            var result = new List<object>();
            try
            {
                var actorApi = ReflectionHelper.FindType("MimicAPI.GameAPI.ActorAPI");
                if (actorApi != null)
                {
                    var room = GetCurrentRoom();
                    if (room != null)
                    {
                        var method = actorApi.GetMethod("GetMonstersInRoom", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var monsters = method?.Invoke(null, new[] { room }) as List<object>;
                        if (monsters != null && monsters.Count > 0) return monsters;
                    }
                }

                var vRoomManager = GetVRoomManager();
                if (vRoomManager != null)
                {
                    var dict = ReflectionHelper.GetFieldValue(vRoomManager, "_vrooms") as IDictionary;
                    if (dict != null)
                    {
                        foreach (var room in dict.Values)
                        {
                            if (room == null) continue;
                            var actorDict = ReflectionHelper.GetFieldValue(room, "_vActorDict") as IDictionary;
                            if (actorDict == null) continue;
                            foreach (var actor in actorDict.Values)
                                if (actor != null && TypeResolver.IsMonster(actor))
                                    result.Add(actor);
                        }
                    }
                }

                TryFindViaUnityMonsters(ref result);
            }
            catch (Exception ex)
            {
                if (CloneVsMonstersConfig.VerboseLog)
                    MelonLogger.Warning($"[MimicAllyController] FindAllMonsters error: {ex.Message}");
            }
            return result;
        }

        private void TryFindViaUnity(ref List<object> mimics)
        {
            try
            {
                var protoActorType = ReflectionHelper.FindType("Mimic.Actors.ProtoActor") ?? ReflectionHelper.FindType("ProtoActor") ?? ReflectionHelper.FindTypeByName("ProtoActor");
                if (protoActorType == null) return;
                var actors = UnityEngine.Object.FindObjectsOfType(protoActorType);
                foreach (var actor in actors)
                    if (actor != null && TypeResolver.IsMimic(actor) && !mimics.Contains(actor))
                        mimics.Add(actor);
            }
            catch { }
        }

        private void TryFindViaUnityMonsters(ref List<object> monsters)
        {
            try
            {
                var vMonsterType = ReflectionHelper.FindType("Mimic.Actors.VMonster") ?? ReflectionHelper.FindType("VMonster") ?? ReflectionHelper.FindTypeByName("VMonster");
                if (vMonsterType == null) return;
                var actors = UnityEngine.Object.FindObjectsOfType(vMonsterType);
                foreach (var actor in actors)
                    if (actor != null && !monsters.Contains(actor))
                        monsters.Add(actor);
            }
            catch { }
        }

        private object? GetVRoomManager()
        {
            try
            {
                var hubType = ReflectionHelper.FindType("Mimic.Hub") ?? ReflectionHelper.FindTypeByName("Hub");
                if (hubType == null) return null;
                var hub = hubType.GetField("s", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (hub == null) return null;
                var vworld = ReflectionHelper.GetFieldValue(hub, "<vworld>k__BackingField");
                if (vworld == null) return null;
                return ReflectionHelper.GetFieldValue(vworld, "_vRoomManager");
            }
            catch { return null; }
        }

        private object? GetCurrentRoom()
        {
            try
            {
                var roomApi = ReflectionHelper.FindType("MimicAPI.GameAPI.RoomAPI");
                if (roomApi != null)
                {
                    var m = roomApi.GetMethod("GetCurrentRoom", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    return m?.Invoke(null, null);
                }
                var vRoomManager = GetVRoomManager();
                if (vRoomManager == null) return null;
                var dict = ReflectionHelper.GetFieldValue(vRoomManager, "_vrooms") as IDictionary;
                if (dict == null) return null;
                foreach (var room in dict.Values)
                    if (room != null && ReflectionHelper.InvokeMethod(room, "IsPlayable") is bool b && b)
                        return room;
            }
            catch { }
            return null;
        }

        // デバッグ用：現在のスキャン状況を取得
        public (int mimics, int monsters) GetLastScanResult() => (_lastMimicCount, _lastMonsterCount);
    }
}
