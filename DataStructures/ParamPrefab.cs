using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UC
{

    [Serializable]
    public class ParamValue
    {
        public ParamValue() { type = null; }
        public ParamValue(Type t)
        {
            if (t == typeof(int)) SetValue(0);
            else if (t == typeof(float)) SetValue(0.0f);
            else if (t == typeof(bool)) SetValue(false);
            else if (t == typeof(string)) SetValue((string)null);
            else if (t == typeof(Color)) SetValue(Color.white);
            else if (t == typeof(Vector2)) SetValue(Vector2.zero);
            else if (t == typeof(Vector3)) SetValue(Vector3.zero);
            else if (t == typeof(Vector4)) SetValue(Vector4.zero);
            else if (t == typeof(Quaternion)) SetValue(Quaternion.identity);
            else if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                SetValue(t, null);
            }

            Debug.LogWarning($"Unsupported type for ParamPrefab ParamValue ({t})!");
        }
        public ParamValue(int v) { SetValue(v); }
        public ParamValue(float v) { SetValue(v); }
        public ParamValue(bool v) { SetValue(v); }
        public ParamValue(string v) { SetValue(v); }
        public ParamValue(Color v) { SetValue(v); }
        public ParamValue(Vector2 v) { SetValue(v); }
        public ParamValue(Vector3 v) { SetValue(v); }
        public ParamValue(Vector4 v) { SetValue(v); }
        public ParamValue(Quaternion v) { SetValue(v); }
        public ParamValue(UnityEngine.Object v) { SetValue(v); }

        [SerializeField, HideInInspector]
        private string typeName;

        public Type type
        {
            get => string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
            set => typeName = value?.AssemblyQualifiedName;
        }

        public int intValue;
        public float floatValue;
        public bool boolValue;
        public string stringValue;
        public Color colorValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public Vector4 vector4Value;
        public Quaternion quaternionValue;
        public UnityEngine.Object objectValue;

        public object GetValue()
        {
            if (type == typeof(int)) return intValue;
            else if (type == typeof(float)) return floatValue;
            else if (type == typeof(bool)) return boolValue;
            else if (type == typeof(string)) return stringValue;
            else if (type == typeof(Color)) return colorValue;
            else if (type == typeof(Vector2)) return vector2Value;
            else if (type == typeof(Vector3)) return vector3Value;
            else if (type == typeof(Vector4)) return vector4Value;
            else if (type == typeof(Quaternion)) return quaternionValue;
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return objectValue;
            }

            return null;
        }

        public void SetValue(string s)
        {
            type = typeof(string);
            stringValue = s;
        }
        public void SetValue(Type type, UnityEngine.Object obj)
        {
            this.type = type;
            objectValue = obj;
        }

        public void SetValue(object value)
        {
            switch (value)
            {
                case int i:
                    type = typeof(int);
                    intValue = i;
                    break;
                case float f:
                    type = typeof(float);
                    floatValue = f;
                    break;
                case bool b:
                    type = typeof(bool);
                    boolValue = b;
                    break;
                case string s:
                    type = typeof(string);
                    stringValue = s;
                    break;
                case Color c:
                    type = typeof(Color);
                    colorValue = c;
                    break;
                case Vector2 v2:
                    type = typeof(Vector2);
                    vector2Value = v2;
                    break;
                case Vector3 v3:
                    type = typeof(Vector3);
                    vector3Value = v3;
                    break;
                case Vector4 v4:
                    type = typeof(Vector4);
                    vector4Value = v4;
                    break;
                case Quaternion q:
                    type = typeof(Quaternion);
                    quaternionValue = q;
                    break;
                case UnityEngine.Object obj:
                    if (obj != null)
                        type = obj.GetType();  // capture actual concrete type
                    else
                        type = typeof(UnityEngine.Object);
                    objectValue = obj;
                    break;
                default:
                    type = null;
                    break;
            }
        }
    }

    [System.Serializable]
    public class ParamPrefabBase
    {

    }

    [Serializable]
    public class ParamPrefab<T> : ParamPrefabBase where T : UnityEngine.Object
    {
        [Serializable]
        public class Param
        {
            public string name;
            public bool overrideValue;
            public ParamValue value = new ParamValue();
        }

        [SerializeField] private T prefabObject;
        [SerializeField] private List<Param> parameters = new();

        public string name => prefabObject.name;

        public bool HasPrefab => (this != null) && (prefabObject != null);

        public T Instantiate()
        {
            if (prefabObject == null) return null;

            var newObject = GameObject.Instantiate(prefabObject);

            ApplyProperties(newObject, parameters);

            return newObject;
        }

        public T Instantiate(Vector3 position, Quaternion rotation)
        {
            if (prefabObject == null) return null;

            var newObject = GameObject.Instantiate(prefabObject, position, rotation);

            ApplyProperties(newObject, parameters);

            return newObject;
        }

        private void ApplyProperties(T newObject, List<Param> parameters)
        {
            if (newObject == null || parameters == null || parameters.Count == 0)
                return;

            GameObject baseGameObject = null;

            if (newObject is GameObject go)
            {
                baseGameObject = go;
            }
            else if (newObject is Component c)
            {
                baseGameObject = c.gameObject;
            }

            if (baseGameObject == null)
            {
                Debug.LogWarning($"ApplyProperties: Unsupported object type ({typeof(T)})");
                return;
            }


            var allTransforms = baseGameObject.GetComponentsInChildren<Transform>(true);

            foreach (var t in allTransforms)
            {
                var components = t.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;

                    var fields = comp.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var field in fields)
                    {
                        if (Attribute.IsDefined(field, typeof(PrefabParamAttribute)))
                        {
                            string paramName = $"{comp.GetType().Name}.{field.Name}";

                            Param param = GetParameter(paramName);
                            if ((param != null) && (param.overrideValue))
                            {
                                try
                                {
                                    field.SetValue(comp, param.value.GetValue());
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"ApplyProperties: Failed to apply param '{paramName}': {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
        }


        private void UpdateParameters()
        {
            var newParameters = new List<Param>();

            if (prefabObject == null) return;

            GameObject baseGameObject = prefabObject as GameObject;
            if (baseGameObject == null)
            {
                Component c = prefabObject as Component;
                if (c != null)
                {
                    baseGameObject = c.gameObject;
                }
                else
                {
                    Debug.LogWarning($"PrefabParam: Unsupported prefab type ({prefabObject.GetType()})!");
                    return;
                }
            }

            var allTransforms = baseGameObject.GetComponentsInChildren<Transform>(true);

            foreach (var t in allTransforms)
            {
                var components = t.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;

                    var fields = comp.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var field in fields)
                    {
                        if (Attribute.IsDefined(field, typeof(PrefabParamAttribute)))
                        {
                            string paramName = $"{comp.GetType().Name}.{field.Name}";

                            var param = GetParameter(paramName);

                            ParamValue value = null;
                            bool overrideValue = false;
                            if ((param == null) || (param.value.type == null))
                            {
                                object fieldValue = field.GetValue(comp);
                                value = new ParamValue();
                                value.SetValue(fieldValue);
                            }
                            else
                            {
                                value = param.value;
                                overrideValue = param.overrideValue;
                            }

                            value.type = field.FieldType;

                            newParameters.Add(new Param
                            {
                                name = paramName,
                                overrideValue = overrideValue,
                                value = value
                            });
                        }
                    }
                }
            }

            parameters = newParameters;
        }

        Param GetParameter(string paramName)
        {
            foreach (var p in parameters)
            {
                if (p.name == paramName) return p;
            }

            return null;
        }
    }

    public class PrefabParamAttribute : PropertyAttribute
    {

    }
}