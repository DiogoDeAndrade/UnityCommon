using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

public class VoxelCollider : MonoBehaviour
{
    [SerializeField] private VoxelTree  _voxelTree;

    [SerializeField] private bool       debugRender;
    [SerializeField, ShowIf("debugRender")] 
    private int debugRenderLevel = 0;

    public VoxelTree voxelTree
    {
        get { return _voxelTree; }
        set { _voxelTree = value; }
    }

    private void OnDrawGizmosSelected()
    {
        if (_voxelTree == null) return;

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
