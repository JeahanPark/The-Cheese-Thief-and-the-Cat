using CTC.Runtime.Core;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 씬에 <see cref="NetworkManager"/>가 없으면 하나 세운다 (2026-08-30).
    ///
    /// 화면이 넷으로 갈리면서 생긴 문제다 — 접속은 **로비에서** 시작되는데(방 만들기·참여)
    /// NetworkManager는 `Game.unity`에만 있었다. 그래서 로비의 버튼이 스팀을 못 찾아
    /// 아무 일도 안 했다.
    ///
    /// NGO가 스스로 `DontDestroyOnLoad`를 걸므로 **한 번만 서면 로비→대기실→인게임을 그대로 탄다.**
    /// `Game.unity`를 혼자 열었을 때(wesqa·MPPM)는 그 씬의 것이 먼저 서고, 이쪽은 아무 일도 안 한다.
    /// </summary>
    [DefaultExecutionOrder(-20000)]
    public class NetworkRoot : MonoBehaviour
    {
        [SerializeField] private GameObject m_NetworkPrefab;

        private void Awake()
        {
            if (NetworkManager.Singleton != null) return;

            if (m_NetworkPrefab == null)
            {
                GameDebug.LogError("[NetworkRoot] 네트워크 프리팹이 안 붙어 있다 — 접속을 시작할 수 없다");
                return;
            }

            GameObject go = Instantiate(m_NetworkPrefab);
            go.name = m_NetworkPrefab.name;   // (Clone)이 로그에 붙는 걸 막는다
            GameDebug.Log("[NetworkRoot] NetworkManager를 세웠다");
        }
    }
}
