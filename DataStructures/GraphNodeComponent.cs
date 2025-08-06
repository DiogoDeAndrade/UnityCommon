using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{

    public class GraphNodeComponent : MonoBehaviour, IEquatable<GraphNodeComponent>
    {
        [SerializeField] private int _id;
        [SerializeField] private float _radius = 0.1f;
        [SerializeField] private bool weightIsDistance = false;
        [SerializeField] private GraphNodeComponent[] links;

        public int id { get { return _id; } set { _id = value; } }
        public float radius
        {
            get { return _radius; }
            set { _radius = value; }
        }

        public bool Equals(GraphNodeComponent other)
        {
            return this == other;
        }

        public GraphNodeComponent[] GetLinks() => links;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if ((Selection.activeGameObject != gameObject) && (transform.IsChildOf(Selection.activeGameObject.transform))) return;

            Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.5f);
            Gizmos.DrawSphere(transform.position, radius);
            DebugHelpers.DrawTextAt(transform.position, Vector3.zero, 16, Color.white, $"{_id}", true);

            bool isDirected = false;
            var graphParent = GetComponentInParent<GraphComponent>();
            if (graphParent != null) isDirected = graphParent.isDirected;

            if (links != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var link in links)
                {
                    if (link == null) continue;

                    Vector3 delta = link.transform.position - transform.position;
                    float deltaMag = delta.magnitude;
                    Vector3 dir = delta / deltaMag;

                    Vector3 p1 = transform.position + dir * _radius;
                    Vector3 p2 = link.transform.position - dir * link._radius;

                    if (isDirected)
                    {
                        Vector3 d = (p2 - p1);
                        float mag = d.magnitude;
                        d /= mag;
                        DebugHelpers.DrawArrow(p1, d, mag, 0.05f * mag, d.PerpendicularXY().normalized);
                    }
                    else
                    {
                        Gizmos.DrawLine(p1, p2);
                    }

                    if (weightIsDistance)
                    {
                        float w = deltaMag;
                        DebugHelpers.DrawTextAt((p1 + p2) * 0.5f, Vector3.zero, 14, Color.blue, $"{w}", false);
                    }
                }
            }

            if (Selection.activeGameObject == gameObject)
            {
                // Draw all siblings
                var parent = transform.parent;
                if (parent)
                {
                    var graphNodes = parent.GetComponentsInChildren<GraphNodeComponent>();
                    foreach (var graphNode in graphNodes)
                    {
                        if (graphNode != this)
                        {
                            graphNode.OnDrawGizmosSelected();
                        }
                    }
                }
            }
        }
#endif
    }
}