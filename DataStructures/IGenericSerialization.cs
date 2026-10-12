using System;
using UnityEngine;

namespace UC
{
    public interface IGenericSerialization
    {
        public static string GenericSerialize(object obj)
        {
            return GenericSerializer.Serialize(obj);
        }

        // Only the JSON is needed: resolveAsset is for when asset references have to come from
        // somewhere other than the default, and source just names the file in the log.
        public static object GenericDeserialize(string json, GenericSerializer.AssetResolver resolveAsset = null, string source = null)
        {
            return GenericSerializer.Deserialize(json, resolveAsset, source);
        }
    }

    // Keeps a field out of the generic serialization, even though Unity serializes it: editor state,
    // back references to the owner, and whatever else makes no sense outside of the asset itself.
    [AttributeUsage(AttributeTargets.Field)]
    public class GenericSerializationIgnoreAttribute : Attribute
    {
    }
}
