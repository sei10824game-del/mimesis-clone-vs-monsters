using System;
using System.Reflection;

namespace CloneVsMonsters.Utils
{
    /// <summary>
    /// MimicAPI の ReflectionHelper と同等の軽量実装。
    /// ゲームのプライベートフィールド/メソッドに安全にアクセスするためのヘルパー。
    /// </summary>
    internal static class ReflectionHelper
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        public static object? GetFieldValue(object target, string fieldName)
        {
            if (target == null) return null;
            var field = target.GetType().GetField(fieldName, Flags);
            return field?.GetValue(target);
        }

        public static T? GetFieldValue<T>(object target, string fieldName) where T : class
        {
            return GetFieldValue(target, fieldName) as T;
        }

        public static T GetFieldValueStruct<T>(object target, string fieldName, T defaultValue = default) where T : struct
        {
            var val = GetFieldValue(target, fieldName);
            return val is T t ? t : defaultValue;
        }

        public static void SetFieldValue(object target, string fieldName, object? value)
        {
            if (target == null) return;
            var field = target.GetType().GetField(fieldName, Flags);
            field?.SetValue(target, value);
        }

        public static object? InvokeMethod(object target, string methodName, params object[]? args)
        {
            if (target == null) return null;
            var method = target.GetType().GetMethod(methodName, Flags);
            if (method == null) return null;
            return method.Invoke(target, args.Length > 0 ? args : null);
        }

        public static object? InvokeMethod(Type type, string methodName, params object[]? args)
        {
            var method = type.GetMethod(methodName, Flags);
            if (method == null) return null;
            return method.Invoke(null, args.Length > 0 ? args : null);
        }

        public static T? InvokeMethod<T>(object target, string methodName, params object[]? args) where T : class
        {
            return InvokeMethod(target, methodName, args) as T;
        }

        public static object? GetPropertyValue(object target, string propertyName)
        {
            if (target == null) return null;
            var prop = target.GetType().GetProperty(propertyName, Flags);
            return prop?.GetValue(target);
        }

        public static T? GetPropertyValue<T>(object target, string propertyName) where T : class
        {
            return GetPropertyValue(target, propertyName) as T;
        }

        public static bool TryGetFieldValue<T>(object target, string fieldName, out T? value)
        {
            value = default;
            if (target == null) return false;
            var field = target.GetType().GetField(fieldName, Flags);
            if (field == null) return false;
            var raw = field.GetValue(target);
            if (raw is T t)
            {
                value = t;
                return true;
            }
            return false;
        }

        public static Type? FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        public static Type? FindTypeByName(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == name || t.FullName == name)
                            return t;
                    }
                }
                catch { /* Dynamic assemblies may throw */ }
            }
            return null;
        }
    }
}
