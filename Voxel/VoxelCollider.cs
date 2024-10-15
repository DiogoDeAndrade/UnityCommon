using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

public class VoxelCollider : MonoBehaviour
{
    [SerializeField] private VoxelTree  _voxelTree;

    [SerializeField] private bool       displayVoxelCollider = false;
    [SerializeField] private bool       debugRender;
    [SerializeField, ShowIf("debugRender")] 
    private int debugRenderLevel = 0;

    public VoxelTree voxelTree
    {
        get { return _voxelTree; }
        set { _voxelTree = value; }
    }

    public bool Intersect(VoxelCollider otherVoxelCollider, float scale1 = 1.0f, float scale2 = 1.0f)
    {
        OBB obb1 = null;
        OBB obb2 = null;
        
        return _voxelTree.Intersect(transform, otherVoxelCollider._voxelTree, otherVoxelCollider.transform, ref obb1, ref obb2, scale1, scale2);
    }

    private void OnDrawGizmosSelected()
    {
        if (_voxelTree == null) return;
        if (!displayVoxelCollider) return;

        var prevMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (debugRender)
        {
            _voxelTree.DrawGizmoBounds(debugRenderLevel);
        }
        else
        {
            _voxelTree.DrawGizmo();
        }

        Gizmos.matrix = prevMatrix;
    }
}
