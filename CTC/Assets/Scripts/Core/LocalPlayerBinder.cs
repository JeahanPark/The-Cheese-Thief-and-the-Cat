using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 로컬 플레이어의 스폰/디스폰을 스트림으로 발행하는 Worker (개편 3b, 2026-07-31).
    ///
    /// 전에는 HUD 8개가 **매 프레임** `SpawnManager.GetLocalPlayerObject()`로 자기 플레이어를
    /// 다시 찾았다. 이제 플레이어가 스폰될 때 자기를 한 번 발행하고(`LocalPlayerPublisher`),
    /// View는 그걸 구독한다 — 매 프레임 탐색 8곳이 사라진다.
    ///
    /// 값이 null이면 **관전자이거나 아직 매치 전**이라는 뜻이라, View의 "숨김" 분기가
    /// `Where`/`Select` 한 줄로 선언된다.
    ///
    /// 정적 프로퍼티인 이유: 플레이어 프리팹(동적 스폰)이 씬 오브젝트를 찾지 않고 발행해야 하고,
    /// HUD도 인스펙터 배선 없이 읽어야 하기 때문. 씬 오브젝트(this)는 플레이 종료 시 초기화만 맡는다
    /// (SfxPlayer가 쓰는 것과 같은 패턴).
    /// </summary>
    public class LocalPlayerBinder : MonoBehaviour
    {
        private static readonly ReactiveProperty<NetworkObject> s_LocalPlayer =
            new ReactiveProperty<NetworkObject>(null);

        /// <summary>로컬 플레이어(없으면 null — 관전자·매치 전).</summary>
        public static ReadOnlyReactiveProperty<NetworkObject> LocalPlayer => s_LocalPlayer;

        internal static void Publish(NetworkObject _player)
        {
            s_LocalPlayer.Value = _player;
        }

        internal static void Withdraw(NetworkObject _player)
        {
            // 새 플레이어가 이미 들어왔으면 덮지 않는다(재시작 시 스폰이 디스폰보다 먼저 올 수 있다)
            if (s_LocalPlayer.Value == _player) s_LocalPlayer.Value = null;
        }

        private void Awake()
        {
            // 도메인 리로드를 끄고 플레이하면 정적 값이 남는다 — 씬 시작마다 비운다
            s_LocalPlayer.Value = null;
        }

        private void OnDestroy()
        {
            s_LocalPlayer.Value = null;
        }
    }
}
