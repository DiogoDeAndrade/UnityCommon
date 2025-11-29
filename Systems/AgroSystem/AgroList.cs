using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using UC.RPG;

namespace UC
{
    public class AgroList : MonoBehaviour
    {
        [SerializeField]
        private float       refreshTime = 1.0f;
        [SerializeField]
        private float       agroDecayPerTime = 0.0f;
        [SerializeField]
        private float       agroDecayPerDistance = 0.0f;
        [SerializeField, ShowIf(nameof(hasDecayPerDistance))]
        private bool        is3D = false;
        [SerializeField, ShowIf(nameof(hasDecayPerDistance))]
        private float       agroMaxDistance = 1000.0f;
        [SerializeField, ShowIf(nameof(hasDecayPerDistance))]
        private bool        useLoS;
        [SerializeField]
        private float       agroGainOnDamage = 1.0f;

        [System.Serializable]                                     
        class AgroElem
        {
            public GameObject   agroTarget;
            public float        agroValue;
        }
        public delegate bool FilterFunction(GameObject target, float value);

        private List<AgroElem>  agroList;
        private bool            dirty = false;
        private float           refreshTimer = 0;

        bool hasDecayPerDistance => agroDecayPerDistance > 0.0f;

        private void OnEnable()
        {
            if (agroGainOnDamage > 0.0f)
            {
                ResourceHandler health = this.FindResourceHandler(GlobalsBase.healthResource);
                if (health)
                    health.onChange += OnDamage_AddAgro;
            }
        }
        private void OnDisable()
        {
            if (agroGainOnDamage > 0.0f)
            {
                ResourceHandler health = this.FindResourceHandler(GlobalsBase.healthResource);
                if (health)
                    health.onChange -= OnDamage_AddAgro;
            }
        }

        private void OnDamage_AddAgro(ResourceInstance resourceInstance, ChangeData changeData)
        {
            if (changeData.deltaValue < 0.0f)
                AddAgro(changeData.source, -changeData.deltaValue * agroGainOnDamage);
        }

        public void AddAgro(GameObject source, float agro)
        {
            if (source == null) return;
            if (agro == 0.0f) return;
            if (agroList == null) agroList = new();

            foreach (var elem in agroList)
            {
                if (elem.agroTarget == source)
                {
                    elem.agroValue += agro;
                    dirty = true;
                    return;
                }
            }

            agroList.Add(new AgroElem
            {
                agroTarget = source,
                agroValue = agro
            });
            dirty = true;
        }
        public void SetAgro(GameObject source, float value)
        {
            if (agroList != null)
            {
                foreach (var elem in agroList)
                {
                    if (elem.agroTarget == source)
                    {
                        elem.agroValue = value;
                        dirty = true;
                        return;
                    }
                }
            }
            else agroList = new();

                agroList.Add(new AgroElem
                {
                    agroTarget = source,
                    agroValue = value
                });
            dirty = true;
        }

        public float GetAgro(GameObject source)
        {
            if (agroList != null)
            {
                foreach (var elem in agroList)
                {
                    if (elem.agroTarget == source)
                    {
                        return elem.agroValue;
                    }
                }
            }

            return 0.0f;
        }

        public GameObject GetTop(FilterFunction filter = null)
        {
            if (agroList == null) return null;
            if (agroList.Count == 0) return null;

            foreach (var elem in agroList)
            {
                if (elem.agroTarget == null) continue;
                if (filter == null) return elem.agroTarget;
                if (filter(elem.agroTarget, elem.agroValue))
                {
                    return elem.agroTarget;
                }
            }
            return null;
        }

        void Sort()
        {
            agroList?.Sort((e1, e2) => e2.agroValue.CompareTo(e1.agroValue));
            dirty = false;
        }

        private void Update()
        {
            if (((agroDecayPerDistance > 0.0f) || (agroDecayPerTime > 0.0f)) && (agroList != null))
            {
                if (agroDecayPerTime > 0.0f)
                {
                    foreach (var elem in agroList)
                    {
                        elem.agroValue -= agroDecayPerTime * Time.deltaTime;
                    }
                }
                if (agroDecayPerDistance > 0.0f)
                {
                    foreach (var elem in agroList)
                    {
                        var toAgroTarget = transform.position - elem.agroTarget.transform.position;
                        if (!is3D) toAgroTarget.z = 0.0f;
                        float dist = 1.0f - Mathf.Clamp01(toAgroTarget.magnitude / agroMaxDistance);

                        if (useLoS)
                        {
                            // Check if there's LoS
                            bool los = false;
                            if (is3D)
                            {
                                los = !Physics.Raycast(transform.position, toAgroTarget.normalized, toAgroTarget.magnitude, GlobalsBase.obstacleMask);
                            }
                            else
                            {
                                los = !Physics2D.Raycast(transform.position, toAgroTarget.normalized, toAgroTarget.magnitude, GlobalsBase.obstacleMask);
                            }
                            if (!los) dist = 1.0f;
                        }

                        elem.agroValue -= dist * agroDecayPerDistance * Time.deltaTime;
                    }
                }

                agroList.RemoveAll((elem) => elem.agroValue < 0.0f);
                dirty = true;
            }

            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0.0f)
            {
                if (dirty) Sort();

                refreshTimer = refreshTime;
            }
        }
    }
}