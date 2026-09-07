using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 예민한 귀 레벨을 <see cref="ListenerRange"/>로 흘려 넣는 얇은 Worker (테크 v3 계획 2b).
    /// HUD 캔버스에 붙는다(`LobbyCanvasBuilder`) — 그릴 게 없으니 View가 아니라,
    /// 로컬 플레이어의 Model(<see cref="TechSlots"/>)과 정적 홀더를 잇기만 한다.
    ///
    /// 배율의 주인이 여기 하나뿐이라, 관전·매치 전(로컬 플레이어 null)에는 저절로 1로 돌아간다.
    /// 남의 몸은 보지 않는다 — 내 귀만 좋아지는 게 이 테크의 전부다.
    /// </summary>
    public class ListenerRangeBinder : MonoBehaviour
    {
        private readonly SerialDisposable m_SlotsSub = new SerialDisposable();

        private void Awake()
        {
            // 도메인 리로드를 끄고 플레이하면 정적 값이 남는다 — 씬 시작마다 비운다
            ListenerRange.Reset();
        }

        private void Start()
        {
            m_SlotsSub.AddTo(this);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnDestroy()
        {
            ListenerRange.Reset();
        }

        // 플레이어가 바뀌면 테크 구독을 통째로 갈아 끼운다(SerialDisposable이 이전 것을 끊는다)
        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            TechSlots slots = _player != null ? _player.GetComponent<TechSlots>() : null;
            if (slots == null)
            {
                m_SlotsSub.Disposable = null;
                ListenerRange.Reset();
                return;
            }

            // Changed는 구독 즉시 한 번 흐른다 — 리스폰·재접속 뒤에도 이미 산 레벨이 그대로 반영된다
            m_SlotsSub.Disposable = slots.Changed.Subscribe(_ => Apply(slots));
        }

        private void Apply(TechSlots _slots)
        {
            if (_slots == null) { ListenerRange.Reset(); return; }
            // 미보유면 ParamOf가 0을 주고, ListenerRange.Set이 그걸 1로 막는다
            ListenerRange.Set(TechLogic.ParamOf(TechId.CatEar, _slots.LevelOf(TechId.CatEar), 1));
        }
    }
}
