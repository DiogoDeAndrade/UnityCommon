using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace UC
{
    public class ScreenshotRuntimeRunner : MonoBehaviour
    {
        static ScreenshotRuntimeRunner _instance;

        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = new GameObject("[UnityCommon] ScreenshotRuntimeRunner");
            _instance = go.AddComponent<ScreenshotRuntimeRunner>();
        }

        public static void RequestGameViewCapture(int width, int height, string path, string ext, bool exportAlpha)
        {
            EnsureExists();
            _instance.StartCoroutine(_instance.CaptureRoutine(width, height, path, ext, exportAlpha));
        }

        IEnumerator CaptureRoutine(int width, int height, string path, string ext, bool exportAlpha)
        {
            // Ensure we capture the final composited frame (incl. overlay canvas)
            yield return new WaitForEndOfFrame();

            Texture2D captured = null;
            RenderTexture rt = null;
            Texture2D outTex = null;

            try
            {
                captured = ScreenCapture.CaptureScreenshotAsTexture();

                // IMPORTANT: If you're upscaling (requested > captured), it WILL be blurry.
                // For best quality, make your Game view resolution at least as large as requested.
                captured.filterMode = FilterMode.Point; // avoids extra blur when scaling

                rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Point
                };

                var prev = RenderTexture.active;
                RenderTexture.active = rt;

                // Point-style blit: still uses bilinear by default in some paths, but Point filters help.
                Graphics.Blit(captured, rt);

                var outFormat = (ext == ".exr") ? TextureFormat.RGBAFloat : TextureFormat.RGBA32;
                outTex = new Texture2D(width, height, outFormat, mipChain: false, linear: true);
                outTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                outTex.Apply(false, false);

                RenderTexture.active = prev;

                if (!exportAlpha)
                    ForceOpaqueAlpha(outTex);

                WriteTextureToFile(outTex, path, ext);
            }
            finally
            {
                if (captured != null) Destroy(captured);
                if (rt != null) Destroy(rt);
                if (outTex != null) Destroy(outTex);

                Destroy(gameObject);         
            }
        }

        static void ForceOpaqueAlpha(Texture2D tex)
        {
            var pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i].a = 1f;
            tex.SetPixels(pixels);
            tex.Apply(false, false);
        }

        static void WriteTextureToFile(Texture2D tex, string path, string ext)
        {
            byte[] bytes = (ext == ".exr")
                ? tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat)
                : tex.EncodeToPNG();

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }
    }
}
