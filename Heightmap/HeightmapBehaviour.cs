using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HeightmapBehaviour : MonoBehaviour
{
    public enum Method { Perlin, CelularAutomata };
    public enum Target { Texture };

    public Vector2Int size = new Vector2Int(256, 256);
    public Method method;
    [ShowIf("isPerlin")]
    public Vector2 offset;
    [ShowIf("isPerlin")]
    public Vector2 frequency = new Vector2(1.0f, 1.0f);
    [ShowIf("isPerlin")]
    public float amplitude = 1.0f;
    [ShowIf("needsRandom")]
    public int seed = 0;
    [ShowIf("isCelularAutomata")]
    public int steps = 1000;
    [ShowIf("isCelularAutomata"), Range(0.0f, 1.0f)]
    public float onProbability = 0.5f;
    [ShowIf("isCelularAutomata")]
    public Vector2Int starvingParams = new Vector2Int(2, 3);
    [ShowIf("isCelularAutomata")]
    public Vector2Int birthParams = new Vector2Int(3, 3);
    public Target target;
    [ShowIf("isTextureTarget")]
    public Texture2D targetTexture;

    bool isTextureTarget() { return target == Target.Texture; }
    bool isPerlin() { return method == Method.Perlin; }
    bool isCelularAutomata() { return method == Method.CelularAutomata; }
    bool needsRandom() { return method == Method.CelularAutomata; }

    Heightmap heightMap;

    [Button("Generate Base")]
    public void GenerateHeightmap()
    {
        heightMap = new Heightmap(size.x, size.y);

        switch (method)
        {
            case Method.Perlin:
                heightMap.PerlinNoise(frequency, offset, amplitude);
                break;
            case Method.CelularAutomata:
                Random.InitState(seed);
                heightMap.CelularAutomata(steps, onProbability, starvingParams, birthParams);
                break;
        }

        SaveHeightmap();
    }

    void SaveHeightmap()
    { 
        switch (target)
        {
            case Target.Texture:
                ReplaceTexture(heightMap, targetTexture);
                break;
        }
    }

    void ReplaceTexture(Heightmap heightmap, Texture2D texture)
    {
#if UNITY_EDITOR
        string filename = "Assets/Textures/heightmap.png";

        if (texture != null)
        {
            filename = AssetDatabase.GetAssetPath(texture);
        }

        heightmap.SaveTexture(filename);

        if (texture == null)
        {
            targetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(filename);
        }

        AssetDatabase.Refresh();
#endif
    }

    [Button("Run Celular Automata Step")]
    public void RunStep()
    {
        heightMap.CelularAutomata_RunStep(null, null, starvingParams.x, starvingParams.y, birthParams.x, birthParams.y);

        SaveHeightmap();
    }
}
