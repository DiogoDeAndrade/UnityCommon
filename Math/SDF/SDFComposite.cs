using NaughtyAttributes;
using System;
using UnityEngine;
using UnityMeshSimplifier.Internal;

namespace UC
{
    [Serializable]
    public class SDFComposite : SDF
    {
        public enum Operation { Union, Intersect, Subtract, SmoothUnion };

        public Operation    op;
        public SDF[]        operands;
        
        [Range(0.0f, 5.0f)]
        public float smoothK = 0.5f;   
        [Range(0.0f, 10.0f)]
        public float localMaskRadius = 0.0f;

        public override bool needArgs => true;
        public override SDF[] args
        {
            get { return operands; }
            set { operands = value; }
        }

        bool isSmoothUnion => op == Operation.SmoothUnion;

        public override Bounds GetBounds()
        {
            if ((operands == null) || (operands.Length == 0)) return new Bounds();

            Bounds  bounds = operands[0].GetBounds();
            for (int i = 1; i < operands.Length; i++) bounds.Encapsulate(operands[i].GetBounds());

            return bounds;
        }

        public override float Sample(Vector3 worldPoint)
        {
            if ((operands == null) || (operands.Length == 0)) return 0.0f;
            float ret = operands[0].Sample(worldPoint);
            switch (op)
            {
                case Operation.Union:
                    for (int i = 1; i < operands.Length; i++) ret = Mathf.Min(ret, operands[i].Sample(worldPoint));
                    break;
                case Operation.Intersect:
                    for (int i = 1; i < operands.Length; i++) ret = Mathf.Max(ret, operands[i].Sample(worldPoint));
                    break;
                case Operation.Subtract:
                    for (int i = 1; i < operands.Length; i++) ret = Mathf.Max(ret, -operands[i].Sample(worldPoint));
                    break;
                case Operation.SmoothUnion:
                    for (int i = 1; i < operands.Length; i++)
                    {
                        float d = operands[i].Sample(worldPoint);

                        // If a local mask is defined, only smooth if within mask radius
                        if (localMaskRadius > 0.0f)
                        {
                            if (Mathf.Abs(ret - d) < localMaskRadius)
                                ret = SmoothMin(ret, d, smoothK);
                            else
                                ret = Mathf.Min(ret, d);
                        }
                        else
                        {
                            ret = SmoothMin(ret, d, smoothK);
                        }
                    }
                    break;
            }
            return ret;
        }

        private static float SmoothMin(float a, float b, float k)
        {
            // polynomial smooth min
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0.0f) / k;
            return Mathf.Min(a, b) - h * h * h * k * (1.0f / 6.0f); // variant with C2 continuity
        }

#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR
        public override void DrawGizmos()
        {
            if (operands == null) return;
            if (operands.Length == 0) return;

            switch (op)
            {
                case Operation.Union:
                case Operation.SmoothUnion:
                    foreach (var arg in operands) arg.DrawGizmos();
                    break;
                case Operation.Intersect:
                    operands[0].DrawGizmos();
                    Gizmos.color = Color.red.ChangeAlpha(Gizmos.color.a);
                    for (int i = 1; i < operands.Length; i++) operands[i].DrawGizmos();
                    break;
                case Operation.Subtract:
                    operands[0].DrawGizmos();
                    Gizmos.color = Color.magenta.ChangeAlpha(Gizmos.color.a);
                    for (int i = 1; i < operands.Length; i++) operands[i].DrawGizmos();
                    break;
                default:
                    break;
            }
        }
#endif
    }
}
