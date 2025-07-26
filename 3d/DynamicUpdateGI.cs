using UnityEngine;

namespace UC
{
    public class DynamicUpdateGI : MonoBehaviour
    {
        [SerializeField] private bool updateAllFrames = true;
        [SerializeField] private ReflectionProbe[] reflectionProbes;

        static DynamicUpdateGI Instance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            if (updateAllFrames)
            {
                UpdateEnv();
            }
        }

        void UpdateEnv()
        { 
            DynamicGI.UpdateEnvironment();

            if (reflectionProbes != null)
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
