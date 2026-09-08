using CTC.Runtime.Core;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 매치 시드 (테크 v3 계획 3, 2026-09-08). 판마다 달라지는 것들 — 상점 자리·오늘의 품절·직업권 —
    /// 의 **단일 원본**이다.
    ///
    /// 서버가 판 시작에 한 번 굴리고 `NetworkVariable`로 복제한다. 결과 목록(품절 테크·상점 좌표)을
    /// 실어 보내지 않는 이유: 시드 4바이트만 건너가면 **클라가 같은 순수 함수로 같은 답을 다시 계산**한다.
    /// 목록을 복제하면 판정이 두 군데(서버가 뽑은 목록 · 클라가 본 목록)가 되어 어긋날 자리가 생긴다.
    ///
    /// 용도마다 <see cref="SubSeed"/>로 수열을 가른다. 시드 하나를 셋이 같이 쓰면
    /// "상점이 왼쪽에 서는 판은 늘 후추가 품절"처럼 상관이 생긴다.
    ///
    /// `LobbyController` 오브젝트에 동승한다(in-scene NetworkObject — 새 프리팹 0, 기획서 §9 제약).
    /// </summary>
    public class MatchRandom : NetworkBehaviour
    {
        // 용도 소금. 복제되는 값이 아니라 코드 안에서만 쓰므로 enum이 아니라 const다 —
        // 값이 바뀌면 그 판의 결과가 통째로 달라질 뿐, 저장·통신 호환은 걸리지 않는다.
        /// <summary>상점 자리 추첨(계획 3 Task 2).</summary>
        public const int SALT_SHOP = 1;
        /// <summary>오늘의 품절 추첨(계획 3 Task 3).</summary>
        public const int SALT_SOLD_OUT = 2;
        /// <summary>직업권 자리 셔플(계획 3 Task 4).</summary>
        public const int SALT_TICKET = 3;

        /// <summary>
        /// 시드가 0이거나 직전과 같아졌을 때만 쓰는 비트 상수 — 그 자체로는 뜻이 없다.
        /// 0을 피하는 이유: 읽는 쪽이 <c>Seed == 0</c>을 "아직 안 굴렸다"(로비·실험실)로 읽는다.
        /// </summary>
        private const int SEED_FALLBACK = unchecked((int)0x5F3759DF);

        private readonly NetworkVariable<int> m_Seed = new NetworkVariable<int>();

        // 스폰 전에 굴린 값. NGO는 **스폰 전 NetworkVariable 쓰기를 조용히 버린다**(IsServer도 아직 false다) —
        // 계획 2a에서 ThornWall·StickyPatch가 같은 곳에 물렸다. 여기 담아 뒀다 OnNetworkSpawn에서 흘린다.
        private int m_PendingSeed;

        private static MatchRandom s_Current;

        /// <summary>지금 판의 시드 소유자(없으면 null). <c>ShopSpot.All</c>과 같은 명부 방식 — 탐색이 없다.</summary>
        public static MatchRandom Current => s_Current;

        /// <summary>이 판의 시드. **0 = 아직 안 굴렸다**(로비·봇 실험실) — 읽는 쪽이 후퇴 경로를 골라야 한다.</summary>
        public int Seed => m_Seed.Value;

        /// <summary>시드 변경 스트림(구독 즉시 현재 값 1회) — 품절 캐시가 이걸 구독한다(규칙 3, 폴링 금지).</summary>
        public Observable<int> SeedChanged => m_Seed.AsObservableWithCurrent();

        public override void OnNetworkSpawn()
        {
            s_Current = this;
            if (IsServer && m_PendingSeed != 0)
            {
                m_Seed.Value = m_PendingSeed;
                m_PendingSeed = 0;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (s_Current == this) s_Current = null;
        }

        /// <summary>
        /// 판 시작에 서버가 한 번 부른다 — **스포너보다 먼저**. 스포너들이 이 시드를 읽어 자리를 뽑으므로
        /// 순서가 뒤집히면 그 판만 옛 시드(또는 0)로 서고, 아무 에러도 안 난다.
        /// </summary>
        public void ServerRoll()
        {
            NetworkManager nm = NetworkManager.Singleton;
            bool isServer = IsSpawned ? IsServer : (nm != null && nm.IsServer);
            if (!isServer) return;

            int previous = IsSpawned ? m_Seed.Value : m_PendingSeed;
            int seed = NextSeed(previous, Entropy());

            if (!IsSpawned) { m_PendingSeed = seed; return; }
            m_Seed.Value = seed;
        }

        /// <summary>
        /// 다음 판의 시드 — **0이 아니고 직전과도 다르다**(순수, EditMode가 덮는다).
        ///
        /// 직전과 다름을 못 박는 이유: 판을 다시 열 때 값이 그대로면 "무작위인데 두 판이 똑같다"가 되고,
        /// 그건 배선이 끊긴 것과 화면상 구분이 안 된다.
        /// </summary>
        public static int NextSeed(int _previous, int _entropy)
        {
            int seed = Mix(_previous ^ _entropy);
            if (seed == 0 || seed == _previous) seed = unchecked(_previous + SEED_FALLBACK);
            if (seed == 0) seed = SEED_FALLBACK;
            return seed;
        }

        /// <summary>
        /// 용도별 수열 — 같은 시드·같은 소금이면 언제 어디서 불러도 같은 값이다(서버·클라가 이걸로 만난다).
        /// 시드가 0이어도 **쓸 수 있는 수**를 돌려준다: 0을 그대로 흘리면 실험실에서 셋이 같은 수를 받는다.
        /// </summary>
        public static int SubSeed(int _seed, int _salt)
        {
            int saltHash = Mix(unchecked(_salt * 0x27D4EB2D + 0x165667B1));
            int mixed = Mix(_seed ^ saltHash);
            return mixed != 0 ? mixed : SEED_FALLBACK;
        }

        /// <summary>매 판 다른 값을 넣는 자리 — 시계와 프레임을 섞는다.</summary>
        private static int Entropy()
        {
            return unchecked((int)System.DateTime.UtcNow.Ticks ^ (Time.frameCount * 397));
        }

        /// <summary>
        /// 32비트 finalizer 해시(전단사) — 입력 1비트가 출력 전체를 흔든다.
        /// 전단사라 <c>Mix(x) == 0</c>은 <c>x == 0</c>일 때뿐이다(0 회피 판정이 이걸 믿는다).
        /// </summary>
        private static int Mix(int _value)
        {
            unchecked
            {
                uint x = (uint)_value;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return (int)x;
            }
        }
    }
}
