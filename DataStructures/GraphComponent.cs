using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UC
{

    public class GraphComponent : MonoBehaviour
    {
        public enum WeightMode { Constant, Distance, Explicit };

        [SerializeField] private bool directed = false;
        [SerializeField] private WeightMode weightMode;
        [SerializeField, Header("Test data")]
        private List<int> terminalPoints;
        [SerializeField, ShowIf("weightMode", WeightMode.Explicit)]
        private List<float> edgeWeights;

        [SerializeField, Header("Data")]
        private Graph<GraphNodeComponent> graph;

        public Graph<GraphNodeComponent> Graph => graph;

        public bool isDirected => directed;

        [Button("Build from components")]
        public void BuildFromComponents()
        {
            graph = BuildGraphFromChildren();
        }

        Graph<GraphNodeComponent> BuildGraphFromChildren()
        {
            var nodes = GetComponentsInChildren<GraphNodeComponent>();

            var graph = new Graph<GraphNodeComponent>(directed);

            GraphEditor graphEditor = GetComponent<GraphEditor>();

            foreach (GraphNodeComponent node in nodes)
            {
                node.id = graph.Add(node);
            }

            int edgeIndex = 0;
            foreach (GraphNodeComponent node in nodes)
            {
                var links = node.GetLinks();
                if (links != null)
                {
                    foreach (var link in links)
                    {
                        if (link != null)
                        {
                            float w = 1.0f;
                            if (weightMode == WeightMode.Distance) w = Vector3.Distance(node.transform.position, link.transform.position);
                            else if (weightMode == WeightMode.Explicit)
                            {
                                if (graphEditor)
                                {
                                    w = graphEditor.DefaultWeight;
                                }
                                else
                                {
                                    if ((edgeWeights != null) && (edgeWeights.Count > edgeIndex))
                                        w = edgeWeights[edgeIndex];
                                    else
                                        w = float.MaxValue;
                                }
                            }
                            graph.Add(node, link, w);

                            edgeIndex++;
                        }
                    }
                }
            }

            return graph;
        }

        [Button("Build Steiner Tree")]
        public void BuildSteinerTree()
        {
            graph = BuildGraphFromChildren();
            graph = SteinerTree.Build(graph, terminalPoints);
        }

        public int AddNode(GraphNodeComponent node)
        {
            if (graph == null)
            {
                graph = new Graph<GraphNodeComponent>(directed);
            }

            var newId = graph.Add(node);
            node.id = newId;

            return newId;
        }

        private void OnDrawGizmosSelected()
        {
            if (graph != null)
            {
                Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.5f);
                for (int i = 0; i < graph.nodeCount; i++)
                {
                    var node = graph.GetNode(i);
                    if (node == null) continue;

                    Gizmos.DrawSphere(node.transform.position, node.radius);
                    DebugHelpers.DrawTextAt(node.transform.position, Vector3.zero, 16, Color.white, $"{node.id}", true);
                }

                Gizmos.color = Color.cyan;
                for (int j = 0; j < graph.edgeCount; j++)
                {
                    var edge = graph.GetEdge(j);
                    if (edge == null) continue;
                    var n1 = graph.GetNode(edge.i1);
                    var n2 = graph.GetNode(edge.i2);

                    Vector3 delta = n2.transform.position - n1.transform.position;
                    float deltaMag = delta.magnitude;
                    Vector3 dir = delta / deltaMag;

                    Vector3 p1 = n1.transform.position + dir * n1.radius;
                    Vector3 p2 = n2.transform.position - dir * n2.radius;

                    if (graph.isDirected)
                    {
                        Vector3 d = (p2 - p1);
                        float mag = d.magnitude;
                        d /= mag;
                        DebugHelpers.DrawArrow(p1, d, mag, 0.05f * mag, 45.0f, d.PerpendicularXY());
                    }
                    else
                    {
                        Gizmos.DrawLine(p1, p2);
                    }

                    DebugHelpers.DrawTextAt((p1 + p2) * 0.5f, Vector3.zero, 14, Color.blue, $"{edge.weight}", false);
                }
            }
        }
    }
}