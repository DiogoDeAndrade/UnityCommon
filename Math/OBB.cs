using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    [System.Serializable]
    public class OBB
    {
        [SerializeField] Vector3 _center;
        [SerializeField] Vector3 _extents;
        [SerializeField] Vector3[] _axis;

        public Vector3 center { get => _center; set { _center = value; } }
        public Vector3 extents { get => _extents; set { _extents = value; } }
        public Vector3 size { get => _extents * 2.0f; set { _extents = value * 0.5f; } }
        public Vector3[] axis { get => _axis; set { _axis = value; } }

        public OBB(OBB src)
        {
            _center = src._center;
            _extents = src._extents;
            _axis = src._axis;
        }

        public OBB(Vector3 center, Vector3 size)
        {
            _axis = new Vector3[3];
            _axis[0] = Vector3.right;
            _axis[1] = Vector3.up;
            _axis[2] = Vector3.forward;
            this._center = center;
            _extents = size * 0.5f;
        }

        public OBB(Bounds bounds) : this(bounds.center, bounds.size)
        {

        }

        public void Transform(Matrix4x4 matrix)
        {
            _center = matrix * new Vector4(_center.x, _center.y, _center.z, 1);
            for (int i = 0; i < 3; i++)
            {
                _axis[i] = matrix * new Vector4(_axis[i].x, _axis[i].y, _axis[i].z, 0);
                _axis[i].Normalize();
            }
            var scale = matrix.lossyScale;
            _extents = new Vector3(_extents.x * scale.x, _extents.y * scale.y, _extents.z * scale.z);
        }

        public Vector3 GetCorner(int i)
        {
            switch (i)
            {
                case 0: return _center - _axis[0] * _extents.x - _axis[1] * _extents.y - _axis[2] * _extents.z;
                case 1: return _center + _axis[0] * _extents.x - _axis[1] * _extents.y - _axis[2] * _extents.z;
                case 2: return _center - _axis[0] * _extents.x + _axis[1] * _extents.y - _axis[2] * _extents.z;
                case 3: return _center + _axis[0] * _extents.x + _axis[1] * _extents.y - _axis[2] * _extents.z;
                case 4: return _center - _axis[0] * _extents.x - _axis[1] * _extents.y + _axis[2] * _extents.z;
                case 5: return _center + _axis[0] * _extents.x - _axis[1] * _extents.y + _axis[2] * _extents.z;
                case 6: return _center - _axis[0] * _extents.x + _axis[1] * _extents.y + _axis[2] * _extents.z;
                case 7:
                default:
                    return _center + _axis[0] * _extents.x + _axis[1] * _extents.y + _axis[2] * _extents.z;
            }
        }

        public bool Intersect(OBB otherOBB)
        {
            // Convenience variables.
            Vector3 C0 = _center;
            Vector3[] A0 = _axis;
            Vector3 E0 = _extents;
            Vector3 C1 = otherOBB._center;
            Vector3[] A1 = otherOBB._axis;
            Vector3 E1 = otherOBB._extents;
            float epsilon = 1e-6f;
            float cutoff = 1.0f - epsilon;
            bool existsParallelPair = false;

            // Compute difference of box centers.
            Vector3 D = C1 - C0;

            // dot01[i,j] = Vector3.Dot(A0[i],A1[j]) = A1[j,i]
            float[,] dot01 = new float[3, 3];
            float[,] absDot01 = new float[3, 3];
            float[] dotDA0 = new float[3];
            float r0, r1, r;
            float r01;

            // Test for separation on the axis C0 + t*A0[0].
            for (int i = 0; i < 3; ++i)
            {
                dot01[0, i] = Vector3.Dot(A0[0], A1[i]);
                absDot01[0, i] = Mathf.Abs(dot01[0, i]);
                if (absDot01[0, i] > cutoff)
                {
                    existsParallelPair = true;
                }
            }
            dotDA0[0] = Vector3.Dot(D, A0[0]);
            r = Mathf.Abs(dotDA0[0]);
            r1 = E1[0] * absDot01[0, 0] + E1[1] * absDot01[0, 1] + E1[2] * absDot01[0, 2];
            r01 = E0[0] + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A0[1].
            for (int i = 0; i < 3; ++i)
            {
                dot01[1, i] = Vector3.Dot(A0[1], A1[i]);
                absDot01[1, i] = Mathf.Abs(dot01[1, i]);
                if (absDot01[1, i] > cutoff)
                {
                    existsParallelPair = true;
                }
            }
            dotDA0[1] = Vector3.Dot(D, A0[1]);
            r = Mathf.Abs(dotDA0[1]);
            r1 = E1[0] * absDot01[1, 0] + E1[1] * absDot01[1, 1] + E1[2] * absDot01[1, 2];
            r01 = E0[1] + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A0[2].
            for (int i = 0; i < 3; ++i)
            {
                dot01[2, i] = Vector3.Dot(A0[2], A1[i]);
                absDot01[2, i] = Mathf.Abs(dot01[2, i]);
                if (absDot01[2, i] > cutoff)
                {
                    existsParallelPair = true;
                }
            }
            dotDA0[2] = Vector3.Dot(D, A0[2]);
            r = Mathf.Abs(dotDA0[2]);
            r1 = E1[0] * absDot01[2, 0] + E1[1] * absDot01[2, 1] + E1[2] * absDot01[2, 2];
            r01 = E0[2] + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A1[0].
            r = Mathf.Abs(Vector3.Dot(D, A1[0]));
            r0 = E0[0] * absDot01[0, 0] + E0[1] * absDot01[1, 0] + E0[2] * absDot01[2, 0];
            r01 = r0 + E1[0];
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A1[1].
            r = Mathf.Abs(Vector3.Dot(D, A1[1]));
            r0 = E0[0] * absDot01[0, 1] + E0[1] * absDot01[1, 1] + E0[2] * absDot01[2, 1];
            r01 = r0 + E1[1];
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A1[2].
            r = Mathf.Abs(Vector3.Dot(D, A1[2]));
            r0 = E0[0] * absDot01[0, 2] + E0[1] * absDot01[1, 2] + E0[2] * absDot01[2, 2];
            r01 = r0 + E1[2];
            if (r > r01)
            {
                return false;
            }

            // At least one pair of box axes was parallel, so the separation is
            // effectively in 2D. The edge-edge axes do not need to be tested.
            if (existsParallelPair)
            {
                // The result.separating[] values are invalid because there is
                // no separation.
                return true;
            }

            // Test for separation on the axis C0 + t*A0[0]xA1[0].
            r = Mathf.Abs(dotDA0[2] * dot01[1, 0] - dotDA0[1] * dot01[2, 0]);
            r0 = E0[1] * absDot01[2, 0] + E0[2] * absDot01[1, 0];
            r1 = E1[1] * absDot01[0, 2] + E1[2] * absDot01[0, 1];
            r01 = r0 + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A0[0]xA1[1].
            r = Mathf.Abs(dotDA0[2] * dot01[1, 1] - dotDA0[1] * dot01[2, 1]);
            r0 = E0[1] * absDot01[2, 1] + E0[2] * absDot01[1, 1];
            r1 = E1[0] * absDot01[0, 2] + E1[2] * absDot01[0, 0];
            r01 = r0 + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A0[0]xA1[2].
            r = Mathf.Abs(dotDA0[2] * dot01[1, 2] - dotDA0[1] * dot01[2, 2]);
            r0 = E0[1] * absDot01[2, 2] + E0[2] * absDot01[1, 2];
            r1 = E1[0] * absDot01[0, 1] + E1[1] * absDot01[0, 0];
            r01 = r0 + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A0[1]xA1[0].
            r = Mathf.Abs(dotDA0[0] * dot01[2, 0] - dotDA0[2] * dot01[0, 0]);
            r0 = E0[0] * absDot01[2, 0] + E0[2] * absDot01[0, 0];
            r1 = E1[1] * absDot01[1, 2] + E1[2] * absDot01[1, 1];
            r01 = r0 + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A0[1]xA1[1].
            r = Mathf.Abs(dotDA0[0] * dot01[2, 1] - dotDA0[2] * dot01[0, 1]);
            r0 = E0[0] * absDot01[2, 1] + E0[2] * absDot01[0, 1];
            r1 = E1[0] * absDot01[1, 2] + E1[2] * absDot01[1, 0];
            r01 = r0 + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A0[1]xA1[2].
            r = Mathf.Abs(dotDA0[0] * dot01[2, 2] - dotDA0[2] * dot01[0, 2]);
            r0 = E0[0] * absDot01[2, 2] + E0[2] * absDot01[0, 2];
            r1 = E1[0] * absDot01[1, 1] + E1[1] * absDot01[1, 0];
            r01 = r0 + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A0[2]xA1[0].
            r = Mathf.Abs(dotDA0[1] * dot01[0, 0] - dotDA0[0] * dot01[1, 0]);
            r0 = E0[0] * absDot01[1, 0] + E0[1] * absDot01[0, 0];
            r1 = E1[1] * absDot01[2, 2] + E1[2] * absDot01[2, 1];
            r01 = r0 + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A0[2]xA1[1].
            r = Mathf.Abs(dotDA0[1] * dot01[0, 1] - dotDA0[0] * dot01[1, 1]);
            r0 = E0[0] * absDot01[1, 1] + E0[1] * absDot01[0, 1];
            r1 = E1[0] * absDot01[2, 2] + E1[2] * absDot01[2, 0];
            r01 = r0 + r1;
            if (r > r01)
            {
                return false;
            }

            // Test for separation on the axis C0 + t*A0[2]xA1[2].
            r = Mathf.Abs(dotDA0[1] * dot01[0, 2] - dotDA0[0] * dot01[1, 2]);
            r0 = E0[0] * absDot01[1, 2] + E0[1] * absDot01[0, 2];
            r1 = E1[0] * absDot01[2, 1] + E1[1] * absDot01[2, 0];
            r01 = r0 + r1;
            if (r > r01)
            {
                return false;
            }

            return true;
        }

        public void DrawGizmo()
        {
            Vector3[] corners = new Vector3[8];

            for (int i = 0; i < 8; i++) corners[i] = GetCorner(i);

            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[3]);
            Gizmos.DrawLine(corners[3], corners[2]);
            Gizmos.DrawLine(corners[2], corners[0]);

            Gizmos.DrawLine(corners[0], corners[4]);
            Gizmos.DrawLine(corners[1], corners[5]);
            Gizmos.DrawLine(corners[3], corners[7]);
            Gizmos.DrawLine(corners[2], corners[6]);

            Gizmos.DrawLine(corners[4], corners[5]);
            Gizmos.DrawLine(corners[5], corners[7]);
            Gizmos.DrawLine(corners[7], corners[6]);
            Gizmos.DrawLine(corners[6], corners[4]);
        }
    }
}