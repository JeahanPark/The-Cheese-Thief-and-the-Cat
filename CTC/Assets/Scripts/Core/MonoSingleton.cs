// WES에서 이식(2026-07-12)
using UnityEngine;

namespace CTC.Runtime.Core
{
    public abstract class MonoSingleton<T> : MonoBehaviour, IManager where T : MonoBehaviour
    {
        private static T s_Instance;

        public static T Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindFirstObjectByType<T>();
                    if (s_Instance == null)
                    {
                        GameObject go = new GameObject(typeof(T).Name);
                        s_Instance = go.AddComponent<T>();
                    }
                }
                return s_Instance;
            }
        }

        protected virtual void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this as T;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (s_Instance == this)
            {
                Clear();
                s_Instance = null;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        public virtual void Init()
        {
        }

        public virtual void Clear()
        {
        }
    }
}
