using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UC
{

    [CreateAssetMenu(fileName = "BuildDefs", menuName = "Unity Common/BuildDefs")]
    public class BuildDefs : ScriptableObject
    {
        public string       version = "1.0.0";
        public string       username = "defaultuser";
        public string       projectName = "";
        public bool         buildWindows = true;
        public bool         buildWeb;
        public bool         createZipFiles = true;
        public bool         uploadToItch = true;
        [Header("Graphics Settings")]
        public bool                 overrideGraphicsSettings;
        public RenderPipelineAsset  windowsRenderPipelineAsset;
        public RenderPipelineAsset  webGLRenderPipelineAsset;

        public List<string> ignoreFilePatterns = new List<string>();

        public bool anyBuilds => buildWindows | buildWeb;
    }
}