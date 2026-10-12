using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UC
{
    // Keeps the RuntimeAssetDatabase from going stale without anybody having to remember to scan: it
    // does it on the way into play mode, and before every build.
    [InitializeOnLoad]
    public class RuntimeAssetDatabaseUpdater : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        static RuntimeAssetDatabaseUpdater()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            Update();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Update();
        }

        public static void Update()
        {
            // Loaded directly instead of through the instance: a project that doesn't use any of this
            // has no database, and that's not something to complain about
            var database = Resources.Load<RuntimeAssetDatabase>(nameof(RuntimeAssetDatabase));
            if ((database == null) || (!database.scanAutomatically)) return;

            if (RuntimeAssetDatabaseEditor.Scan(database))
            {
                DebugHelpers.Log($"{database.name} updated, {RuntimeAssetDatabaseEditor.GetSummary(database)}", database);
            }
        }
    }
}
