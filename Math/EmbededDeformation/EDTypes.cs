using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    public enum DeformationGraphSource { NavMeshAndStructure, StructureOnly };
    public enum BindingSelectionMode { ClosestOne, NearestK };
    public enum BindingWeightMode { Uniform, InversePower, Gaussian, OriginalED };
    public enum GraphLinkMode { PartitionAdjacency, SharedBindings, DirectionAware };

    [Serializable]
    public class EDNode
    {
        public DVector3         restPosition;
        public DVector3         restRight = DVector3.right;
        public DVector3         restUp = DVector3.up;
        public DVector3         restForward = DVector3.forward;
        public List<int>        neighbors = new();

        private static DVector3 SafeNormalized(DVector3 v, DVector3 fallback)
        {
            if (v.sqrMagnitude < 1e-8f)
                return fallback.normalized;

            return v.normalized;
        }

        private static DVector3 PickFallbackUp(DVector3 forward)
        {
            forward = SafeNormalized(forward, DVector3.forward);

            DVector3 candidate = DVector3.up;

            if (Math.Abs(DVector3.Dot(candidate, forward)) > 0.95)
                candidate = DVector3.right;

            return candidate;
        }

        public void BuildSurfaceFrame(DVector3 forwardReference, DVector3 surfaceNormal)
        {
            const double epsilon = 1e-8;

            // The surface normal is authoritative.
            restUp = SafeNormalized(surfaceNormal,DVector3.up);

            // Project the graph direction onto the surface.
            restForward = forwardReference - DVector3.Dot(forwardReference, restUp) * restUp;

            if (restForward.sqrMagnitude < epsilon)
            {
                // Pick any stable tangent direction on the surface.
                DVector3 fallback = (Math.Abs(DVector3.Dot(restUp, DVector3.forward)) < 0.95) ? (DVector3.forward) : (DVector3.right);

                restForward = fallback - DVector3.Dot(fallback, restUp) * restUp;
            }

            restForward.Normalize();

            // Rotate forward 90 degrees within the tangent plane.
            restRight = DVector3.Cross(restUp, restForward);

            if (restRight.sqrMagnitude < epsilon)
                restRight = DVector3.right;
            else
                restRight.Normalize();

            // Remove numerical drift while preserving the selected normal.
            restForward = DVector3.Cross(restRight, restUp).normalized;
        }
    }

    [Serializable]
    public class EDClearanceCache
    {
        [SerializeField]
        private double[] values;

        public EDClearanceCache(int count)
        {
            values = new double[count];
        }

        /// <summary>
        /// Number of cached segments, zero when nothing has been computed yet.
        ///
        /// Testing the cache reference against null is not enough: Unity cannot serialize null
        /// for a [Serializable] class, so after a scene load or domain reload a cache that was
        /// never populated comes back as a live object wrapping a null array.
        /// </summary>
        public int count => (values != null) ? (values.Length) : (0);

        public EDClearanceCache Clone()
        {
            var clone = new EDClearanceCache(values.Length);
            Array.Copy(values, clone.values, values.Length);
            return clone;
        }

        public double Get(int index) => values[index];

        public void Set(int index, double value)
        {
            values[index] = value;
        }
    }

    [Serializable]
    // Public because the energy terms are written against them: a term evaluates a residual from a
    // state and fills Jacobian rows from it, so these are part of the extension surface rather than
    // an implementation detail of the solver.
    public class EDState
    {
        [SerializeField]
        private double[]        parameters; // 12 * nodeCount
        [SerializeField]
        public EDClearanceCache clearances;

        public EDState(int nodeCount)
        {
            parameters = new double[12 * nodeCount];
            // Set all matrices to identity
            for (int i = 0; i < nodeCount; i++)
            {
                int baseIndex = i * 12;
                parameters[baseIndex + 0] = 
                parameters[baseIndex + 5] = 
                parameters[baseIndex + 10] = 1.0; 
            }
        }   

        public double Get(int index) => parameters[index];
        public void Set(int index, double value)
        {
            parameters[index] = value;
        }

        public int Count => parameters.Length;

        public void SetTranslation(int nodeIndex, double x, double y, double z)
        {
            int o = nodeIndex * 12;

            parameters[o + 3] = x;
            parameters[o + 7] = y;
            parameters[o + 11] = z;
        }

        public void ResetRotation(int nodeIndex)
        {
            int o = nodeIndex * 12;

            parameters[o + 0] = 1.0;
            parameters[o + 1] = 0.0;
            parameters[o + 2] = 0.0;

            parameters[o + 4] = 0.0;
            parameters[o + 5] = 1.0;
            parameters[o + 6] = 0.0;

            parameters[o + 8] = 0.0;
            parameters[o + 9] = 0.0;
            parameters[o + 10] = 1.0;
        }

        public void Apply(Vector<double> delta, double damping)
        {
            if (delta == null)
                throw new ArgumentNullException(nameof(delta));

            if (delta.Count != parameters.Length)
            {
                throw new ArgumentException($"Delta size mismatch. Delta={delta.Count}, State={parameters.Length}");
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                double v = parameters[i] + damping * delta[i];

                if (!double.IsFinite(v))
                {
                    throw new InvalidOperationException($"Non-finite state parameter at index {i}.");
                }

                parameters[i] = v;
            }
        }

        public EDState Clone()
        {
            EDState clone = new EDState(parameters.Length / 12);
            Array.Copy(parameters, clone.parameters, parameters.Length);
            clone.clearances = clearances.Clone();
            return clone;
        }

        public EDState CloneAndApply(Vector<double> delta, double damping = 1.0)
        {
            EDState clone = Clone();
            clone.Apply(delta, damping);
            return clone;
        }

        /// <summary>
        /// Cached clearance for a segment, or double.MaxValue when none was computed - the same
        /// "no clearance" marker ComputeClearance stores for segments it cannot measure.
        ///
        /// The cache is absent entirely in every mode that does not run SetNavEDParameters, and
        /// because EDClearanceCache is [Serializable] Unity resurrects it after a domain reload as
        /// a live object wrapping a null array, so a plain null test is not sufficient.
        /// </summary>
        public double GetClearance(int index)
        {
            if ((clearances == null) || (index < 0) || (index >= clearances.count))
                return double.MaxValue;

            return clearances.Get(index);
        }

        public DVector3 TransformOffset(int nodeIndex, DVector3 localOffset)
        {
            int o = nodeIndex * 12;

            double m00 = Get(o + 0);
            double m01 = Get(o + 1);
            double m02 = Get(o + 2);
            double m03 = Get(o + 3);

            double m10 = Get(o + 4);
            double m11 = Get(o + 5);
            double m12 = Get(o + 6);
            double m13 = Get(o + 7);

            double m20 = Get(o + 8);
            double m21 = Get(o + 9);
            double m22 = Get(o + 10);
            double m23 = Get(o + 11);

            return new DVector3(
                m00 * localOffset.x + m01 * localOffset.y + m02 * localOffset.z + m03,
                m10 * localOffset.x + m11 * localOffset.y + m12 * localOffset.z + m13,
                m20 * localOffset.x + m21 * localOffset.y + m22 * localOffset.z + m23
            );
        }
    }

    [Serializable]
    public readonly struct EDStateView
    {
        [SerializeField]
        private readonly EDState            parentState;
        [SerializeField]
        private readonly int                perturbedIndex;
        [SerializeField]
        private readonly double             perturbation;
        [SerializeField]
        private readonly EDClearanceCache   _clearances;

        public EDStateView(EDState parameters, EDClearanceCache clearances = null)
        {
            this.parentState = parameters;
            this.perturbedIndex = -1;
            this.perturbation = 0.0;
            this._clearances = clearances;
        }

        public EDStateView(EDState parameters, int perturbedIndex, double perturbation, EDClearanceCache clearances = null)
        {
            this.parentState = parameters;
            this.perturbedIndex = perturbedIndex;
            this.perturbation = perturbation;
            this._clearances = clearances;
        }

        public EDClearanceCache clearances => (_clearances == null) ? (parentState.clearances) : (_clearances);

        /// <summary>
        /// Cached clearance for a segment, or double.MaxValue when none was computed. Mirrors
        /// EDState.GetClearance - see there for why the cache reference alone is not a validity
        /// test.
        /// </summary>
        public double GetClearance(int index)
        {
            var cache = clearances;

            if ((cache == null) || (index < 0) || (index >= cache.count))
                return double.MaxValue;

            return cache.Get(index);
        }
        public double Get(int index) => (index == (perturbedIndex)) ? (parentState.Get(index) + perturbation) : (parentState.Get(index));

        public DVector3 DeformVertex(int nodeIndex, DVector3 p, DVector3 restPos)
        {
            return TransformOffset(nodeIndex, p - restPos) + restPos;
        }

        public DVector3 TransformOffset(int nodeIndex, DVector3 localOffset)
        {
            int o = nodeIndex * 12;

            double m00 = Get(o + 0);
            double m01 = Get(o + 1);
            double m02 = Get(o + 2);
            double m03 = Get(o + 3);

            double m10 = Get(o + 4);
            double m11 = Get(o + 5);
            double m12 = Get(o + 6);
            double m13 = Get(o + 7);

            double m20 = Get(o + 8);
            double m21 = Get(o + 9);
            double m22 = Get(o + 10);
            double m23 = Get(o + 11);

            return new DVector3(
                m00 * localOffset.x + m01 * localOffset.y + m02 * localOffset.z + m03,
                m10 * localOffset.x + m11 * localOffset.y + m12 * localOffset.z + m13,
                m20 * localOffset.x + m21 * localOffset.y + m22 * localOffset.z + m23
            );
        }

        public DVector3 TransformVector(int nodeIndex, DVector3 localVector)
        {
            int o = nodeIndex * 12;

            double m00 = Get(o + 0);
            double m01 = Get(o + 1);
            double m02 = Get(o + 2);
            double m03 = Get(o + 3);

            double m10 = Get(o + 4);
            double m11 = Get(o + 5);
            double m12 = Get(o + 6);
            double m13 = Get(o + 7);

            double m20 = Get(o + 8);
            double m21 = Get(o + 9);
            double m22 = Get(o + 10);
            double m23 = Get(o + 11);

            return new DVector3(
                m00 * localVector.x + m01 * localVector.y + m02 * localVector.z,
                m10 * localVector.x + m11 * localVector.y + m12 * localVector.z,
                m20 * localVector.x + m21 * localVector.y + m22 * localVector.z
            );
        }


        public DVector3 GetAxisX(int nodeIndex)
        {
            int o = nodeIndex * 12;

            return new DVector3(
                Get(o + 0),  // m00
                Get(o + 4),  // m10
                Get(o + 8)   // m20
            );
        }
        public DVector3 GetAxisY(int nodeIndex)
        {
            int o = nodeIndex * 12;

            return new DVector3(
                Get(o + 1),  // m01
                Get(o + 5),  // m11
                Get(o + 9)   // m21
            );
        }
        public DVector3 GetAxisZ(int nodeIndex)
        {
            int o = nodeIndex * 12;

            return new DVector3(
                Get(o + 2),   // m02
                Get(o + 6),   // m12
                Get(o + 10)   // m22
            );
        }

        public DVector3 GetTranslation(int nodeIndex)
        {
            int o = nodeIndex * 12;

            return new DVector3(
                Get(o + 3),   // m03
                Get(o + 7),   // m13
                Get(o + 11)   // m23
            );
        }
    }

    [Serializable]
    public struct EDVertexBinding
    {
        public int[]    nodeIndices;   // k nearest
        public double[] weights;     // normalized
    }

    [Serializable]
    public struct EDHandleConstraint
    {
        public Matrix4x4 restHandleMatrix;
        public Matrix4x4 currentHandleMatrix;
        public List<int> vertexIndices;

        // Width of the handle bar in its rest configuration.
        public float width;

        // Used by StructureOnly constraints.
        // True for connector handles, false for root/center handles.
        public bool isTerminal;
    }

    [Serializable]
    public struct EDTerminalConstraint
    {
        public int      nodeIndex;

        public DVector3 targetPosition;

        public DVector3 targetRight;
        public DVector3 targetUp;
        public DVector3 targetForward;

        // Scale along the terminal frame's right axis.
        public double   targetScale;
    }

    [Serializable]
    public struct EDLinkAngleConstraint
    {
        public int centerNode;
        public int neighborA;
        public int neighborB;

        public double restCos;
        public double restSin;
    }

    [Serializable]
    public struct EDVertexConstraint
    {
        public int      vertexIndex;
        public DVector3 targetPosition;
    }

    [Serializable]
    public class NavEDSegments
    {
        public DVector3         p1, p2;
        public EDVertexBinding  bind1, bind2;
        public DVector3         center;
        public DVector3         normal, probeT, probeB;
        public EDVertexBinding  cBind, tBind, bBind;
        public int              node1 = -1;
        public int              node2 = -1;
    }

    /// <summary>
    /// A group of navmesh vertices spanning an opening in the piece - a doorway rather than a wall.
    ///
    /// Clearance is measured against boundary edges, and an edge across an opening is not something
    /// an agent is obstructed by, so it must not be measured against. An edge counts as spanning an
    /// opening when *both* its endpoints belong to the *same* group; endpoints in two different
    /// openings do not make the edge between them one.
    ///
    /// Supplied explicitly rather than inferred from the handle constraints. The two happened to
    /// coincide - a connector is both where a piece is pinned and where it opens - but they are
    /// different statements, and reading one off the other meant a change to how handles bind
    /// silently changed what clearance measured.
    /// </summary>
    [Serializable]
    public class EDClearanceOpening
    {
        public List<int> vertexIndices = new();
    }

    public delegate bool HasLOS(Vector3 p1, Vector3 p2);
    public delegate bool TryGetSurfaceNormal(Vector3 p, out Vector3 normal);
    
}
#endif
