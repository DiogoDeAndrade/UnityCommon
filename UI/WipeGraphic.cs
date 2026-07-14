using UnityEngine;
using UnityEngine.UI;

namespace UC
{
    public enum WipeType
    {
        Random = -1,
        Blinds = 0,
        DiagonalSlide = 1,
        CurtainUp = 2,
        CurtainDown = 3,
        WipeLeft = 4,
        WipeRight = 5,
        DiagonalSlideMirrored = 6
    }

    // Draws the rect as a solid-color quad (two triangles) whose vertices move according to
    // the wipe type. Openness 0 = fully covered, 1 = fully revealed (nothing drawn).
    public class WipeGraphic : Graphic
    {
        [SerializeField] private WipeType wipeType = WipeType.Blinds;
        [SerializeField, Range(0.0f, 1.0f)] private float openness = 1.0f;

        public WipeType type
        {
            get { return wipeType; }
            set
            {
                if (wipeType == value) return;
                wipeType = value;
                SetVerticesDirty();
            }
        }

        public float open
        {
            get { return openness; }
            set
            {
                float v = Mathf.Clamp01(value);
                if (openness == v) return;
                openness = v;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (openness >= 1.0f) return;

            Rect r = GetPixelAdjustedRect();
            float sx = r.width;
            float sy = r.height;
            float tt = openness;

            // Base quad, top-left origin with Y down (same space as the original engine);
            // converted to UI space when the vertices are emitted below
            var v = new Vector2[6];
            v[0] = new Vector2(0.0f, 0.0f);
            v[1] = new Vector2(sx, 0.0f);
            v[2] = new Vector2(0.0f, sy);
            v[3] = new Vector2(0.0f, sy);
            v[4] = new Vector2(sx, 0.0f);
            v[5] = new Vector2(sx, sy);

            switch (wipeType)
            {
                case WipeType.Blinds:
                    v[4].y = tt * sy;
                    v[2].y = (1.0f - tt) * sy;
                    break;
                case WipeType.DiagonalSlide:
                case WipeType.DiagonalSlideMirrored:
                    {
                        float d = Mathf.Sqrt(sx * sx + sy * sy);
                        Vector2 dv = (wipeType == WipeType.DiagonalSlide) ? new Vector2(sx, sy).normalized
                                                                          : new Vector2(sy, sx).normalized;
                        Vector2 disp1 = -d * tt * dv;
                        Vector2 disp2 = d * tt * dv;
                        for (int i = 0; i < 3; i++) v[i] += disp1;
                        for (int i = 3; i < 6; i++) v[i] += disp2;
                    }
                    break;
                case WipeType.CurtainUp:
                    v[2].y = v[3].y = v[5].y = (1.0f - tt) * sy;
                    break;
                case WipeType.CurtainDown:
                    v[0].y = v[1].y = v[4].y = tt * sy;
                    break;
                case WipeType.WipeLeft:
                    v[1].x = v[4].x = v[5].x = (1.0f - tt) * sx;
                    break;
                case WipeType.WipeRight:
                    v[0].x = v[2].x = v[3].x = tt * sx;
                    break;
            }

            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;
            for (int i = 0; i < 6; i++)
            {
                vert.position = new Vector3(r.xMin + v[i].x, r.yMax - v[i].y, 0.0f);
                vh.AddVert(vert);
            }
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(3, 4, 5);
        }
    }
}
