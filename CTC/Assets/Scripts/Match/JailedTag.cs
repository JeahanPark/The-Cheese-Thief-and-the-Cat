using CTC.Runtime.Core;
using R3;
using Unity.Netcode;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 이 쥐가 갇혔는가 + 남은 체류 시간 (v3.8 4번).
    ///
    /// 서버만 쓰고 전원이 읽는다 — 갇힌 본인은 카운트다운을 보고, 남은 쥐는 "몇 명 갇혔나"를 본다.
    /// 상태를 감옥이 아니라 **플레이어가** 들고 있는 이유: 갇힌 쥐를 세는 쪽(승패 판정)도,
    /// 입력을 막는 쪽(본인 클라)도 플레이어에서 출발하기 때문이다. 감옥은 인덱스로만 참조한다.
    /// </summary>
    public class JailedTag : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> m_Jailed = new NetworkVariable<bool>();
        private readonly NetworkVariable<float> m_RemainingSeconds = new NetworkVariable<float>();
        private readonly NetworkVariable<int> m_JailIndex = new NetworkVariable<int>(-1);

        public bool Jailed => m_Jailed.Value;
        public float RemainingSeconds => m_RemainingSeconds.Value;
        public int JailIndex => m_JailIndex.Value;

        /// <summary>갇힘 여부 스트림(View용) — 구독 즉시 현재 값이 흐른다.</summary>
        public Observable<bool> JailedChanged => m_Jailed.AsObservableWithCurrent();

        /// <summary>남은 체류 시간 스트림(HUD 카운트다운).</summary>
        public Observable<float> RemainingChanged => m_RemainingSeconds.AsObservableWithCurrent();

        /// <summary>서버 전용 — 수감.</summary>
        public void ServerJail(int _jailIndex, float _holdSeconds)
        {
            if (!IsServer) return;
            m_Jailed.Value = true;
            m_JailIndex.Value = _jailIndex;
            m_RemainingSeconds.Value = _holdSeconds;
        }

        /// <summary>서버 전용 — 석방.</summary>
        public void ServerRelease()
        {
            if (!IsServer) return;
            m_Jailed.Value = false;
            m_JailIndex.Value = -1;
            m_RemainingSeconds.Value = 0f;
        }

        /// <summary>서버 전용 — 체류 시간 갱신(`JailDirector`가 매 틱 부른다).</summary>
        public void ServerSetRemaining(float _seconds)
        {
            if (!IsServer) return;
            m_RemainingSeconds.Value = _seconds;
        }
    }
}
