using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace UC
{
    // Stands in for the AssetDatabase where there isn't one: finds assets by the references that
    // GenericSerializer writes ("<asset path>", "<asset path>:<name>"), or just by their name.
    //
    //  - The entries are baked in the editor, for the types of asset that are ticked: when entering
    //    play mode and before a build, or with "Scan project" in the inspector. Each one is a lazy
    //    reference, so being listed here doesn't get an asset loaded - that only happens when it's
    //    asked for. It does get it into the build, though, whether anything else uses it or not.
    //  - Objects that only come to exist while the game runs can be registered by name. Those are
    //    looked at first, so one of them takes the place of the baked asset with the same name for
    //    whoever looks that asset up, by name or by path.
    //  - The .data files of a folder (what a mod is made of, for one) can be registered just like
    //    that, see LoadDataFolder. One that has the name of a baked ScriptableObject is not a new
    //    object, though: its data goes into that asset, so that everything that points at the asset
    //    sees it, and not just who looks it up.
    //
    // There's only one of these: it has to be called RuntimeAssetDatabase, and be in a Resources folder.
    [CreateAssetMenu(fileName = "RuntimeAssetDatabase", menuName = "Unity Common/Data/Runtime Asset Database")]
    public partial class RuntimeAssetDatabase : ScriptableObject
    {
        [Flags]
        public enum AssetTypes
        {
            ScriptableObject    = 1 << 0,
            Sprite              = 1 << 1,
            Texture             = 1 << 2,
            Material            = 1 << 3,
            Prefab              = 1 << 4,
            Mesh                = 1 << 5,
            AnimationClip       = 1 << 6,
            AnimatorController  = 1 << 7,
            AudioClip           = 1 << 8,
            Font                = 1 << 9,
            TextAsset           = 1 << 10,
            Shader              = 1 << 11,
        }

        [Serializable]
        public class Entry
        {
            // What GenericSerializer.GetAssetReference gives for the asset
            public string                                   reference;
            public string                                   name;
            // Full name of the asset's type, to tell what an entry can be used as without loading it
            public string                                   type;
            public LazyLoadReference<UnityEngine.Object>    asset;
        }

        const string resourceName = "RuntimeAssetDatabase";

        [SerializeField, Tooltip("The types of asset that 'Scan project' collects.")]
        private AssetTypes      _assetTypes = AssetTypes.ScriptableObject | AssetTypes.Sprite;
        [SerializeField, Tooltip("Where 'Scan project' looks, subfolders included. With nothing here it looks everywhere, packages included.")]
        private List<string>    _searchFolders = new() { "Assets" };
        [SerializeField, Tooltip("Scan the project without being asked to, when entering play mode and before a build. It takes longer the more assets there are of the types above.")]
        private bool            _scanAutomatically = true;
        [SerializeField, HideInInspector]
        private List<Entry>     _entries = new();

        // Built the first time something is looked up
        [NonSerialized]
        private Dictionary<string, List<Entry>> _entriesByPath;
        [NonSerialized]
        private Dictionary<string, List<Entry>> _entriesByName;
        [NonSerialized]
        private Dictionary<string, Type>        _entryTypes;

        [NoAutoStaticsCleanup]
        private static RuntimeAssetDatabase _instance;

        // What was registered while running, by name; the latest comes last
        [AutoStaticsCleanup]
        private static readonly Dictionary<string, List<UnityEngine.Object>> registeredAssets = new();
        // The data that objects made out of .data files are still waiting for: it only goes in when
        // somebody asks for the object
        [AutoStaticsCleanup]
        private static readonly Dictionary<UnityEngine.Object, GenericSerializer.Document> pendingData = new();
        // The baked assets that data was put into. Being here keeps them from getting unloaded when
        // nothing else is holding on to them, which would bring them back the way they were.
        [AutoStaticsCleanup]
        private static readonly List<UnityEngine.Object> overriddenAssets = new();
        [AutoStaticsCleanup]
        private static bool missingReported;

        private static readonly (AssetTypes flag, Type type)[] typesByFlag =
        {
            (AssetTypes.ScriptableObject,   typeof(ScriptableObject)),
            (AssetTypes.Sprite,             typeof(Sprite)),
            (AssetTypes.Texture,            typeof(Texture)),
            (AssetTypes.Material,           typeof(Material)),
            (AssetTypes.Prefab,             typeof(GameObject)),
            (AssetTypes.Mesh,               typeof(Mesh)),
            (AssetTypes.AnimationClip,      typeof(AnimationClip)),
            (AssetTypes.AnimatorController, typeof(RuntimeAnimatorController)),
            (AssetTypes.AudioClip,          typeof(AudioClip)),
            (AssetTypes.Font,               typeof(Font)),
            (AssetTypes.TextAsset,          typeof(TextAsset)),
            (AssetTypes.Shader,             typeof(Shader)),
        };

        public AssetTypes               assetTypes => _assetTypes;
        public IReadOnlyList<string>    searchFolders => _searchFolders;
        public bool                     scanAutomatically => _scanAutomatically;
        public IReadOnlyList<Entry>     entries => _entries;

        public static RuntimeAssetDatabase instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<RuntimeAssetDatabase>(resourceName);
                    if ((_instance == null) && (!missingReported))
                    {
                        missingReported = true;
                        DebugHelpers.LogError($"{resourceName} not found, only what gets registered at runtime can be referenced. Make sure it is located in a 'Resources' folder and named '{resourceName}'.");
                    }
                }
                return _instance;
            }
        }

        // The asset behind a reference, as something of the given type; null if there's no such thing.
        // This is what GenericSerializer resolves references with outside of the editor.
        public static UnityEngine.Object Find(string reference, Type type)
        {
            if (string.IsNullOrEmpty(reference)) return null;

            // Whatever was registered at runtime only goes by its name
            var asset = FindRegistered(reference, type);
            if (asset != null) return asset;

            var database = instance;

            return (database != null) ? database.FindEntry(reference, type) : null;
        }

        public static T Find<T>(string reference) where T : UnityEngine.Object
        {
            return Find(reference, typeof(T)) as T;
        }

        // Makes an object something that can be referenced by name. Another one of the same type that
        // was there under that name is replaced.
        public static void Register(string name, UnityEngine.Object asset)
        {
            if (!registeredAssets.TryGetValue(name, out var assets))
            {
                assets = new List<UnityEngine.Object>();
                registeredAssets[name] = assets;
            }

            foreach (var other in assets)
            {
                if ((other == null) || (other.GetType() == asset.GetType())) pendingData.Remove(other);
            }
            assets.RemoveAll(other => (other == null) || (other.GetType() == asset.GetType()));
            assets.Add(asset);
        }

        public static void Register(UnityEngine.Object asset)
        {
            Register(asset.name, asset);
        }

        public static void Unregister(UnityEngine.Object asset)
        {
            foreach (var assets in registeredAssets.Values)
            {
                assets.Remove(asset);
            }

            pendingData.Remove(asset);
        }

        // Registers what each .data file in a folder (and in the folders inside of it) describes, under
        // the name of the file, and says how many there were.
        //
        // A new object isn't filled in at this point: that's left for when it's first asked for, and
        // it's then that it brings in what it references, the way loading an asset would. So the order
        // of the files makes no difference, they can reference each other, and no asset gets loaded
        // on account of an object that nobody ends up using.
        //
        // A file that goes by the name of a baked asset is something else: that's an override, and
        // its data goes into the asset right here (once everything else is registered, since it may
        // reference any of it). It can't wait for somebody to ask - most of what uses that asset
        // points straight at it, and never asks anything.
        public static int LoadDataFolder(string folder)
        {
            if (!Directory.Exists(folder)) return 0;

            var files = Directory.GetFiles(folder, "*.data", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);

            var overrides = new List<(Entry entry, GenericSerializer.Document document, string source)>();

            int count = 0;
            foreach (var file in files)
            {
                string json;
                try
                {
                    json = File.ReadAllText(file);
                }
                catch (Exception e) when ((e is IOException) || (e is UnauthorizedAccessException))
                {
                    DebugHelpers.LogError($"Can't read {file}: {e.Message}");
                    continue;
                }

                if (RegisterData(Path.GetFileNameWithoutExtension(file), json, file, overrides)) count++;
            }

            foreach (var (entry, document, source) in overrides)
            {
                ApplyOverride(entry, document, source);
            }

            return count;
        }

        // The same, for a single piece of data that can come from anywhere; the source is only there
        // to say which one it was in the log. An override is applied on the spot, so what it
        // references has to be registered by then.
        public static bool RegisterData(string name, string json, string source = null)
        {
            return RegisterData(name, json, source ?? name, null);
        }

        private static bool RegisterData(string name, string json, string source, List<(Entry entry, GenericSerializer.Document document, string source)> laterOverrides)
        {
            var document = GenericSerializer.Parse(json, source);
            if (document == null) return false;

            if (!typeof(ScriptableObject).IsAssignableFrom(document.type))
            {
                DebugHelpers.LogError($"{source} describes a {document.type.FullName}, and only a ScriptableObject can be registered");
                return false;
            }

            var database = instance;
            var overriddenEntry = (database != null) ? database.FindOverridable(name, document.type, source) : null;
            if (overriddenEntry != null)
            {
                if (laterOverrides != null) laterOverrides.Add((overriddenEntry, document, source));
                else ApplyOverride(overriddenEntry, document, source);

                return true;
            }

            var asset = document.Create() as ScriptableObject;
            if (asset == null) return false;

            asset.name = name;

            Register(name, asset);
            pendingData[asset] = document;

            return true;
        }

        // Puts the data in the baked asset itself. The fields that the data doesn't mention stay as
        // the asset has them.
        private static void ApplyOverride(Entry entry, GenericSerializer.Document document, string source)
        {
            var asset = entry.asset.asset;
            if (asset == null)
            {
                DebugHelpers.LogWarning($"{source} is meant for {entry.reference}, which isn't there anymore");
                return;
            }

#if UNITY_EDITOR
            RememberOriginal(asset);
#endif
            if (!overriddenAssets.Contains(asset)) overriddenAssets.Add(asset);

            document.Populate(asset, Find);

            DebugHelpers.Log($"{source} overrides {entry.reference}");
        }

#if UNITY_EDITOR
        // In the editor, an overridden asset is the actual asset of the project, and what a mod did to
        // it has no business staying there once the game stops. So what the asset was like is kept,
        // and put back on the way out of play mode. It's kept in the session state and not in a
        // static, which is what it takes to still have it after scripts get recompiled during play.
        const string originalsKey = "UC.RuntimeAssetDatabase.originals";

        private static void RememberOriginal(UnityEngine.Object asset)
        {
            string id = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            string ids = UnityEditor.SessionState.GetString(originalsKey, "");

            // Overridden more than once, it's what it was like before the first time that counts
            if (ids.Contains(id)) return;

            UnityEditor.SessionState.SetString($"{originalsKey}.{id}", UnityEditor.EditorJsonUtility.ToJson(asset));
            UnityEditor.SessionState.SetString(originalsKey, $"{ids}{id}\n");
        }

        // Undoes every override. Leaving play mode does this on its own.
        public static void RestoreOverriddenAssets()
        {
            string ids = UnityEditor.SessionState.GetString(originalsKey, "");
            if (ids.Length == 0) return;

            foreach (var id in ids.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (UnityEditor.GlobalObjectId.TryParse(id, out var globalId))
                {
                    var asset = UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                    if (asset != null) UnityEditor.EditorJsonUtility.FromJsonOverwrite(UnityEditor.SessionState.GetString($"{originalsKey}.{id}", "{}"), asset);
                }

                UnityEditor.SessionState.EraseString($"{originalsKey}.{id}");
            }

            UnityEditor.SessionState.EraseString(originalsKey);
            overriddenAssets.Clear();
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void RestoreOverriddenAssetsAfterPlayMode()
        {
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Play mode can also be over by the time the code gets (re)loaded, with nobody having been told
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) RestoreOverriddenAssets();
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) RestoreOverriddenAssets();
        }
#endif

        // The asset types the flags stand for
        public static List<Type> GetTypes(AssetTypes assetTypes)
        {
            var types = new List<Type>();
            foreach (var (flag, type) in typesByFlag)
            {
                if ((assetTypes & flag) != 0) types.Add(type);
            }

            return types;
        }

        // All there is of a type (or of something that derives from it): the baked ones, with what was
        // registered taking the place of those it shares a name with, and then the rest of what was
        // registered. Unlike finding a single asset, this loads every one of them.
        public static List<T> GetScriptableObjects<T>() where T : ScriptableObject
        {
            var objects = new List<T>();
            var added = new HashSet<T>();

            var database = instance;
            if (database != null)
            {
                database.BuildIndex();

                foreach (var entry in database._entries)
                {
                    if (!database.IsA(entry, typeof(T))) continue;

                    var asset = Load(entry, typeof(T)) as T;
                    if ((asset != null) && (added.Add(asset))) objects.Add(asset);
                }
            }

            foreach (var assets in registeredAssets.Values)
            {
                foreach (var asset in assets)
                {
                    if ((asset is T typedAsset) && (typedAsset != null) && (added.Add(typedAsset)))
                    {
                        FillIn(typedAsset);
                        objects.Add(typedAsset);
                    }
                }
            }

            return objects;
        }

        // Replaces what's baked, which is something for the editor to do
        public void SetEntries(List<Entry> entries)
        {
            _entries = entries;
            ClearIndex();
        }

        private void OnEnable()
        {
            ClearIndex();
        }

        private void OnValidate()
        {
            ClearIndex();
        }

        private void ClearIndex()
        {
            _entriesByPath = null;
            _entriesByName = null;
            _entryTypes = null;
        }

        private void BuildIndex()
        {
            if (_entriesByPath != null) return;

            _entriesByPath = new Dictionary<string, List<Entry>>();
            _entriesByName = new Dictionary<string, List<Entry>>();
            _entryTypes = new Dictionary<string, Type>();

            foreach (var entry in _entries)
            {
                GenericSerializer.SplitAssetReference(entry.reference, out string assetPath, out _);

                AddToIndex(_entriesByPath, assetPath, entry);
                AddToIndex(_entriesByName, entry.name, entry);
            }
        }

        private static void AddToIndex(Dictionary<string, List<Entry>> index, string key, Entry entry)
        {
            if (!index.TryGetValue(key, out var entries))
            {
                entries = new List<Entry>();
                index[key] = entries;
            }

            entries.Add(entry);
        }

        private static UnityEngine.Object FindRegistered(string name, Type type)
        {
            if (!registeredAssets.TryGetValue(name, out var assets)) return null;

            for (int i = assets.Count - 1; i >= 0; i--)
            {
                if ((assets[i] != null) && (type.IsInstanceOfType(assets[i])))
                {
                    FillIn(assets[i]);
                    return assets[i];
                }
            }

            return null;
        }

        // What came out of a .data file gets its data the first time it's handed out. It stops being
        // pending before that and not after, because what it references may well lead back to it.
        private static void FillIn(UnityEngine.Object asset)
        {
            if (!pendingData.TryGetValue(asset, out var document)) return;

            pendingData.Remove(asset);
            document.Populate(asset, Find);
        }

        // The baked asset that data of this type, going by this name, is an override of. It has to be
        // an asset of that type or of one that derives from it: an object can take in the data of
        // what it is, but it can't be made into something that it isn't.
        private Entry FindOverridable(string name, Type type, string source)
        {
            BuildIndex();

            if (!_entriesByName.TryGetValue(name, out var entriesWithName)) return null;

            var candidates = entriesWithName.FindAll(entry => IsA(entry, type));
            if (candidates.Count > 1)
            {
                DebugHelpers.LogWarning($"{source}: there are {candidates.Count} {type.Name} assets called \"{name}\", and only {candidates[0].reference} is overridden");
            }
            if (candidates.Count > 0) return candidates[0];

            // Not an override, then, but still close enough to be mistaken for one
            var relatedEntry = entriesWithName.Find(entry => IsRelated(entry, type));
            if (relatedEntry != null)
            {
                DebugHelpers.LogWarning($"{source} describes a {type.FullName}, which {relatedEntry.reference} ({relatedEntry.type}) can't be turned into. It's registered as an object of its own: that's what looking up \"{name}\" gets from now on, but whatever points straight at the asset still has the asset.");
            }

            return null;
        }

        private UnityEngine.Object FindEntry(string reference, Type type)
        {
            BuildIndex();

            GenericSerializer.SplitAssetReference(reference, out string assetPath, out string name);

            if (_entriesByPath.TryGetValue(assetPath, out var entriesAtPath))
            {
                // No name means whatever fits that's there, and the asset itself is listed before what's inside of it
                var entry = entriesAtPath.Find(candidate => ((name == null) || (candidate.name == name)) && (IsA(candidate, type)));

                return (entry != null) ? Load(entry, type) : FindInPrefab(entriesAtPath, name, type);
            }

            // Not the path to anything, so it has to be a name
            if (_entriesByName.TryGetValue(reference, out var entriesWithName))
            {
                var namedEntries = entriesWithName.FindAll(candidate => IsA(candidate, type));
                if (namedEntries.Count > 1)
                {
                    DebugHelpers.LogWarning($"There are {namedEntries.Count} {type.Name} assets called \"{reference}\", using {namedEntries[0].reference}. Use the path instead of the name to get one of the others.");
                }

                return (namedEntries.Count > 0) ? Load(namedEntries[0], type) : FindInPrefab(entriesWithName, null, type);
            }

            return null;
        }

        // An asset that something was registered over (same name, and good for the same use) is no
        // longer the one to hand out, even to who asks for it by its path
        private static UnityEngine.Object Load(Entry entry, Type type)
        {
            var registered = FindRegistered(entry.name, type);

            return (registered != null) ? registered : entry.asset.asset;
        }

        // The components and the children of a prefab aren't listed, the way to them is through the prefab they're in
        private UnityEngine.Object FindInPrefab(List<Entry> candidates, string name, Type type)
        {
            bool isComponent = typeof(Component).IsAssignableFrom(type);
            if ((!isComponent) && (type != typeof(GameObject))) return null;

            foreach (var entry in candidates)
            {
                if (!IsA(entry, typeof(GameObject))) continue;

                var prefab = entry.asset.asset as GameObject;
                if (prefab == null) continue;

                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if ((name != null) && (transform.name != name)) continue;
                    if (!isComponent) return transform.gameObject;

                    var component = transform.GetComponent(type);
                    if (component != null) return component;
                }
            }

            return null;
        }

        private bool IsA(Entry entry, Type type)
        {
            if (!_entryTypes.TryGetValue(entry.type, out var entryType))
            {
                entryType = GenericSerializer.FindType(entry.type);
                _entryTypes[entry.type] = entryType;
            }

            return (entryType != null) && (type.IsAssignableFrom(entryType));
        }

        // One of them is the other, or derives from it: there are fields that would take either
        private bool IsRelated(Entry entry, Type type)
        {
            if (IsA(entry, type)) return true;

            // Looked up by IsA, if it wasn't already
            Type entryType = _entryTypes[entry.type];

            return (entryType != null) && (entryType.IsAssignableFrom(type));
        }
    }
}
