using UnityEngine;

namespace UC
{

    public class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        protected static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance) return _instance;
                _instance = FindFirstObjectByType<T>();
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if ((_instance != null) && (_instance != this))
            {
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}