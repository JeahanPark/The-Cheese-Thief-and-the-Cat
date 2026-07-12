// WES에서 이식(2026-07-12)
using UnityEngine;
using CTC.Runtime.Core;

namespace CTC.Runtime.Controller
{
    /// <summary>
    /// Base class for all scene controllers
    /// Provides singleton-like access within a scene (not persistent across scenes)
    /// </summary>
    public abstract class GameController<T> : MonoBehaviour where T : GameController<T>
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
                        GameDebug.LogError($"[GameController] No instance of {typeof(T).Name} found in scene!");
                    }
                }
                return s_Instance;
            }
        }

        protected virtual void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                GameDebug.LogWarning($"[GameController] Multiple instances of {typeof(T).Name} detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            s_Instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }
    }
}
