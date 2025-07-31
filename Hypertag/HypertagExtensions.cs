using UnityEngine;

namespace UC
{

    public static class HypertaggedExtension
    {
        public static bool HasHypertag(this GameObject go, Hypertag tag)
        {
            foreach (var obj in go.GetComponents<HypertaggedObject>())
            {
                if (obj.hypertag == tag) return true;
            }

            return false;
        }
        public static bool HasHypertags(this GameObject go, Hypertag[] tags)
        {
            foreach (var obj in go.GetComponents<HypertaggedObject>())
            {
                if (obj.HasAnyHypertag(tags)) return true;
            }

            return false;
        }

        public static T GetComponentInChildrenWithHypertag<T>(this Component go, Hypertag tag) where T : Component
        {
            T obj = go.GetComponentInChildren<T>();
            if (obj == null) return null;

            if (obj.gameObject.HasHypertag(tag)) return obj;

            return null;
        }

        public static T GetComponentInChildrenWithHypertag<T>(this Component go, Hypertag[] tags) where T : Component
        {
            T obj = go.GetComponentInChildren<T>();
            if (obj == null) return null;

            if (obj.gameObject.HasHypertags(tags)) return obj;

            return null;
        }
    }
}