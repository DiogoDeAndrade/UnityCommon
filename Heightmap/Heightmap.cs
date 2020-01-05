using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Heightmap
{
    public int sizeX;
    public int sizeY;

    public float[] heights;

    public Heightmap(int sx, int sy)
    {
        sizeX = sx;
        sizeY = sy;

        heights = new float[sizeX * sizeY];
    }

    public void PerlinNoise(Vector2 frequency, Vector2 offset, float amplitude)
    {
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                heights[y * sizeX + x] = amplitude * Mathf.PerlinNoise((x * frequency.x + offset.x) / sizeX, 
                                                                       (y * frequency.y + offset.y) / sizeY);
            }
        }
    }

    public void CelularAutomata(int steps, float onProb, Vector2Int starvationRule, Vector2Int birthRule)
    {
        // Initial condition
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                heights[y * sizeX + x] = (Random.value < onProb) ? (1.0f) : (0.0f);
            }
        }

        float[] buffer = new float[sizeX * sizeY];
        for (int s = 0; s < steps; s++)
        {
            CelularAutomata_RunStep(heights, buffer, starvationRule.x, starvationRule.y, birthRule.x, birthRule.y);

            // Swap buffers
            float[] tmp = heights;
            heights = buffer;
            buffer = tmp;
        }
    }

    public void CelularAutomata_RunStep(float[] src, float[] dest, int underPop, int overPop, int underBirth, int overBirth)
    {
        bool setInEnd = false;
        if (src == null) { src = heights; setInEnd = true; }
        if (dest == null) dest = new float[sizeX * sizeY];

        // Run stuff
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                int n = CountMooreNeighbours(src, x, y, true);
                int idx = x + y * sizeX;
                float v = heights[idx];

                if (heights[idx] > 0.5f)
                {
                    if ((n < underPop) || (n > overPop)) v = 0.0f;
                }
                else
                {
                    if ((n >= underBirth) && (n <= overBirth)) v = 1.0f;
                }

                dest[idx] = v;
            }
        }

        if (setInEnd)
        {
            heights = dest;
        }
    }

    int CountMooreNeighbours(float[] cells, int x, int y, bool wrap)
    {
        int count = 0;

        if (wrap)
        {
            for (int yy = -1; yy <= 1; yy++)
            {
                for (int xx = -1; xx <= 1; xx++)
                {
                    if ((xx == 0) && (yy == 0)) continue;

                    int dx = (x + xx) % sizeX; if (dx < 0) dx = sizeX + dx;
                    int dy = (y + yy) % sizeY; if (dy < 0) dy = sizeY + dy;

                    if (cells[dx + dy * sizeX] > 0.5f) count++;
                }
            }
        }
        else
        {
            for (int yy = -1; yy <= 1; yy++)
            {
                for (int xx = -1; xx <= 1; xx++)
                {
                    if ((xx == 0) && (yy == 0)) continue;

                    int dx = Mathf.Clamp(x + xx, 0, sizeX - 1);
                    int dy = Mathf.Clamp(y + yy, 0, sizeY - 1);

                    if (cells[dx + dy * sizeX] > 0.5f) count++;
                }
            }
        }

        return count;
    }

    public float Get(int x, int y)
    {
        return heights[y * sizeX + x];
    }
}
