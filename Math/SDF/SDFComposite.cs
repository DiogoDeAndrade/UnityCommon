using System;
using UnityEngine;
using UnityMeshSimplifier.Internal;

namespace UC
{
    [Serializable]
    public class SDFComposite : SDF
    {
        public enum Operation { Union, Intersect, Subtract };

        public Operation    op;
        public SDF[]        operands;

        public override bool needArgs => true;
        public override SDF[] args
        {
            get { return operands; }
            set { operands = value; }
        }

        public override Bounds GetBounds()
        {
            if ((operands == null) || (operands.Length == 0)) return new Bounds();

            Bounds  bounds = operands[0].GetBounds();
            for (int i = 1; i < operands.Length; i++) bounds.Encapsulate(operands[i].GetBounds());

            return bounds;
        }

        public override float Sample(Vector3 worldPoint)
        {
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
            }
            return ret;
        }

#if UNITY_6000_0_OR_NEWER
        public override void DrawGizmos()
        {
            switch (op)
            {
                case Operation.Union:
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
