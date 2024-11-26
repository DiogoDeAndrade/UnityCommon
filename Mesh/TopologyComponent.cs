using NaughtyAttributes;
using UnityEditor.TerrainTools;
using UnityEditor;
using UnityEngine;
using System.Dynamic;
using UnityEngine.Tilemaps;
using System;

[ExecuteInEditMode]
public class TopologyComponent : MonoBehaviour
{
    [SerializeField, HideInInspector] 
    TopologyStatic _topology;
    [SerializeField] 
    bool              interaction;
    [SerializeField] 
    bool              displayVertex;
    [SerializeField, ShowIf(nameof(displayVertex))]
    bool              displayVertexLabel = false;
    [SerializeField, ShowIf(nameof(displayVertex))]
    Color             vertexColor = Color.green;
    [SerializeField, ShowIf(nameof(displayVertex))]
    float             vertexRadius = 0.1f;
    [SerializeField] 
    bool              displayEdges;
    [SerializeField, ShowIf(EConditionOperator.And, nameof(displayEdges), nameof(interaction))]
    Color             edgeColor = Color.yellow;
    [SerializeField] 
    bool              displayTriangles;
    [SerializeField] 
    bool              displayTrianglesLabel;
    [SerializeField, ShowIf(EConditionOperator.And, nameof(displayTriangles), nameof(interaction))]
    Color             triangleColor = Color.red;

    public delegate Color CustomColorFunction(int index, TopologyStatic.TVertex vertex, Color originalColor);
    public TopologyStatic topology => _topology;

    public CustomColorFunction customColorFunction;


    [Button("Build")]
    public void Build()
    {
        MeshFilter meshFilter = GetComponentInChildren<MeshFilter>();

        Build(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix);
    }

    public void Build(Mesh mesh, Matrix4x4 matrix)
    {
        MeshFilter meshFilter = GetComponentInChildren<MeshFilter>();

        _topology = new TopologyStatic(mesh, matrix);
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        // Subscribe to the Scene view event
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        // Unsubscribe when disabled
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private TopologyStatic.TVertex hoverVertex;
    
    static private Material          gizmoMaterial;

    private void OnDrawGizmos()
    {
        if (_topology == null) return;

        if (displayVertex)
        {
            for (int i = 0; i < _topology.vertexCount; i++)
            {
                var vertex = _topology.vertices[i];
                Gizmos.color = (hoverVertex == vertex) ? (Color.yellow) : (vertexColor);
                if (customColorFunction != null) Gizmos.color = customColorFunction(i, vertex, Gizmos.color);

                Gizmos.DrawSphere(vertex.position, vertexRadius);

                if (hoverVertex == vertex)
                {
                    if (displayVertexLabel)
                    {
                        DebugHelpers.DrawTextAt(vertex.position, Vector3.zero, 10, Color.white, $"Vertex={i}", true);
                    }
                    if (displayEdges)
                    {
                        Gizmos.color = edgeColor;
                        foreach (var edgeId in vertex.edges)
                        {
                            var edge = _topology.GetEdgeVertex(edgeId);
                            Gizmos.DrawLine(_topology.GetVertexPosition(edge.i1), _topology.GetVertexPosition(edge.i2));
                        }
                    }
                    if (displayTriangles)
                    {
                        if (gizmoMaterial == null)
                        {
                            // Create a simple unlit material for GL
                            Shader shader = Shader.Find("Hidden/Internal-Colored");
                            gizmoMaterial = new Material(shader);
                            gizmoMaterial.hideFlags = HideFlags.HideAndDontSave;
                            gizmoMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            gizmoMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            gizmoMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                            gizmoMaterial.SetInt("_ZWrite", 0);
                        }
                        gizmoMaterial.SetPass(0);

                        GL.Begin(GL.TRIANGLES);
                        GL.Color(triangleColor);

                        foreach (var triangleId in vertex.triangles)
                        {
                            (var v1, var v2, var v3) = _topology.GetTriangle(triangleId);
                            GL.Vertex(v1);
                            GL.Vertex(v2);
                            GL.Vertex(v3);

                            if (displayTrianglesLabel)
                            {
                                DebugHelpers.DrawTextAt((v1 + v2 + v3) / 3.0f, Vector3.zero, 10, Color.white, $"Tri={triangleId}", true);
                            }
                        }

                        GL.End();

                        if (displayTrianglesLabel)
                        {
                            foreach (var triangleId in vertex.triangles)
                            {
                                (var v1, var v2, var v3) = _topology.GetTriangle(triangleId);

                                DebugHelpers.DrawTextAt((v1 + v2 + v3) / 3.0f, Vector3.zero, 10, Color.white, $"Tri={triangleId}", true);
                            }
                        }
                    }
                }
            }
        }
    }

    void OnSceneGUI(SceneView view)
    {
        if ((_topology == null) || (!interaction))
        {
            hoverVertex = null;
            return;
        }    
        if (_topology.vertices == null)
        {
            hoverVertex = null;
            return;
        }

        // Get mouse position in Scene view
        Event e = Event.current;
        if (e != null)
        {
            // Only proceed if the mouse is moving in the scene view
            if (e.type == EventType.MouseMove || e.type == EventType.Repaint)
            {
                hoverVertex = null;

                // Create a ray from the mouse position, and change it to local coordinates
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

                foreach (var vertex in _topology.vertices)
                {
                    if (Sphere.Raycast(ray, vertex.position, vertexRadius, float.MaxValue, out float dist))
                    {
                        hoverVertex = vertex;
                        break;
                    }
                }
            }
        }
    }
#endif

}
