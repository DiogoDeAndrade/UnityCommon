using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UC
{

    public class HypertaggedObject : MonoBehaviour
    {
        public Hypertag hypertag;

        void Awake()
        {
            AddToHypertagList(this);
        }

        private void OnDestroy()
        {
            RemoveFromHypertagList(this);
        }

        public bool HasAnyHypertag(IEnumerable<Hypertag> hypertags)
        {
            foreach (var t in hypertags)
                if (hypertag == t) return true;

            return false;
        }
        public bool HasAnyHypertag(Hypertag[] hypertags)
        {
            foreach (var t in hypertags)
                if (hypertag == t) return true;

            return false;
        }

        static Dictionary<Hypertag, List<HypertaggedObject>> allHypertagObjects = new();

        static void AddToHypertagList(HypertaggedObject obj)
        {
            if (obj.hypertag == null) return;

            if (allHypertagObjects.TryGetValue(obj.hypertag, out var l))
            {
                l.Add(obj);
                return;
            }

            allHypertagObjects[obj.hypertag] = l = new();
            l.Add(obj);
        }

        static void RemoveFromHypertagList(HypertaggedObject obj)
        {
            if ((obj.hypertag != null) && (allHypertagObjects.TryGetValue(obj.hypertag, out var l)))
            {
                l.Remove(obj);
                return;
            }
        }

        public static IEnumerable<HypertaggedObject> Get(Hypertag tag)
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
                foreach (var obj in editorL)
                {
                    if (obj.hypertag == tag) yield return obj;
                }
                yield break;
            }
#endif

            if (allHypertagObjects.TryGetValue(tag, out var l))
            {
                foreach (var obj in l)
                {
                    yield return obj;
                }
            }
        }

        public static IEnumerable<HypertaggedObject> Get(IEnumerable<Hypertag> tags)
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
                foreach (var obj in editorL)
                {
                    if (!obj.HasAnyHypertag(tags)) continue;

                    yield return obj;
                }
                yield break;
            }
#endif

            foreach (var tag in tags)
            {
                if (allHypertagObjects.TryGetValue(tag, out var l))
                {
                    foreach (var obj in l)
                    {
                        yield return obj;
                    }
                }
            }
        }

        public static T GetFirstOrDefault<T>(Hypertag tag) where T : Component
        {
            foreach (var t in Get<T>(tag))
            {
                return t;
            }
            return default(T);
        }

        public static IEnumerable<T> Get<T>(Hypertag tag) where T : Component
        {
            if (tag == null) yield break;
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
                foreach (var obj in editorL)
                {
                    if (obj.hypertag != tag) continue;

                    var c = obj.GetComponent<T>();
                    if (c) yield return c;
                }
                yield break;
            }
#endif

            if (allHypertagObjects.TryGetValue(tag, out var l))
            {
                foreach (var obj in l)
                {
                    var c = obj.GetComponent<T>();
                    if (c) yield return c;
                }
            }
        }

        public static IEnumerable<T> Get<T>(IEnumerable<Hypertag> tags) where T : Component
        {
            foreach (var tag in tags)
            {
                foreach (var obj in Get(tag))
                {
                    var c = obj.GetComponent<T>();
                    if (c) yield return c;
                }
            }
        }

        public static IEnumerable<T> GetInRadius<T>(Hypertag tag, Vector3 position, float radius) where T : Component
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
                foreach (var obj in editorL)
                {
                    if (obj.hypertag != tag) continue;
                    if (Vector3.Distance(position, obj.transform.position) < radius)
                    {
                        var c = obj.GetComponent<T>();
                        if (c) yield return c;
                    }
                }
                yield break;
            }
#endif

            if (allHypertagObjects.TryGetValue(tag, out var l))
            {
                foreach (var obj in l)
                {
                    if (Vector3.Distance(position, obj.transform.position) < radius)
                    {
                        var c = obj.GetComponent<T>();
                        if (c) yield return c;
                    }
                }
            }
        }

        public static IEnumerable<T> GetInRadius<T>(Hypertag tag, Vector2 position, float radius) where T : Component
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
                foreach (var obj in editorL)
                {
                    if (obj.hypertag != tag) continue;
                    if (Vector2.Distance(position, obj.transform.position) < radius)
                    {
                        var c = obj.GetComponent<T>();
                        if (c) yield return c;
                    }
                }
                yield break;
            }
#endif
            if (allHypertagObjects.TryGetValue(tag, out var l))
            {
                foreach (var obj in l)
                {
                    if (Vector2.Distance(position, obj.transform.position) < radius)
                    {
                        var c = obj.GetComponent<T>();
                        if (c) yield return c;
                    }
                }
            }
        }

        public static IEnumerable<T> GetInRadius<T>(IEnumerable<Hypertag> tags, Vector2 position, float radius) where T : Component
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
                foreach (var obj in editorL)
                {
                    if (!obj.HasAnyHypertag(tags)) continue;
                    if (Vector2.Distance(position, obj.transform.position) < radius)
                    {
                        var c = obj.GetComponent<T>();
                        if (c) yield return c;
                    }
                }
                yield break;
            }
#endif

            foreach (var tag in tags)
            {
                if (allHypertagObjects.TryGetValue(tag, out var l))
                {
                    foreach (var obj in l)
                    {
                        if (Vector2.Distance(position, obj.transform.position) < radius)
                        {
                            var c = obj.GetComponent<T>();
                            if (c) yield return c;
                        }
                    }
                }
            }
        }

        public static IEnumerable<T> GetInRadius<T>(IEnumerable<Hypertag> tags, Vector3 position, float radius) where T : Component
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
                foreach (var obj in editorL)
                {
                    if (!obj.HasAnyHypertag(tags)) continue;
                    if (Vector3.Distance(position, obj.transform.position) < radius)
                    {
                        var c = obj.GetComponent<T>();
                        if (c) yield return c;
                    }
                }
                yield break;
            }
#endif

            foreach (var tag in tags)
            {
                if (allHypertagObjects.TryGetValue(tag, out var l))
                {
                    foreach (var obj in l)
                    {
                        if (Vector3.Distance(position, obj.transform.position) < radius)
                        {
                            var c = obj.GetComponent<T>();
                            if (c) yield return c;
                        }
                    }
                }
            }
        }

        public static bool CheckTags(Component component, Hypertag[] tags, bool includeParent = true)
        {
            HypertaggedObject[] selfTags;

            if (includeParent)
            {
                selfTags = component.GetComponentsInParent<HypertaggedObject>();
                foreach (var t in selfTags)
                {
                    if (t.HasAnyHypertag(tags)) return true;
                }
            }

            selfTags = component.GetComponents<HypertaggedObject>();
            foreach (var t in selfTags)
            {
                if (t.HasAnyHypertag(tags)) return true;
            }

            return false;
        }
    }
}