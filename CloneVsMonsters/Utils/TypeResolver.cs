using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;

namespace CloneVsMonsters.Utils
{
    /// <summary>
    /// ゲームのバージョン差異を吸収するため、実行時に Assembly-CSharp 内の型を自動解決する。
    /// クラス名がバージョン間で変わっても、候補リストから見つかったものを採用する。
    /// </summary>
    internal static class TypeResolver
    {
        // 解決済みキャッシュ
        private static readonly Dictionary<string, Type?> Cache = new();

        // 偽物（ミメシス）に関連する可能性のある型名候補
        // 実際のゲームでは VMonster のサブタイプ、または Mimic という名前のクラスが使われている可能性がある
        public static readonly string[] MimicTypeCandidates = new[]
        {
            "Mimic.VMimic",
            "Mimic.Actors.VMimic",
            "VMimic",
            "Mimic.MimicActor",
            "Mimic.Actors.MimicActor",
            "MimicActor",
            "VFakePlayer",
            "FakePlayer",
            "VFaker",
            "Faker",
            "VClone",
            "CloneActor",
            "MimicClone",
        };

        // モンスター型候補
        public static readonly string[] MonsterTypeCandidates = new[]
        {
            "Mimic.Actors.VMonster",
            "Mimic.VMonster",
            "VMonster",
            "Monster",
            "VEnemy",
            "EnemyActor",
            "VCreature",
            "Mimic.Monster",
        };

        // プレイヤー型候補
        public static readonly string[] PlayerTypeCandidates = new[]
        {
            "Mimic.Actors.ProtoActor",
            "Mimic.ProtoActor",
            "ProtoActor",
            "Mimic.Actors.VPlayer",
            "VPlayer",
            "VActor",
            "PlayerActor",
        };

        // ダメージ系メソッド候補
        public static readonly string[] DamageMethodCandidates = new[]
        {
            "TakeDamage",
            "ApplyDamage",
            "OnDamage",
            "OnHit",
            "Hit",
            "Damage",
            "ReceiveDamage",
            "ProcessHit",
            "AddDamage",
            "SetDamage",
        };

        // 敵対判定メソッド候補
        public static readonly string[] HostilityMethodCandidates = new[]
        {
            "IsEnemy",
            "IsHostile",
            "IsHostileTo",
            "IsEnemyTo",
            "CanAttack",
            "CanTarget",
            "IsValidTarget",
            "ShouldAttack",
            "IsAttackable",
            "CheckHostility",
        };

        // ターゲット選択メソッド候補
        public static readonly string[] TargetMethodCandidates = new[]
        {
            "SelectTarget",
            "FindTarget",
            "SearchTarget",
            "ChooseTarget",
            "UpdateTarget",
            "SetTarget",
            "FindNearestTarget",
            "SearchVictim",
            "ChooseVictim",
            "SelectVictim",
            "GetTarget",
            "AcquireTarget",
        };

        private static Type? ResolveFromCandidates(string[] candidates)
        {
            foreach (var name in candidates)
            {
                // フルネームで試す
                var t = ReflectionHelper.FindType(name);
                if (t != null) return t;

                // 短い名前で試す
                var shortName = name.Contains('.') ? name.Split('.').Last() : name;
                t = ReflectionHelper.FindTypeByName(shortName);
                if (t != null) return t;
            }
            return null;
        }

        public static Type? ResolveMimicType() => ResolveCached("Mimic", MimicTypeCandidates);
        public static Type? ResolveMonsterType() => ResolveCached("Monster", MonsterTypeCandidates);
        public static Type? ResolvePlayerType() => ResolveCached("Player", PlayerTypeCandidates);

        private static Type? ResolveCached(string key, string[] candidates)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var type = ResolveFromCandidates(candidates);
            Cache[key] = type;
            if (type != null)
                MelonLogger.Msg($"[TypeResolver] {key} => {type.FullName}");
            else
                MelonLogger.Warning($"[TypeResolver] {key} type not found (candidates: {string.Join(", ", candidates)})");
            return type;
        }

        /// <summary>
        /// Assembly-CSharp 内の全ての型から、特定のメソッド名を持つ型を列挙する。
        /// 例: "TakeDamage" を持つ型を全て取得してパッチ対象にする。
        /// </summary>
        public static List<Type> FindTypesWithMethod(string methodName)
        {
            var result = new List<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // ゲームアセンブリのみ対象（Assembly-CSharp、Mimic関連）
                if (!asm.FullName.Contains("Assembly-CSharp") && !asm.FullName.Contains("Mimic"))
                    continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    try
                    {
                        if (t.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static) != null)
                            result.Add(t);
                    }
                    catch { }
                }
            }
            return result;
        }

        /// <summary>
        /// 全候補メソッド名を持つ型を列挙（ダメージ/敵対/ターゲット系）
        /// </summary>
        public static void LogAllFoundTypes()
        {
            MelonLogger.Msg("[TypeResolver] === Scanning for patchable types ===");
            foreach (var m in DamageMethodCandidates)
            {
                var types = FindTypesWithMethod(m);
                if (types.Count > 0)
                    MelonLogger.Msg($"  Damage method '{m}' found in: {string.Join(", ", types.Select(t => t.FullName))}");
            }
            foreach (var m in HostilityMethodCandidates)
            {
                var types = FindTypesWithMethod(m);
                if (types.Count > 0)
                    MelonLogger.Msg($"  Hostility method '{m}' found in: {string.Join(", ", types.Select(t => t.FullName))}");
            }
            foreach (var m in TargetMethodCandidates)
            {
                var types = FindTypesWithMethod(m);
                if (types.Count > 0)
                    MelonLogger.Msg($"  Target method '{m}' found in: {string.Join(", ", types.Select(t => t.FullName))}");
            }
            MelonLogger.Msg("[TypeResolver] === Scan complete ===");
        }

        /// <summary>
        /// オブジェクトが「偽物」かどうかを判定する。
        /// 1. 型名に Mimic/Fake/Clone/Faker が含まれる
        /// 2. 特定のフラグフィールド（isMimic, isFake など）が true
        /// 3. VMonster だが特別な Mimic フラグを持つ（ゲーム仕様による）
        /// </summary>
        public static bool IsMimic(object? actor)
        {
            if (actor == null) return false;
            var type = actor.GetType();

            // 型名チェック
            var typeName = type.FullName ?? type.Name;
            if (typeName.Contains("Mimic", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Fake", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Clone", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Faker", StringComparison.OrdinalIgnoreCase))
            {
                // VMonster の場合は追加チェックが必要（全モンスターを偽物と誤判定しないため）
                // VMonster 型そのものは Mimic とは別なので、サブクラスかフラグで判定
                if (typeName.Contains("VMonster") && !typeName.Contains("Mimic") && !typeName.Contains("Fake") && !typeName.Contains("Clone"))
                    return false;
                return true;
            }

            // フィールド/プロパティフラグチェック
            var flagNames = new[] { "isMimic", "isFake", "isClone", "isFaker", "IsMimic", "IsFake", "IsClone", "mimicFlag", "_isMimic", "_isFake" };
            foreach (var flag in flagNames)
            {
                try
                {
                    var val = ReflectionHelper.GetFieldValue(actor, flag);
                    if (val is bool b && b) return true;
                    val = ReflectionHelper.GetPropertyValue(actor, flag);
                    if (val is bool b2 && b2) return true;
                }
                catch { }
            }

            return false;
        }

        /// <summary>
        /// オブジェクトが「モンスター」かどうかを判定する。
        /// VMonster 型または名前に Monster/Enemy/Creature を含む型。
        /// ただし偽物は除外する。
        /// </summary>
        public static bool IsMonster(object? actor)
        {
            if (actor == null) return false;
            if (IsMimic(actor)) return false; // 偽物はモンスター扱いしない

            var type = actor.GetType();
            var typeName = type.FullName ?? type.Name;

            if (typeName.Contains("VMonster") ||
                typeName.Contains("Monster", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Enemy", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Creature", StringComparison.OrdinalIgnoreCase) ||
                type.Name == "VMonster")
                return true;

            // 親クラスチェック
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == "VMonster" || baseType.Name.Contains("Monster"))
                    return true;
                baseType = baseType.BaseType;
            }

            return false;
        }

        /// <summary>
        /// オブジェクトが「プレイヤー」かどうかを判定する。
        /// ProtoActor / VPlayer / VActor など。
        /// </summary>
        public static bool IsPlayer(object? actor)
        {
            if (actor == null) return false;
            if (IsMimic(actor)) return false;
            if (IsMonster(actor)) return false;

            var type = actor.GetType();
            var typeName = type.FullName ?? type.Name;

            if (typeName.Contains("ProtoActor") ||
                typeName.Contains("VPlayer") ||
                (typeName.Contains("VActor") && !typeName.Contains("Monster")) ||
                typeName.Contains("PlayerActor"))
                return true;

            return false;
        }

        /// <summary>
        /// デバッグ用：オブジェクトの型情報を文字列化
        /// </summary>
        public static string DescribeActor(object? actor)
        {
            if (actor == null) return "null";
            var type = actor.GetType();
            var name = type.FullName ?? type.Name;
            var mimic = IsMimic(actor) ? "[MIMIC]" : "";
            var monster = IsMonster(actor) ? "[MONSTER]" : "";
            var player = IsPlayer(actor) ? "[PLAYER]" : "";
            return $"{name} {mimic}{monster}{player} (Hash:{actor.GetHashCode():X})";
        }
    }
}
