using System;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

namespace UC
{

    public static class PlayerPrefsHelpers
    {
        public static Vector4 GetVector4(string key, Vector4 defaultValue = default)
        {
            if (PlayerPrefs.HasKey(key))
            {
                string value = PlayerPrefs.GetString(key);
                string[] values = value.Split(';');
                if (values.Length == 4)
                {
                    Vector4 val = new Vector4();
                    if (float.TryParse(values[0], out val.x) &&
                        float.TryParse(values[1], out val.y) &&
                        float.TryParse(values[2], out val.z) &&
                        float.TryParse(values[3], out val.w))
                    {
                        return val;
                    }
                }
            }

            return defaultValue;

        }

        public static void SetVector4(string key, Vector4 value)
        {
            PlayerPrefs.SetString(key, $"{value.x};{value.y};{value.z};{value.w}");
        }

        public static void SetBool(string key, bool bValue)
        {
            PlayerPrefs.SetString(key, (bValue) ? ("true") : ("false"));
        }

        public static bool GetBool(string key, bool defaultValue)
        {
            if (PlayerPrefs.HasKey(key))
            {
                string value = PlayerPrefs.GetString(key).ToLower();
                if (value == "true") return true;
                return false;
            }
            return defaultValue;
        }
    }
}