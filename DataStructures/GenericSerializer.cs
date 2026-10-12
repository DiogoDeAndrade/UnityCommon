using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine;
using UnityEngine.Assemblies;

namespace UC
{
    // Reflection based JSON export and import, the engine behind IGenericSerialization.GenericSerialize
    // and GenericDeserialize.
    //
    // Fields are picked the way Unity picks them (public or [SerializeField]/[SerializeReference], not
    // [NonSerialized], of a type Unity can serialize) over the whole class hierarchy, so the JSON
    // mirrors what ends up in the .asset file - minus anything tagged [GenericSerializationIgnore].
    //
    //  - Typed objects (the root, and whatever sits behind a [SerializeReference]) get a "$type" member
    //    with the full name of their actual type, which is what it takes to rebuild them.
    //  - [SerializeReference] values are only followed if they implement IGenericSerialization; the
    //    others are exported as null, with a warning. The same goes the other way: "$type" can only
    //    name a type that opted in, a file doesn't get to instantiate whatever it feels like.
    //  - References to assets are exported as "<asset path>", or as "<asset path>:<name>" for objects
    //    that live inside another asset (a Sprite inside a texture, for instance). The AssetDatabase
    //    turns them back into assets in the editor, RuntimeAssetDatabase does it everywhere else.
    //  - Reading is forgiving about what's missing: a field the JSON doesn't mention keeps whatever
    //    value the object was constructed with.
    public static class GenericSerializer
    {
        // Finds the asset behind a reference written by GetAssetReference; null if there's none of that type
        public delegate UnityEngine.Object AssetResolver(string reference, Type type);

        // Not plain "type": that collides with actual fields (RPGStatModule.type, for one), while no
        // C# identifier can start with a '$'.
        public const string typeKey = "$type";
        public const char subAssetSeparator = ':';

        const BindingFlags declaredFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // Unity's own types: most keep their data in private or native fields that reflection can't
        // get to, so each one states how to take it apart and how to put it back together.
        readonly struct BuiltinType
        {
            public readonly Func<object, (string name, object value)[]> getMembers;
            public readonly Func<MemberReader, object>                  create;

            public BuiltinType(Func<object, (string name, object value)[]> getMembers, Func<MemberReader, object> create)
            {
                this.getMembers = getMembers;
                this.create = create;
            }
        }

        static readonly Dictionary<Type, BuiltinType>       builtinTypes = new();
        static readonly Dictionary<Type, List<FieldInfo>>   serializedFieldsCache = new();
        static readonly Dictionary<string, Type>            typesByName = new();

        static GenericSerializer()
        {
            AddBuiltin<Vector2>(v => Members(("x", v.x), ("y", v.y)),
                                m => new Vector2(m.Get<float>("x"), m.Get<float>("y")));
            AddBuiltin<Vector3>(v => Members(("x", v.x), ("y", v.y), ("z", v.z)),
                                m => new Vector3(m.Get<float>("x"), m.Get<float>("y"), m.Get<float>("z")));
            AddBuiltin<Vector4>(v => Members(("x", v.x), ("y", v.y), ("z", v.z), ("w", v.w)),
                                m => new Vector4(m.Get<float>("x"), m.Get<float>("y"), m.Get<float>("z"), m.Get<float>("w")));
            AddBuiltin<Vector2Int>(v => Members(("x", v.x), ("y", v.y)),
                                   m => new Vector2Int(m.Get<int>("x"), m.Get<int>("y")));
            AddBuiltin<Vector3Int>(v => Members(("x", v.x), ("y", v.y), ("z", v.z)),
                                   m => new Vector3Int(m.Get<int>("x"), m.Get<int>("y"), m.Get<int>("z")));
            // Left out, w and alpha are what they almost always are, instead of a zero nobody meant
            AddBuiltin<Quaternion>(q => Members(("x", q.x), ("y", q.y), ("z", q.z), ("w", q.w)),
                                   m => new Quaternion(m.Get<float>("x"), m.Get<float>("y"), m.Get<float>("z"), m.Get("w", 1.0f)));
            AddBuiltin<Color>(c => Members(("r", c.r), ("g", c.g), ("b", c.b), ("a", c.a)),
                              m => new Color(m.Get<float>("r"), m.Get<float>("g"), m.Get<float>("b"), m.Get("a", 1.0f)));
            AddBuiltin<Color32>(c => Members(("r", c.r), ("g", c.g), ("b", c.b), ("a", c.a)),
                                m => new Color32(m.Get<byte>("r"), m.Get<byte>("g"), m.Get<byte>("b"), m.Get<byte>("a", 255)));
            AddBuiltin<Rect>(r => Members(("x", r.x), ("y", r.y), ("width", r.width), ("height", r.height)),
                             m => new Rect(m.Get<float>("x"), m.Get<float>("y"), m.Get<float>("width"), m.Get<float>("height")));
            AddBuiltin<RectInt>(r => Members(("x", r.x), ("y", r.y), ("width", r.width), ("height", r.height)),
                                m => new RectInt(m.Get<int>("x"), m.Get<int>("y"), m.Get<int>("width"), m.Get<int>("height")));
            AddBuiltin<Bounds>(b => Members(("center", b.center), ("extents", b.extents)),
                               m => new Bounds { center = m.Get<Vector3>("center"), extents = m.Get<Vector3>("extents") });
            AddBuiltin<BoundsInt>(b => Members(("position", b.position), ("size", b.size)),
                                  m => new BoundsInt(m.Get<Vector3Int>("position"), m.Get<Vector3Int>("size")));
            AddBuiltin<Keyframe>(k => Members(("time", k.time), ("value", k.value), ("inTangent", k.inTangent), ("outTangent", k.outTangent), ("inWeight", k.inWeight), ("outWeight", k.outWeight), ("weightedMode", k.weightedMode)),
                                 m => new Keyframe(m.Get<float>("time"), m.Get<float>("value"), m.Get<float>("inTangent"), m.Get<float>("outTangent"), m.Get<float>("inWeight"), m.Get<float>("outWeight")) { weightedMode = m.Get<WeightedMode>("weightedMode") });
            AddBuiltin<AnimationCurve>(c => Members(("preWrapMode", c.preWrapMode), ("postWrapMode", c.postWrapMode), ("keys", c.keys)),
                                       m => new AnimationCurve(m.Get<Keyframe[]>("keys") ?? Array.Empty<Keyframe>()) { preWrapMode = m.Get<WrapMode>("preWrapMode"), postWrapMode = m.Get<WrapMode>("postWrapMode") });
            AddBuiltin<GradientColorKey>(k => Members(("color", k.color), ("time", k.time)),
                                         m => new GradientColorKey(m.Get<Color>("color"), m.Get<float>("time")));
            AddBuiltin<GradientAlphaKey>(k => Members(("alpha", k.alpha), ("time", k.time)),
                                         m => new GradientAlphaKey(m.Get<float>("alpha"), m.Get<float>("time")));
            AddBuiltin<Gradient>(g => Members(("mode", g.mode), ("colorSpace", g.colorSpace), ("colorKeys", g.colorKeys), ("alphaKeys", g.alphaKeys)), CreateGradient);
        }

        public static string Serialize(object obj)
        {
            if (obj == null) return "null";

            var exporter = new Exporter(obj);
            exporter.WriteObject(obj, obj.GetType(), typed: true);

            return exporter.ToString();
        }

        // Rebuilds what Serialize wrote; null if there's nothing usable in there (the log says why).
        //
        // Assets are looked up through resolveAsset, which defaults to LoadAssetReference. The source
        // is only there to tell one file from another in the log.
        public static object Deserialize(string json, AssetResolver resolveAsset = null, string source = null)
        {
            var document = Parse(json, source);
            if (document == null) return null;

            object root = document.Create();
            if (root != null) document.Populate(root, resolveAsset);

            return root;
        }

        // The first half of Deserialize: reads the JSON and finds out what it describes, without making
        // anything out of it yet. Null if it's no good (and the log says why).
        public static Document Parse(string json, string source = null)
        {
            var importer = new Importer(null, source);

            object data;
            try
            {
                data = JsonParser.Parse(json ?? "");
            }
            catch (FormatException e)
            {
                importer.Error($"invalid JSON, {e.Message}");
                return null;
            }

            return importer.ReadDocument(data);
        }

        // Data that was read, and isn't an object yet. Having this step in between is what lets
        // several pieces of data reference each other, whatever the order they come in: create all
        // the objects first, fill them in afterwards.
        public sealed class Document
        {
            readonly Dictionary<string, object> _members;
            readonly string                     _source;

            public Type type { get; }

            internal Document(Dictionary<string, object> members, Type type, string source)
            {
                _members = members;
                _source = source;
                this.type = type;
            }

            // A new object of the type the data says it is, with none of the data in it
            public object Create()
            {
                return new Importer(null, _source).CreateRoot(type);
            }

            // Puts the data in an object, which doesn't have to be one that came from Create as long as
            // it's of the right type. The fields that the data doesn't mention are left as they are.
            public void Populate(object target, AssetResolver resolveAsset = null)
            {
                new Importer(resolveAsset ?? LoadAssetReference, _source).PopulateRoot(target, type, _members);
            }
        }

        // "<asset path>" for a main asset, "<asset path>:<name>" for what lives inside one, null for
        // anything that isn't an asset at all (scene objects, runtime instances).
        public static string GetAssetReference(UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath)) return null;

            return UnityEditor.AssetDatabase.IsMainAsset(obj) ? assetPath : $"{assetPath}{subAssetSeparator}{obj.name}";
#else
            // Asset paths only exist in the editor
            return null;
#endif
        }

        // The way back from GetAssetReference. A reference with no name in it stands for whatever
        // there is of that type at the path, the main asset first - so the path to a texture is
        // enough to get its (first) sprite.
        public static UnityEngine.Object LoadAssetReference(string reference, Type type)
        {
#if UNITY_EDITOR
            SplitAssetReference(reference, out string assetPath, out string name);

            if (name == null) return UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, type);

            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if ((asset != null) && (asset.name == name) && (type.IsInstanceOfType(asset))) return asset;
            }

            return null;
#else
            // No AssetDatabase out here, what was baked out of it (and what was registered since) has to do
            return RuntimeAssetDatabase.Find(reference, type);
#endif
        }

        // Asset paths can't have the separator in them, but names can ("mixamorig:Hips"), so it's the first one that counts
        public static void SplitAssetReference(string reference, out string assetPath, out string name)
        {
            int separator = reference.IndexOf(subAssetSeparator);

            assetPath = (separator < 0) ? reference : reference.Substring(0, separator);
            name = (separator < 0) ? null : reference.Substring(separator + 1);
        }

        static void AddBuiltin<T>(Func<T, (string name, object value)[]> getMembers, Func<MemberReader, T> create)
        {
            builtinTypes[typeof(T)] = new BuiltinType(value => getMembers((T)value), members => create(members));
        }

        static (string name, object value)[] Members(params (string name, object value)[] members) => members;

        // What the JSON leaves out stays the way a new Gradient has it
        static Gradient CreateGradient(MemberReader members)
        {
            var gradient = new Gradient();

            gradient.mode = members.Get("mode", gradient.mode);
            gradient.colorSpace = members.Get("colorSpace", gradient.colorSpace);
            gradient.SetKeys(members.Get<GradientColorKey[]>("colorKeys") ?? gradient.colorKeys, members.Get<GradientAlphaKey[]>("alphaKeys") ?? gradient.alphaKeys);

            return gradient;
        }

        // Every type that reading or writing an object of the given type can get to through its fields:
        // the type, what it derives from, and the classes, structs and enums it's made of. What sits
        // behind a [SerializeReference] isn't in there (that could be anything), only the declared type.
        public static void CollectSerializedTypes(Type type, HashSet<Type> types)
        {
            if (TryGetElementType(type, out var elementType)) type = elementType;
            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments()) CollectSerializedTypes(argument, types);
            }

            // Nothing to look for in the primitives, in what belongs to the framework, or in references to assets
            if ((type.IsPrimitive) || (type.IsGenericParameter) || (IsInNamespace(type, "System"))) return;
            if ((typeof(UnityEngine.Object).IsAssignableFrom(type)) && (!typeof(IGenericSerialization).IsAssignableFrom(type))) return;
            if (!types.Add(type)) return;

            // Some of its fields are declared by what it derives from
            for (var baseType = type.BaseType; (baseType != null) && (baseType != typeof(object)); baseType = baseType.BaseType)
            {
                types.Add(baseType);
            }

            foreach (var field in GetSerializedFields(type))
            {
                CollectSerializedTypes(field.FieldType, types);
            }
        }

        // Full name, namespace included, in whatever assembly has it. Only the hits are cached:
        // assemblies can still show up later.
        public static Type FindType(string fullName)
        {
            if (typesByName.TryGetValue(fullName, out var type)) return type;

            foreach (var assembly in CurrentAssemblies.GetLoadedAssemblies())
            {
                type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    typesByName[fullName] = type;
                    break;
                }
            }

            return type;
        }

        // The fields Unity serializes for this type, base classes first (which is also Unity's order)
        static List<FieldInfo> GetSerializedFields(Type type)
        {
            if (serializedFieldsCache.TryGetValue(type, out var fields)) return fields;

            fields = new List<FieldInfo>();
            for (var currentType = type; (currentType != null) && (currentType != typeof(object)); currentType = currentType.BaseType)
            {
                // Reflection doesn't promise declaration order, the metadata tokens do
                var declaredFields = currentType.GetFields(declaredFieldFlags);
                Array.Sort(declaredFields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));

                int insertAt = 0;
                foreach (var field in declaredFields)
                {
                    if (IsSerializedField(field)) fields.Insert(insertAt++, field);
                }
            }

            serializedFieldsCache[type] = fields;

            return fields;
        }

        static bool IsSerializedField(FieldInfo field)
        {
            if ((field.IsStatic) || (field.IsInitOnly) || (field.IsNotSerialized)) return false;
            if (field.IsDefined(typeof(GenericSerializationIgnoreAttribute), false)) return false;

            return (field.IsPublic) || (field.IsDefined(typeof(SerializeField), false)) || (field.IsDefined(typeof(SerializeReference), false));
        }

        // Can Unity serialize a (non [SerializeReference]) field of this type?
        static bool IsSerializableType(Type type)
        {
            if (TryGetElementType(type, out var elementType))
            {
                // No containers of containers
                return (!TryGetElementType(elementType, out _)) && (IsSerializableType(elementType));
            }

            if (type.IsPrimitive) return (type != typeof(IntPtr)) && (type != typeof(UIntPtr));
            if ((type.IsEnum) || (type == typeof(string))) return true;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return true;
            if ((type == typeof(LayerMask)) || (builtinTypes.ContainsKey(type))) return true;

            // What's left are plain classes and structs: they have to be [Serializable] and concrete,
            // and the framework's own don't count (Dictionary<,> is [Serializable], for instance).
            return (type.IsSerializable) && (!type.IsAbstract) && (!IsInNamespace(type, "System"));
        }

        // Arrays and List<> are the only containers Unity serializes
        static bool TryGetElementType(Type type, out Type elementType)
        {
            elementType = null;

            if (type.IsArray)
            {
                if (type.GetArrayRank() == 1) elementType = type.GetElementType();
            }
            else if ((type.IsGenericType) && (type.GetGenericTypeDefinition() == typeof(List<>)))
            {
                elementType = type.GetGenericArguments()[0];
            }

            return elementType != null;
        }

        static bool IsInNamespace(Type type, string rootNamespace)
        {
            string typeNamespace = type.Namespace;
            if (typeNamespace == null) return false;

            return (typeNamespace == rootNamespace) || (typeNamespace.StartsWith(rootNamespace + "."));
        }

        // A single export: walks the object, writes the JSON, and keeps track of where it is for the warnings.
        sealed class Exporter
        {
            readonly JsonWriter         _json = new();
            readonly StringBuilder      _path = new();
            readonly List<object>       _openObjects = new();
            readonly UnityEngine.Object _context;

            public Exporter(object root)
            {
                _context = root as UnityEngine.Object;
                _path.Append((_context != null) ? _context.name : root.GetType().Name);
            }

            public override string ToString() => _json.ToString();

            // An object, as its serialized fields. Typed objects also say what they are, since nothing
            // else does: the field that holds them only knows about a base type.
            public void WriteObject(object obj, Type type, bool typed)
            {
                // Only classes can loop back on themselves
                bool trackCycles = !type.IsValueType;
                if (trackCycles)
                {
                    foreach (var openObject in _openObjects)
                    {
                        if (ReferenceEquals(openObject, obj))
                        {
                            Warn("cyclic reference, exported as null");
                            _json.Null();
                            return;
                        }
                    }
                    _openObjects.Add(obj);
                }

                // Same courtesy Unity extends: let the object bring its serialized fields up to date
                if (obj is ISerializationCallbackReceiver callbackReceiver) callbackReceiver.OnBeforeSerialize();

                _json.BeginObject();

                if (typed)
                {
                    _json.Name(typeKey);
                    _json.Value(type.FullName);
                }

                foreach (var field in GetSerializedFields(type))
                {
                    WriteField(obj, field);
                }

                _json.EndObject();

                if (trackCycles) _openObjects.RemoveAt(_openObjects.Count - 1);
            }

            void WriteField(object owner, FieldInfo field)
            {
                bool isManagedReference = field.IsDefined(typeof(SerializeReference), false);

                int pathLength = _path.Length;
                _path.Append('.').Append(field.Name);

                if ((isManagedReference) || (IsSerializableType(field.FieldType)))
                {
                    _json.Name(field.Name);
                    WriteValue(field.GetValue(owner), field.FieldType, isManagedReference);
                }
                else if (IsInNamespace(field.FieldType, "UnityEngine"))
                {
                    // Unity serializes plenty of its own types natively, and those need an entry in builtinTypes.
                    // Everything else that ends up here is something Unity doesn't serialize either, so no warning.
                    Warn($"{field.FieldType.FullName} is not supported, field skipped");
                }

                _path.Length = pathLength;
            }

            void WriteValue(object value, Type type, bool isManagedReference)
            {
                if (value == null)
                {
                    _json.Null();
                }
                else if (TryGetElementType(type, out var elementType))
                {
                    _json.BeginArray();

                    int index = 0;
                    foreach (var element in (IList)value)
                    {
                        int pathLength = _path.Length;
                        _path.Append('[').Append(index++).Append(']');

                        WriteValue(element, elementType, isManagedReference);

                        _path.Length = pathLength;
                    }

                    _json.EndArray();
                }
                else if (isManagedReference)
                {
                    WriteManagedReference(value);
                }
                else
                {
                    WritePlainValue(value, type);
                }
            }

            // What a [SerializeReference] points to: polymorphic, so it's only exported if it opted in
            void WriteManagedReference(object value)
            {
                if (value is IGenericSerialization)
                {
                    WriteObject(value, value.GetType(), typed: true);
                }
                else
                {
                    Warn($"{value.GetType().FullName} doesn't implement {nameof(IGenericSerialization)}, exported as null");
                    _json.Null();
                }
            }

            void WritePlainValue(object value, Type type)
            {
                switch (value)
                {
                    case UnityEngine.Object unityObject:
                        WriteAssetReference(unityObject);
                        break;
                    case string text:
                        _json.Value(text);
                        break;
                    case char character:
                        _json.Value(character.ToString());
                        break;
                    case bool boolean:
                        _json.Value(boolean);
                        break;
                    case Enum enumValue:
                        // By name, which survives reordering the enum (and is something a person can write)
                        _json.Value(enumValue.ToString());
                        break;
                    case float single:
                        _json.Value(single);
                        break;
                    case double number:
                        _json.Value(number);
                        break;
                    case ulong unsignedLong:
                        _json.Value(unsignedLong);
                        break;
                    case sbyte or byte or short or ushort or int or uint or long:
                        _json.Value(Convert.ToInt64(value));
                        break;
                    case LayerMask layerMask:
                        _json.Value((long)layerMask.value);
                        break;
                    default:
                        if (builtinTypes.TryGetValue(value.GetType(), out var builtinType))
                        {
                            // Structs go on a single line
                            WriteMembers(builtinType.getMembers(value), inline: value.GetType().IsValueType);
                        }
                        else
                        {
                            // A plain [Serializable] class or struct: Unity goes by the type of the field
                            // and not of the instance, so does this.
                            WriteObject(value, type, typed: false);
                        }
                        break;
                }
            }

            void WriteMembers((string name, object value)[] members, bool inline)
            {
                _json.BeginObject(inline);

                foreach (var (name, value) in members)
                {
                    _json.Name(name);
                    WriteValue(value, value?.GetType(), isManagedReference: false);
                }

                _json.EndObject();
            }

            void WriteAssetReference(UnityEngine.Object obj)
            {
                // Unassigned and missing references can be "fake null" objects, which only Unity's == knows about
                if (obj == null)
                {
                    _json.Null();
                    return;
                }

                string assetReference = GetAssetReference(obj);
                if (assetReference == null)
                {
                    Warn($"'{obj.name}' ({obj.GetType().Name}) can't be resolved to an asset path, exported as null");
                    _json.Null();
                }
                else
                {
                    _json.Value(assetReference);
                }
            }

            void Warn(string message)
            {
                DebugHelpers.LogWarning($"[GenericSerialization] {_path}: {message}", _context);
            }
        }

        // A single import, the Exporter in reverse: walks the parsed JSON and builds the objects it describes.
        sealed class Importer
        {
            readonly AssetResolver  _resolveAsset;
            readonly string         _source;
            readonly StringBuilder  _path = new();

            public Importer(AssetResolver resolveAsset, string source)
            {
                _resolveAsset = resolveAsset;
                _source = source;
            }

            public Document ReadDocument(object data)
            {
                if (!(data is Dictionary<string, object> members))
                {
                    Error($"expected an object at the top level, found {Describe(data)}");
                    return null;
                }

                Type type = ReadType(members, typeof(object), out string problem);
                if (type == null)
                {
                    Error(problem);
                    return null;
                }

                return new Document(members, type, _source);
            }

            public object CreateRoot(Type type)
            {
                _path.Append(type.Name);

                // Of all the Unity objects, ScriptableObjects are the only ones that can be made out of nothing
                if (typeof(ScriptableObject).IsAssignableFrom(type)) return ScriptableObject.CreateInstance(type);

                if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    Error($"can't create a {type.FullName}, the only Unity objects that can be deserialized are ScriptableObjects");
                    return null;
                }

                return CreateInstance(type);
            }

            public void PopulateRoot(object root, Type type, Dictionary<string, object> members)
            {
                _path.Append(type.Name);

                if (!type.IsInstanceOfType(root))
                {
                    Error($"this is the data of a {type.FullName}, it can't go in a {((root != null) ? root.GetType().FullName : "null")}");
                    return;
                }

                ReadFields(root, type, members);
            }

            // For the builtin types, which ask for their members one at a time
            public bool TryReadMember(string name, object data, Type type, out object value)
            {
                int pathLength = _path.Length;
                _path.Append('.').Append(name);

                bool success = TryReadValue(data, type, false, null, out value);

                _path.Length = pathLength;

                return success;
            }

            public void Error(string message)
            {
                DebugHelpers.LogError(Format(message));
            }

            // What a typed object says it is, as long as that's something it is allowed to be
            Type ReadType(Dictionary<string, object> members, Type expectedType, out string problem)
            {
                problem = null;

                if (!members.TryGetValue(typeKey, out var typeName))
                {
                    problem = $"there's no \"{typeKey}\" to say what this is";
                }
                else if (!(typeName is string fullName))
                {
                    problem = $"\"{typeKey}\" has to be the name of a type, found {Describe(typeName)}";
                }
                else
                {
                    Type type = FindType(fullName);

                    if (type == null) problem = $"unknown type {fullName}";
                    else if (!typeof(IGenericSerialization).IsAssignableFrom(type)) problem = $"{fullName} doesn't implement {nameof(IGenericSerialization)}";
                    else if (!expectedType.IsAssignableFrom(type)) problem = $"{fullName} is not a {expectedType.FullName}";
                    else if ((type.IsAbstract) || (type.ContainsGenericParameters)) problem = $"{fullName} can't be instantiated";
                    else return type;
                }

                return null;
            }

            // Same deal as with Unity: a parameterless constructor is used if there is one (public or
            // not), and the object comes out blank if there isn't.
            object CreateInstance(Type type)
            {
                try
                {
                    return Activator.CreateInstance(type, true);
                }
                catch (MissingMethodException)
                {
                    return FormatterServices.GetUninitializedObject(type);
                }
                catch (TargetInvocationException e)
                {
                    Warn($"couldn't create a {type.FullName}: {e.InnerException?.Message}");
                    return null;
                }
            }

            void ReadFields(object target, Type type, Dictionary<string, object> members)
            {
                var fields = GetSerializedFields(type);

                int usedMembers = members.ContainsKey(typeKey) ? 1 : 0;
                foreach (var field in fields)
                {
                    // Not in the JSON: stays as the object was constructed
                    if (!members.TryGetValue(field.Name, out var data)) continue;
                    usedMembers++;

                    int pathLength = _path.Length;
                    _path.Append('.').Append(field.Name);

                    bool isManagedReference = field.IsDefined(typeof(SerializeReference), false);
                    if ((!isManagedReference) && (!IsSerializableType(field.FieldType)))
                    {
                        Warn($"{field.FieldType.FullName} is not supported, field skipped");
                    }
                    else if (TryReadValue(data, field.FieldType, isManagedReference, field.GetValue(target), out var value))
                    {
                        field.SetValue(target, value);
                    }

                    _path.Length = pathLength;
                }

                // Whatever is left has no field to go to: a typo, more often than not, and those are hard to find without a hint
                if (usedMembers < members.Count)
                {
                    foreach (var name in members.Keys)
                    {
                        if ((name != typeKey) && (!fields.Exists(field => field.Name == name))) Warn($"{type.FullName} has no \"{name}\" field, ignored");
                    }
                }

                // The object's turn to catch up with its data, same as when Unity is the one loading it
                if (target is ISerializationCallbackReceiver callbackReceiver) callbackReceiver.OnAfterDeserialize();
            }

            // False if the data was of no use (and a warning says why): whoever asked keeps what it had
            bool TryReadValue(object data, Type type, bool isManagedReference, object currentValue, out object value)
            {
                value = null;

                if (data == null)
                {
                    if (!type.IsValueType) return true;

                    Warn($"null is not a valid {type.Name}");
                    return false;
                }

                if (TryGetElementType(type, out var elementType)) return TryReadList(data, type, elementType, isManagedReference, out value);
                if (isManagedReference) return TryReadManagedReference(data, type, out value);

                return TryReadPlainValue(data, type, currentValue, out value);
            }

            bool TryReadList(object data, Type type, Type elementType, bool isManagedReference, out object value)
            {
                value = null;

                if (!TryCast(data, "an array", out List<object> elements)) return false;

                IList list = type.IsArray ? Array.CreateInstance(elementType, elements.Count) : (IList)Activator.CreateInstance(type);

                for (int i = 0; i < elements.Count; i++)
                {
                    int pathLength = _path.Length;
                    _path.Append('[').Append(i).Append(']');

                    // An element that can't be read still takes its place, or everything after it changes index
                    if (!TryReadValue(elements[i], elementType, isManagedReference, null, out var element))
                    {
                        element = elementType.IsValueType ? Activator.CreateInstance(elementType) : null;
                    }

                    if (type.IsArray) list[i] = element;
                    else list.Add(element);

                    _path.Length = pathLength;
                }

                value = list;

                return true;
            }

            // What a [SerializeReference] points to: "$type" decides what gets created
            bool TryReadManagedReference(object data, Type declaredType, out object value)
            {
                value = null;

                if (!TryCast(data, "an object", out Dictionary<string, object> members)) return false;

                Type type = ReadType(members, declaredType, out string problem);
                if (type == null)
                {
                    Warn(problem);
                    return false;
                }
                if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    Warn($"{type.FullName} is a Unity object, those can only be referenced as assets");
                    return false;
                }

                value = CreateInstance(type);
                if (value == null) return false;

                ReadFields(value, type, members);

                return true;
            }

            bool TryReadPlainValue(object data, Type type, object currentValue, out object value)
            {
                value = null;

                if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return TryReadAssetReference(data, type, out value);
                if (type.IsEnum) return TryReadEnum(data, type, out value);

                if (type == typeof(string))
                {
                    if (!TryCast(data, "a string", out string text)) return false;

                    value = text;
                    return true;
                }
                if (type == typeof(char))
                {
                    if (!TryCast(data, "a string", out string text)) return false;
                    if (text.Length != 1)
                    {
                        Warn($"\"{text}\" is not a single character");
                        return false;
                    }

                    value = text[0];
                    return true;
                }
                if (type == typeof(bool))
                {
                    if (!TryCast(data, "true or false", out bool boolean)) return false;

                    value = boolean;
                    return true;
                }
                if (type.IsPrimitive) return TryReadNumber(data, type, out value);
                if (type == typeof(LayerMask))
                {
                    if (!TryReadNumber(data, typeof(int), out var mask)) return false;

                    value = (LayerMask)(int)mask;
                    return true;
                }

                // All that's left are the types that are objects in the JSON as well
                if (!TryCast(data, "an object", out Dictionary<string, object> members)) return false;

                if (builtinTypes.TryGetValue(type, out var builtinType))
                {
                    value = builtinType.create(new MemberReader(this, members));
                    return true;
                }

                // A plain [Serializable] class or struct. If there's one in place already, that's the one
                // that gets filled in, so that what the JSON leaves out keeps the value its owner gave it.
                value = ((currentValue != null) && (currentValue.GetType() == type)) ? currentValue : CreateInstance(type);
                if (value == null) return false;

                ReadFields(value, type, members);

                return true;
            }

            bool TryReadAssetReference(object data, Type type, out object value)
            {
                value = null;

                if (!TryCast(data, "an asset path", out string reference)) return false;

                var asset = _resolveAsset(reference, type);
                if ((asset == null) || (!type.IsInstanceOfType(asset)))
                {
                    Warn($"there's no {type.Name} at \"{reference}\"");
                    return false;
                }

                value = asset;

                return true;
            }

            // Written by name, but a number will do as well
            bool TryReadEnum(object data, Type type, out object value)
            {
                value = null;

                if (data is JsonNumber)
                {
                    if (!TryReadNumber(data, Enum.GetUnderlyingType(type), out var number)) return false;

                    value = Enum.ToObject(type, number);
                    return true;
                }

                if (!TryCast(data, $"the name of a {type.Name}", out string name)) return false;

                try
                {
                    value = Enum.Parse(type, name);
                    return true;
                }
                catch (Exception e) when ((e is ArgumentException) || (e is OverflowException))
                {
                    Warn($"\"{name}\" is not a valid {type.Name}");
                    return false;
                }
            }

            bool TryReadNumber(object data, Type type, out object value)
            {
                value = null;

                bool isFloatingPoint = (type == typeof(float)) || (type == typeof(double));

                // NaN and the infinities are written as strings
                string text = (data as JsonNumber)?.text;
                if ((text == null) && (isFloatingPoint)) text = data as string;
                if (text == null)
                {
                    Warn($"expected a number, found {Describe(data)}");
                    return false;
                }

                bool valid;
                if (type == typeof(float))
                {
                    valid = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number);
                    value = number;
                }
                else if (type == typeof(double))
                {
                    valid = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number);
                    value = number;
                }
                else if (type == typeof(ulong))
                {
                    valid = ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong number);
                    value = number;
                }
                else
                {
                    valid = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number);
                    if (valid)
                    {
                        // Parsed as the widest there is, which may be more than the field can take
                        try
                        {
                            value = Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
                        }
                        catch (OverflowException)
                        {
                            valid = false;
                        }
                    }
                }

                if (!valid) Warn($"{text} is not a valid {type.Name}");

                return valid;
            }

            bool TryCast<T>(object data, string expected, out T value)
            {
                if (data is T typedData)
                {
                    value = typedData;
                    return true;
                }

                Warn($"expected {expected}, found {Describe(data)}");
                value = default;

                return false;
            }

            static string Describe(object data)
            {
                switch (data)
                {
                    case null: return "null";
                    case string: return "a string";
                    case bool: return "a boolean";
                    case JsonNumber: return "a number";
                    case List<object>: return "an array";
                    default: return "an object";
                }
            }

            void Warn(string message)
            {
                DebugHelpers.LogWarning(Format(message));
            }

            // "[GenericSerialization] <source>: <where in the object>: <message>", minus the parts there's nothing to say about
            string Format(string message)
            {
                var text = new StringBuilder("[GenericSerialization] ");

                if (!string.IsNullOrEmpty(_source)) text.Append(_source).Append(": ");
                if (_path.Length > 0) text.Append(_path).Append(": ");

                return text.Append(message).ToString();
            }
        }

        // The members of a builtin type on their way back in, each one read as whatever it's asked for as
        readonly struct MemberReader
        {
            readonly Importer                   _importer;
            readonly Dictionary<string, object> _members;

            public MemberReader(Importer importer, Dictionary<string, object> members)
            {
                _importer = importer;
                _members = members;
            }

            public T Get<T>(string name, T fallback = default)
            {
                if (!_members.TryGetValue(name, out var data)) return fallback;
                if (!_importer.TryReadMember(name, data, typeof(T), out var value)) return fallback;

                return (T)value;
            }
        }

        // Just enough JSON: one element per line, except inside inline objects, which stay on a single
        // line (vectors, colors and the like read a lot better that way).
        sealed class JsonWriter
        {
            const string indent = "    ";

            readonly StringBuilder  _text = new();
            readonly Stack<bool>    _hasElements = new();   // One per open object/array
            int                     _inlineDepth;           // How many of those are inline
            bool                    _afterName;

            public override string ToString() => _text.ToString();

            public void BeginObject(bool inline = false) => Begin('{', inline);
            public void EndObject() => End('}');
            public void BeginArray() => Begin('[', false);
            public void EndArray() => End(']');

            public void Name(string name)
            {
                Separate();
                AppendString(name);
                _text.Append(": ");
                _afterName = true;
            }

            public void Null()
            {
                Separate();
                _text.Append("null");
            }

            public void Value(string value)
            {
                Separate();
                AppendString(value);
            }

            public void Value(bool value)
            {
                Separate();
                _text.Append(value ? "true" : "false");
            }

            public void Value(long value)
            {
                Separate();
                _text.Append(value.ToString(CultureInfo.InvariantCulture));
            }

            public void Value(ulong value)
            {
                Separate();
                _text.Append(value.ToString(CultureInfo.InvariantCulture));
            }

            // Numbers are written with as few digits as it takes to read back the exact same value ("R"
            // is supposed to do just that, but Mono's overshoots: 0.18316127 comes out as 0.183161274).
            // NaN and the infinities aren't numbers as far as JSON is concerned, so those go out as strings.
            public void Value(float value)
            {
                if ((float.IsNaN(value)) || (float.IsInfinity(value)))
                {
                    Value(value.ToString(CultureInfo.InvariantCulture));
                    return;
                }

                string number = null;
                for (int digits = 7; digits <= 9; digits++)
                {
                    number = value.ToString("G" + digits, CultureInfo.InvariantCulture);
                    if (float.Parse(number, CultureInfo.InvariantCulture) == value) break;
                }

                Separate();
                _text.Append(number);
            }

            public void Value(double value)
            {
                if ((double.IsNaN(value)) || (double.IsInfinity(value)))
                {
                    Value(value.ToString(CultureInfo.InvariantCulture));
                    return;
                }

                string number = null;
                for (int digits = 15; digits <= 17; digits++)
                {
                    number = value.ToString("G" + digits, CultureInfo.InvariantCulture);
                    if (double.Parse(number, CultureInfo.InvariantCulture) == value) break;
                }

                Separate();
                _text.Append(number);
            }

            void Begin(char bracket, bool inline)
            {
                Separate();
                _text.Append(bracket);
                _hasElements.Push(false);

                // Whatever is inside an inline object is inline as well
                if ((inline) || (_inlineDepth > 0)) _inlineDepth++;
            }

            void End(char bracket)
            {
                bool hadElements = _hasElements.Pop();
                bool inline = _inlineDepth > 0;
                if (inline) _inlineDepth--;

                if (hadElements)
                {
                    if (inline) _text.Append(' ');
                    else NewLine();
                }
                _text.Append(bracket);
            }

            // What goes between the previous token and the element about to be written
            void Separate()
            {
                // The value of a member stays next to its name
                if (_afterName)
                {
                    _afterName = false;
                    return;
                }

                // Root value
                if (_hasElements.Count == 0) return;

                if (_hasElements.Pop()) _text.Append(',');
                _hasElements.Push(true);

                if (_inlineDepth > 0) _text.Append(' ');
                else NewLine();
            }

            void NewLine()
            {
                _text.Append('\n');
                for (int i = 0; i < _hasElements.Count; i++) _text.Append(indent);
            }

            void AppendString(string value)
            {
                _text.Append('"');

                foreach (char c in value)
                {
                    switch (c)
                    {
                        case '"': _text.Append("\\\""); break;
                        case '\\': _text.Append("\\\\"); break;
                        case '\n': _text.Append("\\n"); break;
                        case '\r': _text.Append("\\r"); break;
                        case '\t': _text.Append("\\t"); break;
                        default:
                            // The rest of the control characters have no short form
                            if (c < ' ') _text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else _text.Append(c);
                            break;
                    }
                }

                _text.Append('"');
            }
        }

        // A number the way it was written: what it turns into is up to the field it's for
        sealed class JsonNumber
        {
            public readonly string text;

            public JsonNumber(string text)
            {
                this.text = text;
            }
        }

        // Just enough JSON, the other way around. Objects come out as Dictionary<string, object>, arrays
        // as List<object>, and everything else as a string, a bool, a JsonNumber or null. The first
        // thing that's wrong gets a FormatException, which says where it is.
        sealed class JsonParser
        {
            // More than anything real needs, and nowhere near what it takes to blow the stack
            const int maxDepth = 128;

            readonly string _text;
            int             _position;
            int             _depth;

            JsonParser(string text)
            {
                _text = text;
            }

            public static object Parse(string text)
            {
                var parser = new JsonParser(text);

                object value = parser.ParseValue();

                parser.SkipWhitespace();
                if (parser._position < text.Length) throw parser.Error("there's more after the end of the data");

                return value;
            }

            object ParseValue()
            {
                SkipWhitespace();
                if (_position >= _text.Length) throw Error("unexpected end of the data");

                char c = _text[_position];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': ParseLiteral("true"); return true;
                    case 'f': ParseLiteral("false"); return false;
                    case 'n': ParseLiteral("null"); return null;
                    default:
                        if ((c == '-') || ((c >= '0') && (c <= '9'))) return ParseNumber();
                        throw Error($"unexpected '{c}'");
                }
            }

            Dictionary<string, object> ParseObject()
            {
                var members = new Dictionary<string, object>();

                Open();
                if (!TryConsume('}'))
                {
                    do
                    {
                        SkipWhitespace();
                        if ((_position >= _text.Length) || (_text[_position] != '"')) throw Error("expected the name of a member");

                        string name = ParseString();
                        Expect(':');
                        members[name] = ParseValue();
                    }
                    while (TryConsume(','));

                    Expect('}');
                }
                _depth--;

                return members;
            }

            List<object> ParseArray()
            {
                var elements = new List<object>();

                Open();
                if (!TryConsume(']'))
                {
                    do
                    {
                        elements.Add(ParseValue());
                    }
                    while (TryConsume(','));

                    Expect(']');
                }
                _depth--;

                return elements;
            }

            string ParseString()
            {
                var value = new StringBuilder();

                // Opening quote
                _position++;

                while (true)
                {
                    // Strings don't span lines, and stopping at the end of this one puts the error where the quote is missing
                    if ((_position >= _text.Length) || (_text[_position] == '\n')) throw Error("unterminated string");

                    char c = _text[_position++];
                    if (c == '"') break;
                    if (c != '\\')
                    {
                        value.Append(c);
                        continue;
                    }

                    if (_position >= _text.Length) throw Error("unterminated string");

                    switch (_text[_position])
                    {
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        case '/': value.Append('/'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'u':
                            if ((_position + 4 >= _text.Length) || (!ushort.TryParse(_text.Substring(_position + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code)))
                            {
                                throw Error("\\u has to be followed by 4 hexadecimal digits");
                            }
                            value.Append((char)code);
                            _position += 4;
                            break;
                        default:
                            throw Error($"unknown escape sequence '\\{_text[_position]}'");
                    }
                    _position++;
                }

                return value.ToString();
            }

            // Only where it ends is decided here, whether it makes sense as a number is found out when it's read as one
            JsonNumber ParseNumber()
            {
                int start = _position;
                while ((_position < _text.Length) && ("+-.0123456789eE".IndexOf(_text[_position]) >= 0)) _position++;

                return new JsonNumber(_text.Substring(start, _position - start));
            }

            void ParseLiteral(string literal)
            {
                if (string.CompareOrdinal(_text, _position, literal, 0, literal.Length) != 0) throw Error($"unexpected '{_text[_position]}'");

                _position += literal.Length;
            }

            void Open()
            {
                _position++;
                if (++_depth > maxDepth) throw Error("too many levels of nesting");
            }

            bool TryConsume(char c)
            {
                SkipWhitespace();
                if ((_position >= _text.Length) || (_text[_position] != c)) return false;

                _position++;

                return true;
            }

            void Expect(char c)
            {
                if (!TryConsume(c)) throw Error($"expected '{c}'");
            }

            void SkipWhitespace()
            {
                while ((_position < _text.Length) && (char.IsWhiteSpace(_text[_position]))) _position++;
            }

            // Thrown with the parser sitting on whatever it didn't like
            FormatException Error(string message)
            {
                int line = 1;
                int lineStart = 0;
                for (int i = 0; (i < _position) && (i < _text.Length); i++)
                {
                    if (_text[i] == '\n')
                    {
                        line++;
                        lineStart = i + 1;
                    }
                }

                return new FormatException($"{message} (line {line}, column {_position - lineStart + 1})");
            }
        }
    }
}
