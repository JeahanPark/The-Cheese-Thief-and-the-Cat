// WES에서 이식(2026-07-12)
using Unity.Netcode;
using UnityEngine;
using CTC.Runtime.Core;

namespace CTC.Runtime.Controller
{
    /// <summary>
    /// 네트워크 RPC가 필요한 씬 컨트롤러의 베이스.
    /// GameController와 동일한 씬 단위 싱글톤 접근을 제공하되, NetworkBehaviour를 상속해
    /// 이 클래스(및 파생 클래스)에서 선언한 [Rpc] 메서드가 클라이언트에서도 정상 동작한다.
    /// (NGO의 RPC는 NetworkBehaviour에서만 네트워크 전송된다 — MonoBehaviour에 붙이면 호스트 로컬에서만 실행됨)
    /// 같은 GameObject에 NetworkObject가 반드시 부착돼 있어야 한다.
    /// </summary>
    public abstract class NetworkGameController<T> : NetworkBehaviour where T : NetworkGameController<T>
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
                        GameDebug.LogError($"[NetworkGameController] No instance of {typeof(T).Name} found in scene!");
                    }
                }
                return s_Instance;
            }
        }

        protected virtual void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                GameDebug.LogWarning($"[NetworkGameController] Multiple instances of {typeof(T).Name} detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            s_Instance = this as T;
        }

        public override void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
            base.OnDestroy();
        }
    }
}
