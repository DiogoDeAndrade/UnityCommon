using System.Collections.Generic;
using System.Linq;
using Unity.Hierarchy;
using UnityEngine;

public interface ICompareIndices
{
    public bool             isLess(int index1, int index2);
    public bool             isLess(int index1, float value);
    public float            GetValue(int index);
    public List<Polyline>   ComputeContours(float refValue, bool onlySaddle);
}

[System.Serializable]
public class LevelSetDiagram
{
    [SerializeField] private ICompareIndices    _comparisonOperator;
    [SerializeField] private TopologyStatic     _topology;
    public class SingleContour
    {
        public int      sadleVertex;
        public Polyline polyline;
    }
    public class MultiContour
    {
        public float        lRef;
        public List<int>    contours;
    }
    private List<SingleContour> _contourList;
    private List<MultiContour>  _contours;
    //private List<float>   _vertexIndexNumber;

    public ICompareIndices comparisonOperator
    {
        get { return _comparisonOperator; }
        set { _comparisonOperator = value; }
    }
    public TopologyStatic topology
    {
        get { return _topology; }
        set { _topology = value; }
    }
    public List<MultiContour> contours => _contours;
    
    class TreeNode
    {
        public TreeNode(Vector3 p) { pos = p; children = null; }

        public bool isLeaf => (children == null);
        public void AddChild(TreeNode node) { if (children == null) children = new(); children.Add(node); }

        public Vector3          pos;
        public List<TreeNode>   children;
    }

    public void Build()
    {
        if (_comparisonOperator == null) return;

        //ComputeVertexIndexNumbers();

        _contourList = new List<SingleContour>();
        _contours = new List<MultiContour>();

        List<int> indices = Enumerable.Range(0, _topology.vertexCount).ToList();

        indices.Sort((index1, index2) =>
            _comparisonOperator.isLess(index1, index2) ? -1 :
            _comparisonOperator.isLess(index2, index1) ? 1 : 0);

        int vertexCount = indices.Count;
        List<bool> processedVertex = new(Enumerable.Repeat(false, vertexCount));
        List<int> Q = new() { indices[0] };
        List<float> d = new(Enumerable.Repeat(float.MaxValue, vertexCount));
        d[indices[0]] = 0;

        Vector3  rootPos = _topology.GetVertexPosition(indices[0]);
        TreeNode root = new(rootPos);
        _contourList.Add(new SingleContour
        {
            sadleVertex = indices[0],
            polyline = new Polyline(rootPos)
        });
        _contours.Add(new MultiContour
        {
            lRef = 0,
            contours = new() { 0 }
        });

        int nc = 1;
        List<int> c = new(Enumerable.Repeat(-1, vertexCount));
        c[indices[0]] = 0;

        while (Q.Count > 0)
        {
            // Sort Q to make it a priority list
            Q.Sort((index1, index2) => _comparisonOperator.isLess(index1, index2) ? -1 : _comparisonOperator.isLess(index2, index1) ? 1 : 0);

            var vIndex = Q[0]; Q.RemoveAt(0);
            processedVertex[vIndex] = true;
            // Get neighbours to ensure they will be processed later
            var neighbours = _topology.GetVertexNeighbours(vIndex);
            foreach (var n in neighbours)
            {
                if (processedVertex[n]) continue;
                float newDist = d[vIndex] + Vector3.Distance(_topology.GetVertexPosition(vIndex), _topology.GetVertexPosition(n));
                if (Q.IndexOf(n) == -1)
                {
                    c[n] = c[vIndex];
                    d[n] = newDist;
                    Q.Add(n);
                }
                else 
                {
                    if (newDist < d[n])
                    {
                        c[n] = c[vIndex];
                        d[n] = newDist;
                    }
                }
            }
            float vin = ComputeVertexIndexNumber(vIndex);
            if (vin == 1.0f)
            {
                // Insert line (CT[c(v)], v) into Ts(P) with v as a leaf
            }
            else if (vin < 0.0f) 
            {
                // Insert line (CT[c(v)], v) into Ts(P)
                var contours = _comparisonOperator.ComputeContours(d[vIndex], true);
                // There should be (1 - vin) contours which involve the saddle vertex
                int vertexContour = c[vIndex];
                int nBase = _contourList.Count;
                int nCountours = contours.Count;
                _contourList[vertexContour].polyline = contours[0];
                for (int j = 1; j < nCountours; ++j) 
                {
                    _contourList.Add(new SingleContour
                    {
                        sadleVertex = vIndex,
                        polyline = contours[j]
                    });
                }
                nc = nc - (int)vin;

                int a = 10;
            }
        }

        /*float refValue = 0.0f;
        int   refIndex = 0;
        int   actualIndex;
        float prevValue = -float.MaxValue;
        while (refIndex < indices.Count)
        {
            actualIndex = indices[refIndex];
            refValue = _comparisonOperator.GetValue(actualIndex);
            //if (Mathf.Abs(refValue - prevValue) > 1.0f)       // Contour every 1.0f unit of distance
            if (_vertexIndexNumber[actualIndex] != 0)
            {
                var lines = _comparisonOperator.ComputeContour(refValue);
                if ((refIndex != 0) && (lines.Count == 0)) break;

                _contours.Add(new MultiContour
                {
                    lRef = refValue,
                    contours = lines
                });

                prevValue = refValue;
            }

            refIndex++;
        }*/
    }

    /*public void ComputeVertexIndexNumbers()
    {
        _vertexIndexNumber = new List<float>();
        for (int i = 0; i < topology.vertexCount; i++)
        {
            float vertexIndexNumber = ComputeVertexIndexNumber(i);
            _vertexIndexNumber.Add(vertexIndexNumber);
        }
    }*/

    public float ComputeVertexIndexNumber(int index)
    {
        var neighbours = topology.GetVertexNeighbours(index);
        var centerPos = topology.GetVertexPosition(index);
        var centerNormal = topology.GetVertexNormal(index);

        Vector3 toFirstNeighbor = (topology.GetVertexPosition(neighbours.First()) - centerPos).normalized;
        Vector3 right = Vector3.Cross(centerNormal, toFirstNeighbor).normalized;
        Vector3 forward = Vector3.Cross(right, centerNormal).normalized;

        var sortedNeighbours = neighbours.OrderBy(neighbour =>
        {
            Vector3 direction = (topology.GetVertexPosition(neighbour) - centerPos).normalized;

            // Project direction onto the reference plane
            float x = Vector3.Dot(direction, right);
            float y = Vector3.Dot(direction, forward);

            // Compute angle in radians
            return Mathf.Atan2(y, x);
        }).ToList();

        int signChanges = 0;
        for (int i = 0; i < sortedNeighbours.Count; i++)
        {
            int i1 = sortedNeighbours[i];
            int i2 = sortedNeighbours[(i + 1) % sortedNeighbours.Count];
            int s1 = _comparisonOperator.isLess(i1, index) ? (-1) : (1);
            int s2 = _comparisonOperator.isLess(i2, index) ? (-1) : (1);
            if (s1 != s2) signChanges++;
        }

        return 1.0f - signChanges / 2.0f;
    }
}
