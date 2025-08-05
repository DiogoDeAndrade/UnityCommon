using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using NaughtyAttributes;
#endif

namespace UC
{
    [ExecuteAlways]
    public class GraphEditor : MonoBehaviour
    {
        [SerializeField] private float radiusScale = 1.0f;
        
        [SerializeField, Tooltip("Radius for newly created nodes")]
        private float defaultRadius = 0.1f;

        [SerializeField, Tooltip("Weight to assign to links when using Constant mode")]
        private float defaultLinkWeight = 1.0f;

        public float DefaultRadius => defaultRadius;
        public float DefaultWeight => defaultLinkWeight;

        public List<GraphNodeComponent> Nodes
        {
            get
            {
                List<GraphNodeComponent> list = new();
                foreach (Transform child in transform)
                {
                    if (child.TryGetComponent(out GraphNodeComponent n))
                        list.Add(n);
                }
                return list;
            }
        }

        public GraphComponent GraphComponent => GetComponent<GraphComponent>();

#if UNITY_EDITOR
        private void OnEnable()
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        private void OnDisable()
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void OnDrawGizmosSelected()
        {
            var nodes = Nodes;
            foreach (var node in nodes)
            {
                if (node == null) continue;

                Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.5f);
                Gizmos.DrawSphere(node.transform.position, radiusScale * node.radius);
                DebugHelpers.DrawTextAt(node.transform.position, Vector3.zero, 16, Color.white, $"{node.id}", true);

                if (node.GetLinks() != null)
                {
                    bool isDirected = GraphComponent?.isDirected ?? false;
                    Gizmos.color = Color.cyan;

                    foreach (var target in node.GetLinks())
                    {
                        if (target == null) continue;

                        Vector3 delta = target.transform.position - node.transform.position;
                        float deltaMag = delta.magnitude;
                        Vector3 dir = delta / deltaMag;

                        Vector3 p1 = node.transform.position + dir * radiusScale * node.radius;
                        Vector3 p2 = target.transform.position - dir * radiusScale * target.radius;

                        if (isDirected)
                        {
                            Vector3 d = p2 - p1;
                            float mag = d.magnitude;
                            d.Normalize();
                            DebugHelpers.DrawArrow(p1, d, mag, 0.05f * mag, d.PerpendicularXY());
                        }
                        else
                        {
                            Gizmos.DrawLine(p1, p2);
                        }
                    }
                }

            }
        }

        [ContextMenu("Add Node")]
        [Button("Add Node")]
        public void AddNode()
        {
            var graphComponent = GetComponent<GraphComponent>();
            if (graphComponent == null)
            {
                Debug.LogWarning("GraphComponent not found on this GameObject.");
                return;
            }

            // Create new GameObject
            GameObject nodeObj = new GameObject($"Node_{transform.childCount}");
            nodeObj.transform.SetParent(transform);
            nodeObj.transform.localPosition = Vector3.zero;

            // Add GraphNodeComponent
            var node = nodeObj.AddComponent<GraphNodeComponent>();

            // Register with graph and assign ID
            int newId = graphComponent.AddNode(node);
            node.id = newId;
            node.radius = DefaultRadius;

            // Mark dirty to save
            EditorUtility.SetDirty(graphComponent);
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);

            // Select and frame in scene view
            Selection.activeGameObject = nodeObj;
            SceneView.lastActiveSceneView.FrameSelected();
        }

#endif
    }
}
