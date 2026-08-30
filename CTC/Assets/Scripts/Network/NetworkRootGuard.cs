using CTC.Runtime.Core;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 씬에 박힌 <see cref="NetworkManager"/>가 **두 번째**면 스스로 사라진다 (2026-08-30).
    ///
    /// NGO는 중복을 지우지 않는다 — `OnEnable`에서 `Singleton == null`일 때만 자기를 등록하고,
    /// 그렇지 않으면 그냥 `DontDestroyOnLoad`로 남는다. 대기실에서 인게임으로 넘어오면
    /// `Game.unity`의 NetworkManager 오브젝트가 그 두 번째가 되고, 거기 동승한 스팀·접속 UI·
    /// MPPM 배선까지 통째로 두 벌이 된다.
    ///
    /// `Game.unity`를 혼자 열었을 때는 Singleton이 없으므로 아무 일도 안 한다 —
    /// wesqa·MPPM 회귀 경로는 지금까지와 완전히 같다.
    /// </summary>
    [DefaultExecutionOrder(-20000)]
    public class NetworkRootGuard : MonoBehaviour
    {
        private void Awake()
        {
            NetworkManager existing = NetworkManager.Singleton;
            if (existing == null || existing.gameObject == gameObject) return;

            GameDebug.Log("[NetworkRoot] 씬의 NetworkManager가 중복 — 앞선 것을 쓰고 이건 버린다");
            Destroy(gameObject);
        }
    }
}
