namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 예민한 귀(고양이 유틸 테크) — **듣는 쪽**의 가청 거리 배율 (테크 v3 계획 2b).
    /// 소리는 각 클라가 자기 프로세스에서 재생하므로(3D AudioSource maxDistance),
    /// 내 화면의 배율만 올리면 나만 멀리 듣는다. 서버 판정도 복제도 필요 없다.
    /// 클라가 제 오디오를 조작하는 것은 원래 막을 수 없는 영역이라 신뢰 경계 밖이다.
    ///
    /// 값을 세우는 것은 <see cref="ListenerRangeBinder"/> 하나뿐이다 — 로컬 플레이어의
    /// TechSlots를 구독해 갱신하고, 로컬 플레이어가 없어지면 1로 되돌린다.
    /// **봇 청각(BotSenses·SoundLog)에는 이 배율이 닿지 않는다** — 봇이 재는 것은 설계값이라야 한다.
    /// </summary>
    public static class ListenerRange
    {
        /// <summary>1 = 기본. 로컬 플레이어가 바뀌거나 테크를 사면 갱신된다.</summary>
        public static float Multiplier { get; private set; } = 1f;

        /// <summary>0·음수는 소리를 통째로 죽이므로 1로 막는다(미보유는 ParamOf가 0을 준다).</summary>
        public static void Set(float _multiplier)
        {
            Multiplier = _multiplier > 0f ? _multiplier : 1f;
        }

        public static void Reset() => Multiplier = 1f;
    }
}
