using UnityEngine;

public class CubicBezier
{
    public Vector3[] pt;

    public CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        pt = new Vector3[] { p0, p1, p2, p3 };
    }

    public void Split(float t, out CubicBezier firstCurve, out CubicBezier secondCurve)
    {
        BezierHelpers.SplitCubic(pt[0], pt[1], pt[2], pt[3], t, out Vector3[] firstPts, out Vector3[] secondPts);
        firstCurve = new CubicBezier(firstPts[0], firstPts[1], firstPts[2], firstPts[3]);
        secondCurve = new CubicBezier(secondPts[0], secondPts[1], secondPts[2], secondPts[3]);
    }

    public Vector3 Evaluate(float t)
    {
        return BezierHelpers.ComputeCubic(pt, t);
    }

    public Vector3 EvaluateDerivative(float t)
    {
        return BezierHelpers.ComputeCubicDerivative(pt, t);
    }

    public Vector3 GetClosestPoint(Vector3 pt)
    {
        Vector3 closestPoint = Vector3.zero;
        float   closestT = 0.0f;
        
        BezierHelpers.ComputeDistanceFast(this.pt[0], this.pt[1], this.pt[2], this.pt[3], pt, ref closestT, ref closestPoint);
        
        return closestPoint;
    }

    public Vector3 GetClosestPoint(Vector3 pt, out float distance, out float t)
    {
        Vector3 closestPoint = Vector3.zero;
        float closestT = 0.0f;

        distance = BezierHelpers.ComputeDistanceFast(this.pt[0], this.pt[1], this.pt[2], this.pt[3], pt, ref closestT, ref closestPoint);
        t = closestT;

        return closestPoint;
    }
}
