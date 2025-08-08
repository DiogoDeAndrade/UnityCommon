using NaughtyAttributes;
using UnityEngine;

namespace UC
{

    public class SDFMesher : MonoBehaviour
    {
        [SerializeField]
        private SDFComponent        sdf;
        [SerializeField]
        private float               voxelsPerUnit = 1.0f;
        [SerializeField]
        private bool                debugEnabled;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool                debugShowGrid;
        [SerializeField, ShowIf(nameof(isShowGrid))]
        private bool                debugShowGridLines;
        [SerializeField, ShowIf(nameof(isShowGrid))]
        private Gradient            debugColorRange;
        [SerializeField, ShowIf(nameof(isShowGrid))]
        private Vector2             debugFilterRange;

        [SerializeField, HideInInspector]
        private VoxelData<float> voxelData;
        [SerializeField, ReadOnly]
        private Vector2          distanceRange;

        bool isShowGrid => debugEnabled && debugShowGrid;


        [Button("Build")]
        public void Build()
        {
            // Create the voxel field
            Bounds bounds = sdf.GetBounds();

            Vector3Int gs = new Vector3Int(Mathf.CeilToInt(bounds.size.x * voxelsPerUnit) + 1,
                                           Mathf.CeilToInt(bounds.size.y * voxelsPerUnit) + 1,
                                           Mathf.CeilToInt(bounds.size.z * voxelsPerUnit) + 1);

            voxelData = new VoxelData<float>();
            voxelData.Init(gs, Vector3.one / voxelsPerUnit);
            voxelData.minBound = bounds.min;

            distanceRange = new Vector2(-1.0f, 1.0f);

            SampleSDF();
        }

        [Button("Reset Debug Filter Range")]
        public void ResetRange()
        {
            debugFilterRange = distanceRange;
        }

        void SampleSDF()
        {
            Vector3Int gs = voxelData.gridSize;
            for (int x = 0; x < gs.x; x++)
            {
                for (int y = 0; y < gs.y; y++)
                {
                    for (int z = 0; z < gs.z; z++)
                    {
                        var worldPoint = GetPos(x, y, z);
                        var value = sdf.Sample(worldPoint);
                        if (value < distanceRange.x) distanceRange.x = value;
                        if (value > distanceRange.y) distanceRange.y = value;

                        voxelData[x, y, z] = value;
                    }
                }
            }
        }

        private Vector3 GetPos(int x, int y, int z) => new Vector3(voxelData.minBound.x + x * voxelData.voxelSize.x,
                                                                   voxelData.minBound.y + y * voxelData.voxelSize.y,
                                                                   voxelData.minBound.z + z * voxelData.voxelSize.z);

        private void OnDrawGizmos()
        {
            if (!debugEnabled || !debugShowGrid || voxelData == null)
                return;

            Vector3Int gs = voxelData.gridSize;

            // 1) Draw grid lines by marching between corner points:

            if (debugShowGridLines)
            {
                Gizmos.color = Color.gray;

                // — lines along Z (vary x,y, connect z=0 -> z=gs.z-1)
                for (int x = 0; x < gs.x; x++)
                {
                    for (int y = 0; y < gs.y; y++)
                    {
                        Vector3 p1 = GetPos(x, y, 0);
                        Vector3 p2 = GetPos(x, y, gs.z - 1);
                        Gizmos.DrawLine(p1, p2);
                    }
                }

                // — lines along Y (vary x,z, connect y=0 -> y=gs.y-1)
                for (int x = 0; x < gs.x; x++)
                {
                    for (int z = 0; z < gs.z; z++)
                    {
                        Vector3 p1 = GetPos(x, 0, z);
                        Vector3 p2 = GetPos(x, gs.y - 1, z);
                        Gizmos.DrawLine(p1, p2);
                    }
                }

                // — lines along X (vary y,z, connect x=0 -> x=gs.x-1)
                for (int y = 0; y < gs.y; y++)
                {
                    for (int z = 0; z < gs.z; z++)
                    {
                        Vector3 p1 = GetPos(0, y, z);
                        Vector3 p2 = GetPos(gs.x - 1, y, z);
                        Gizmos.DrawLine(p1, p2);
                    }
                }
            }
            // 2) Draw a small colored sphere at each corner sample:
            float sphereRadius = voxelData.voxelSize.magnitude * 0.20f;
            for (int x = 0; x < gs.x; x++)
            {
                for (int y = 0; y < gs.y; y++)
                {
                    for (int z = 0; z < gs.z; z++)
                    {
                        float val = voxelData[x, y, z];
                        if ((val >= debugFilterRange.x) && (val <= debugFilterRange.y))
                        {
                            float t = Mathf.InverseLerp(distanceRange.x, distanceRange.y, val);
                            Color c = debugColorRange.Evaluate(t);

                            Gizmos.color = c;
                            Gizmos.DrawSphere(GetPos(x, y, z), sphereRadius);
                        }
                    }
                }
            }
        }
    }
}