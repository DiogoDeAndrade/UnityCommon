using UnityEditor;
using UnityEngine;

namespace UC
{
    public static class BoxColliderFitContextMenu
    {
        [MenuItem("CONTEXT/BoxCollider/Unity Common/Fit To Child Renderers")]
        private static void FitToChildRenderers(MenuCommand command)
        {
            var box = (BoxCollider)command.context;
            var rootTransform = box.transform;

            var renderers = rootTransform.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                Debug.LogWarning($"No renderers found under {rootTransform.name}.", box);
                return;
            }

            UpdateBounds(box, rootTransform, renderers);
        }

        [MenuItem("CONTEXT/BoxCollider/Unity Common/Fit To Child Renderers", true)]
        private static bool ValidateFitToChildRenderers(MenuCommand command)
        {
            return command.context is BoxCollider;
        }

        [MenuItem("CONTEXT/BoxCollider/Unity Common/Fit To All Renderers")]
        private static void FitToAllRenderers(MenuCommand command)
        {
            var box = (BoxCollider)command.context;
            var rootTransform = box.transform;

            var renderers = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            if (renderers.Length == 0)
            {
                Debug.LogWarning($"No renderers found.", box);
                return;
            }

            UpdateBounds(box, rootTransform, renderers);
        }


        [MenuItem("CONTEXT/BoxCollider/Unity Common/Fit To All Renderers", true)]
        private static bool ValidateFitToAllRenderers(MenuCommand command)
        {
            return command.context is BoxCollider;
        }

        private static void UpdateBounds(BoxCollider box, Transform rootTransform, Renderer[] renderers)
        { 
            bool hasBounds = false;
            Bounds localBounds = new Bounds();

            foreach (var renderer in renderers)
            {
                if (!renderer.enabled)
                    continue;

                Bounds worldBounds = renderer.bounds;

                // Convert the 8 corners of the renderer's world bounds
                // into the BoxCollider's local space.
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;

                Vector3[] corners =
                {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

                foreach (var corner in corners)
                {
                    Vector3 localPoint = rootTransform.InverseTransformPoint(corner);

                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localPoint);
                    }
                }
            }

            if (!hasBounds)
            {
                Debug.LogWarning($"No enabled renderers found.", box);
                return;
            }

            Undo.RecordObject(box, "Fit BoxCollider To Renderers");

            box.center = localBounds.center;
            box.size = localBounds.size;

            EditorUtility.SetDirty(box);
        }

    }
}
