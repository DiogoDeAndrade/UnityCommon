using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CaveGenerator : MonoBehaviour
{
    public Vector2Int   size = new Vector2Int(64, 64);
    public int          seed = 0;
    public bool         debug = false;
    [ShowIf("debug")]
    public Texture2D    targetTexture;

    Heightmap   heightmap;
    Vector2Int  spawnPos;
    Vector2Int  exitPos;
    
    [Button("Generate")]
    void Generate()
    {
        Random.InitState(seed);
        heightmap = new Heightmap(size.x, size.y);
        heightmap.CelularAutomata(3, 0.25f, new Vector2Int(2, 10), new Vector2Int(4, 10));
        heightmap.Border(1, 1.0f);

        // Segment in regions
        // 1. Initial setup (0 = unassigned, 1 = wall)
        int[] regions = new int[size.x * size.y];
        float h;
        int   idx = 0;
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                h = heightmap.Get(idx);
                regions[idx] = (h < 0.5f) ? (0) : (1);

                idx++;
            }
        }

        // 2. Paint regions with flood fill
        int region = 2;
        int largestRegion = -1;
        int largestRegionArea = -1;

        idx = 0;
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (regions[idx] == 0)
                {
                    int areaFlooded = FloodFill(regions, x, y, region);
                    if (areaFlooded > largestRegionArea)
                    {
                        largestRegionArea = areaFlooded;
                        largestRegion = region;
                    }
                    region++;
                }
                idx++;
            }
        }

        // 3. Remove all but the largest region
        idx = 0;
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                int r = regions[idx];
                if ((r > 1) && (r != largestRegion))
                {
                    heightmap.Set(idx, 1.0f);
                    regions[idx] = 1;
                }
                idx++;
            }
        }

        // 4. Set valid region to region 2
        idx = 0;
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                int r = regions[idx];
                if (r == largestRegion)
                {
                    regions[idx] = 2;
                }
                idx++;
            }
        }

        // Select spawn position
        int corner = Random.Range(0, 4);

        spawnPos = FindClearArea(regions, 1, corner);

        if (spawnPos.x == -1)
        {
            // Problem with generation, can't find a suitable spawn point
            Debug.LogError("Can't find spawn position!");
            return;
        }

        // Select exit position
        exitPos = FindClearArea(regions, 1, (corner + 2) % 4);

        if (exitPos.x == -1)
        {
            // Problem with generation, can't find a suitable spawn point
            Debug.LogError("Can't find exit position!");
            return;
        }


#if UNITY_EDITOR
        if (debug)
        {
            if (targetTexture)
            {
                string filename = "Assets/Textures/heightmap.png";

                if (targetTexture != null)
                {
                    filename = AssetDatabase.GetAssetPath(targetTexture);
                }

                // Save heightmap to texture
                heightmap.SaveTexture(filename);
                // Save regions to texture
                regions[spawnPos.x + spawnPos.y * size.x] = 4;
                regions[exitPos.x + exitPos.y * size.x] = 6;
                SaveRegions(regions, filename);

                if (targetTexture == null)
                {
                    targetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(filename);
                }

                AssetDatabase.Refresh();
            }
        }
#endif
    }

    int FloodFill(int[] regions, int x, int y, int region)
    {
        int idx = y * size.x + x;

        if (regions[idx] != 0) return 0;

        regions[idx] = region;

        int count = 1;

        if (x > 0) count += FloodFill(regions, x - 1, y, region);
        if (x < (size.x - 1)) count += FloodFill(regions, x + 1, y, region);
        if (y > 0) count += FloodFill(regions, x, y - 1, region);
        if (y < (size.y - 1)) count += FloodFill(regions, x, y + 1, region);

        return count;
    }

    bool HasFreeArea(int[] regions, int x, int y, int area, int region)
    {
        if (x - area < 0) return false;
        if (x + area > (size.x - 1)) return false;
        if (y < area) return false;
        if (y + area > (size.y - 1)) return false;

        for (int dy = -area; dy <= area; dy++)
        {
            for (int dx = -area; dx <= area; dx++)
            {
                if (regions[(y + dy) * size.x + (x + dx)] != region) return false;
            }
        }

        return true;
    }

    Vector2Int FindClearArea(int[] regions, int radius, int corner)
    {
        int sx = 0, sy = 0;
        int dx = 0, dy = 0;
        switch (corner)
        {
            case 0: sx = 0; sy = 0; dx = 1; dy = 1; break;
            case 1: sx = size.x - 1; sy = 0; dx = -1; dy = 1; break;
            case 2: sx = size.x - 1; sy = size.y - 1; dx = -1; dy = -1; break;
            case 3: sx = 0; sy = size.y - 1; dx = 1; dy = -1; break;
        }

        int px = sx;
        int py = sy;        

        while (true)
        {
            px = sx;
            while (true)
            {
                if (HasFreeArea(regions, px, py, radius, 2))
                {
                    return new Vector2Int(px, py);
                }

                if ((dx < 0) && (px == 0)) break;
                if ((dx > 0) && (px == size.x - 1)) break;

                px = px + dx;
            }
            

            if ((dy < 0) && (py == 0)) break;
            if ((dy > 0) && (py == size.y - 1)) break;

            py = py + dy;
        }

        return new Vector2Int(-1, -1);
    }

    void SaveRegions(int[] regions, string filename)
    {
        Color[] colors = new Color[14]
        {
            new Color(0,0,0,1),
            new Color(1,1,1,1),
            new Color(1,0,0,1),
            new Color(1,0.5f,0,1),
            new Color(1,1,0,1),
            new Color(0.5f,1,0,1),
            new Color(0,1,0,1),
            new Color(0,1,0.5f,1),
            new Color(0,1,1,1),
            new Color(0,0.5f,1,1),
            new Color(0,0,1,1),
            new Color(0.5f,0,1,1),
            new Color(1,0,1,1),
            new Color(1,0,0.5f,1),
        };

        Texture2D newTexture = new Texture2D(size.x, size.y, TextureFormat.ARGB32, false);

        int idx = 0;
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                newTexture.SetPixel(x, y, colors[regions[idx]]);
                idx++;
            }
        }

        byte[] bytes = newTexture.EncodeToPNG();

        System.IO.File.WriteAllBytes(filename, bytes);
    }
}
