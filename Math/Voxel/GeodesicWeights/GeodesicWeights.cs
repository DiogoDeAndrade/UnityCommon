using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    static public class GeodesicWeights
    {
        public static VoxelData<byte> Voxelize(List<Mesh> meshes, List<Matrix4x4> transforms, float voxelSize)
        {
            var target = new AxisSlice.VolumeDef();
            target.bounds = GetWorldBounds(meshes, transforms);
            target.origin = target.bounds.min;

            Vector3 size = target.bounds.size;
            int nx = Mathf.CeilToInt(size.x / voxelSize);
            int ny = Mathf.CeilToInt(size.y / voxelSize);
            int nz = Mathf.CeilToInt(size.z / voxelSize);
            target.dims = new Vector3Int(nx, ny, nz);

            Vector3[] directions = new Vector3[6]
            {
                Vector3.forward, Vector3.back,
                Vector3.right, Vector3.left,
                Vector3.up, Vector3.down,
            };

            AxisSlice xp = null, xn = null, yp = null, yn = null, zp = null, zn = null;

            foreach (var dir in directions)
            {
                var sliceRenderer = new AxisSlice(voxelSize, dir, false);
                sliceRenderer.AddMesh(meshes, transforms);
                sliceRenderer.SetVolume(target);
                sliceRenderer.Render();

                if (dir == Vector3.right) xp = sliceRenderer;
                else if (dir == Vector3.left) xn = sliceRenderer;
                else if (dir == Vector3.up) yp = sliceRenderer;
                else if (dir == Vector3.down) yn = sliceRenderer;
                else if (dir == Vector3.forward) zp = sliceRenderer;
                else if (dir == Vector3.back) zn = sliceRenderer;
            }

            var axisX = BitVolume.And(xp.sliceData, xn.sliceData);
            var axisY = BitVolume.And(yp.sliceData, yn.sliceData);
            var axisZ = BitVolume.And(zp.sliceData, zn.sliceData);

            var insideFinal = BitVolume.Majority3(axisX, axisY, axisZ);

            // Convert final bit volume to VoxelData<byte>
            var vox = new VoxelData<byte>();
            vox.Init(target.dims, new Vector3(voxelSize, voxelSize, voxelSize));
            vox.minBound = target.origin;

            FillVoxelDataFromBits(vox, insideFinal, filledValue: 1, emptyValue: 0);

            return vox;
        }

        static void FillVoxelDataFromBits(VoxelData<byte> dst, BitVolume bits, byte filledValue, byte emptyValue)
        {
            if (dst.gridSize.x != bits.width || dst.gridSize.y != bits.height || dst.gridSize.z != bits.depth)
                throw new System.Exception("VoxelData and BitVolume dimensions mismatch.");

            // Fast path: write sequentially in the same flat order as BitVolume uses
            // BitVolume index: (z*H + y)*W + x
            // VoxelData index: x + y*W + z*W*H  -> same ordering
            var words = bits.RawWords;
            int w = bits.width;
            int h = bits.height;
            int d = bits.depth;

            int total = w * h * d;

            int voxelIndex = 0;
            int wordCount = words.Length;

            for (int wi = 0; wi < wordCount; wi++)
            {
                ulong word = words[wi];

                // process up to 64 voxels, but stop at total
                int n = System.Math.Min(64, total - voxelIndex);
                for (int b = 0; b < n; b++)
                {
                    bool inside = ((word >> b) & 1UL) != 0;
                    dst.data[voxelIndex++] = inside ? filledValue : emptyValue;
                }
            }
        }

        static public Bounds GetWorldBounds(List<Mesh> meshes, List<Matrix4x4> transforms)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            for (int i = 0; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                var b = mesh.bounds.ToWorld(transforms[i]);

                // Convert bounds to be relative to this one
                if (first)
                {
                    bounds = b;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            return bounds;
        }
    }
}
