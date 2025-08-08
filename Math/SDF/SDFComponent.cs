using NaughtyAttributes;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UC
{
    public class SDFComponent : MonoBehaviour
    {
        [SerializeField]
        public SDF      sdf;
        [SerializeField]
        public bool     debugDisplay;
        [SerializeField, ShowIf(nameof(debugDisplay))]
        public bool     debugDisplayOnlyWhenSelected = true;
        [SerializeField]
        public Color    displayColor = Color.yellow;

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (sdf == null) return;
            if (sdf.ownerGameObject == gameObject) return;

            // Make a shallow clone of the SO
            var clone = Instantiate(sdf);
            clone.name = sdf.name;
            clone.ownerGameObject = gameObject;

            // point to the new copy
            sdf = clone;

            // mark the component dirty so the change is saved
            EditorUtility.SetDirty(this);
#endif
        }

        public Bounds GetBounds()
        {
            return sdf.GetBounds();
        }

        public float Sample(Vector3 worldPoint)
        {
            return sdf.Sample(worldPoint);
        }

        public void UpdateArgs()
        {
            // Check special case of composite, and add all children to the parameters of the composite SDF
            if (sdf.needArgs)
            {
                List<SDF> args = new();
                foreach (Transform child in transform)
                {
                    var comp = child.GetComponent<SDFComponent>();
                    if (comp != null) args.Add(comp.sdf);
                }
                sdf.args = args.ToArray();
            }
        }
    }
}
