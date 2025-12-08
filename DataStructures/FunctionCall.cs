using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace UC
{

    [Serializable]
    public class FunctionCall
    {
        public string componentName;
        public string functionName;
        public string[] parameters;

        public void Invoke(GameObject go)
        {
            Invoke(go, null);
        }

        // New entry point: allows runtime overrides
        public void Invoke(GameObject go, params object[] runtimeParameters)
        {
            if (go == null)
            {
                Debug.LogWarning("[FunctionCall] Target GameObject is null");
                return;
            }

            if (string.IsNullOrEmpty(componentName))
            {
                Debug.LogWarning($"[FunctionCall] componentName is empty on {go.name}");
                return;
            }

            if (string.IsNullOrEmpty(functionName))
            {
                Debug.LogWarning($"[FunctionCall] functionName is empty on {go.name}");
                return;
            }

            var component = FindComponent(go, componentName);
            if (component == null)
            {
                Debug.LogWarning($"[FunctionCall] Component '{componentName}' not found on {go.name}");
                return;
            }

            var compType = component.GetType();
            var method = FindMethod(compType, functionName, parameters?.Length ?? 0);

            if (method == null)
            {
                Debug.LogWarning($"[FunctionCall] Method '{functionName}' with {parameters?.Length ?? 0} params " +
                                 $"not found on component {compType.Name} on {go.name}");
                return;
            }

            var paramInfos = method.GetParameters();
            object[] args = new object[paramInfos.Length];

            for (int i = 0; i < paramInfos.Length; ++i)
            {
                var pType = paramInfos[i].ParameterType;

                bool hasOverride = runtimeParameters != null && i < runtimeParameters.Length;
                if (hasOverride)
                {
                    var o = runtimeParameters[i];

                    if (o == null)
                    {
                        // null override => use serialized value
                        string stored = (parameters != null && i < parameters.Length) ? parameters[i] : null;
                        args[i] = ConvertParameter(stored, pType);
                    }
                    else if (o is string s)
                    {
                        // string override => parse like normal
                        args[i] = ConvertParameter(s, pType);
                    }
                    else
                    {
                        // non-string override => try to use directly
                        if (pType.IsInstanceOfType(o))
                        {
                            args[i] = o;
                        }
                        else
                        {
                            try
                            {
                                args[i] = Convert.ChangeType(o, pType, System.Globalization.CultureInfo.InvariantCulture);
                            }
                            catch
                            {
                                Debug.LogWarning(
                                    $"[FunctionCall] Cannot convert runtime parameter {i} ({o.GetType().Name}) " +
                                    $"to {pType.Name}; using default."
                                );
                                string stored = (parameters != null && i < parameters.Length) ? parameters[i] : null;
                                args[i] = ConvertParameter(stored, pType);
                            }
                        }
                    }
                }
                else
                {
                    // No runtime override => use serialized parameter
                    string stored = (parameters != null && i < parameters.Length) ? parameters[i] : null;
                    args[i] = ConvertParameter(stored, pType);
                }
            }

            try
            {
                method.Invoke(component, args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FunctionCall] Exception calling {compType.Name}.{functionName} on {go.name}: {ex}");
            }
        }

        static Component FindComponent(GameObject go, string typeName)
        {
            var targetType = GetTypeFromString(typeName);
            if (targetType == null)
                return null;

            return go.GetComponent(targetType);
        }

        static MethodInfo FindMethod(Type type, string methodName, int paramCount)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var methods = type.GetMethods(flags);
            foreach (var m in methods)
            {
                if (m.Name != methodName) continue;
                var p = m.GetParameters();
                if (p.Length == paramCount)
                    return m;
            }

            return null;
        }

        public static Type GetTypeFromString(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;

            // Try direct
            var type = Type.GetType(fullName);
            if (type != null) return type;

            // Try loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                type = asm.GetType(fullName);
                if (type != null) return type;
            }

            // Fallback: search by simple name
            foreach (var asm in assemblies)
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                if (types == null) continue;
                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (t.Name == fullName)
                        return t;
                }
            }

            return null;
        }

        // ---- basic string -> type converters ----

        static object ConvertParameter(string value, Type targetType)
        {
            if (targetType == typeof(string))
                return value ?? string.Empty;

            if (string.IsNullOrEmpty(value))
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            var ci = CultureInfo.InvariantCulture;

            if (targetType == typeof(int))
            {
                int.TryParse(value, NumberStyles.Integer, ci, out int v);
                return v;
            }
            if (targetType == typeof(float))
            {
                float.TryParse(value, NumberStyles.Float, ci, out float v);
                return v;
            }
            if (targetType == typeof(bool))
            {
                bool.TryParse(value, out bool v);
                return v;
            }
            if (targetType == typeof(double))
            {
                double.TryParse(value, NumberStyles.Float, ci, out double v);
                return v;
            }
            if (targetType == typeof(Vector2))
                return ParseVector2(value, ci);
            if (targetType == typeof(Vector3))
                return ParseVector3(value, ci);
            if (targetType == typeof(Vector4))
                return ParseVector4(value, ci);
            if (targetType == typeof(Quaternion))
            {
                var v4 = ParseVector4(value, ci);
                return new Quaternion(v4.x, v4.y, v4.z, v4.w);
            }
            if (targetType == typeof(Color))
                return ParseColor(value, ci);

            try
            {
                return Convert.ChangeType(value, targetType, ci);
            }
            catch
            {
                Debug.LogWarning($"[FunctionCall] Could not convert '{value}' to {targetType}");
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }

        public static Vector2 ParseVector2(string s, IFormatProvider ci)
        {
            var parts = s.Split(',');
            float x = parts.Length > 0 ? float.Parse(parts[0], ci) : 0;
            float y = parts.Length > 1 ? float.Parse(parts[1], ci) : 0;
            return new Vector2(x, y);
        }

        public static Vector3 ParseVector3(string s, IFormatProvider ci)
        {
            var parts = s.Split(',');
            float x = parts.Length > 0 ? float.Parse(parts[0], ci) : 0;
            float y = parts.Length > 1 ? float.Parse(parts[1], ci) : 0;
            float z = parts.Length > 2 ? float.Parse(parts[2], ci) : 0;
            return new Vector3(x, y, z);
        }

        public static Vector4 ParseVector4(string s, IFormatProvider ci)
        {
            var parts = s.Split(',');
            float x = parts.Length > 0 ? float.Parse(parts[0], ci) : 0;
            float y = parts.Length > 1 ? float.Parse(parts[1], ci) : 0;
            float z = parts.Length > 2 ? float.Parse(parts[2], ci) : 0;
            float w = parts.Length > 3 ? float.Parse(parts[3], ci) : 1;
            return new Vector4(x, y, z, w);
        }

        public static Color ParseColor(string s, IFormatProvider ci)
        {
            if (string.IsNullOrEmpty(s))
                return Color.white;

            // Try HTML style first: "#RRGGBB" or "#RRGGBBAA"
            if (ColorUtility.TryParseHtmlString(s, out var c))
                return c;

            // Fallback: "r,g,b[,a]" as floats 0–1
            var parts = s.Split(',');
            float r = parts.Length > 0 && float.TryParse(parts[0], NumberStyles.Float, ci, out var vr) ? vr : 0f;
            float g = parts.Length > 1 && float.TryParse(parts[1], NumberStyles.Float, ci, out var vg) ? vg : 0f;
            float b = parts.Length > 2 && float.TryParse(parts[2], NumberStyles.Float, ci, out var vb) ? vb : 0f;
            float a = parts.Length > 3 && float.TryParse(parts[3], NumberStyles.Float, ci, out var va) ? va : 1f;
            return new Color(r, g, b, a);
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class FunctionCallOptionsAttribute : PropertyAttribute
    {
        public bool ShowParameters { get; }
        public bool FilterBySignature { get; }
        public Type[] SignatureTypes { get; }

        public FunctionCallOptionsAttribute(bool showParameters = true, bool filterBySignature = false, params Type[] signatureTypes)
        {
            ShowParameters = showParameters;
            FilterBySignature = filterBySignature;
            SignatureTypes = signatureTypes ?? Array.Empty<Type>();
        }
    }
}
