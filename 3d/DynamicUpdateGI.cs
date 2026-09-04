using UnityEngine;
using NaughtyAttributes;

namespace UC
{
    public class DynamicUpdateGI : Singleton<DynamicUpdateGI>
    {
        [SerializeField] private bool updateAllFrames = true;
        [SerializeField] private bool updateReflectionProbes = true;
        [SerializeField, ShowIf(nameof(updateReflectionProbes))] private ReflectionProbe[] reflectionProbes;

        void Update()
        {
            if (updateAllFrames)
            {
                UpdateEnv();
            }
        }

        [Button("Update Now")]
        void UpdateEnv()
        { 
            DynamicGI.UpdateEnvironment();

            if ((reflectionProbes != null) && (updateReflectionProbes))
            {
                foreach (var probe in reflectionProbes)
                {
                    if (probe.isActiveAndEnabled)
                        probe.RenderProbe();
                }
            }
        }

        static public void UpdateEnvironment()
        {
            Instance?.UpdateEnv();
        }
    }
}
