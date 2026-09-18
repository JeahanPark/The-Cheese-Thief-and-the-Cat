namespace CTC.Runtime.UI
{
    /// <summary>
    /// 접속 대기 로딩창 판정 — 순수 함수 (2026-09-19, 사용자 시안 5).
    ///
    /// 대기실 추첨 연출이 검게 닫히는 순간 뜨고(<see cref="LoadingScreen.Begin"/>), 씬이 넘어가는 동안 계속 가리다가
    /// **판이 서면**(`GameStarted` — 서버가 전원 접속 또는 접속 대기 시간이 끝났을 때 켠다) 걷힌다.
    /// 로컬처럼 전원이 한꺼번에 들어오면 판이 곧바로 서므로 **최소 시간**만큼은 보여 준다 — 안 그러면 한 프레임 번쩍하고 만다
    /// (2026-09-19 실측: 게임 씬이 선 지 0.4초 안에 걷혀 3인 캡처에서 한 번도 안 잡혔다).
    /// 서버가 멈춰도 화면이 영영 막히지 않게 상한을 둔다.
    /// </summary>
    public static class LoadingScreenLogic
    {
        /// <summary>
        /// 이 시간이 지나면 판이 안 서도 걷는다 — 씬 전환 + 접속 대기(10초) + 맵·스폰 여유.
        /// 가려진 채 굳는 것보다 빈 맵이 보이는 편이 낫다(무엇이 잘못됐는지라도 보인다).
        /// </summary>
        public const float MAX_SECONDS = 25f;

        /// <summary>
        /// 판이 이미 섰어도 이만큼은 보인다 — 추첨 연출이 닫히는 0.4초 + 씬 로딩(약 1초)을 덮고,
        /// 게임 씬에서 그림이 한 번은 읽히게 한다(로컬 3인 실측: 연출 3.8초에 떠 게임 씬은 약 5초에 선다).
        /// </summary>
        public const float MIN_SECONDS = 2.0f;

        /// <summary>뜰 때 — 대기실 추첨 연출이 검게 닫히는 시간(<c>RoomDrawStage</c> 0.4초)과 같다: 방이 이 그림으로 넘어간다.</summary>
        public const float FADE_IN_SECONDS = 0.4f;

        /// <summary>걷힐 때 — 판 화면이 스르르 드러난다.</summary>
        public const float FADE_OUT_SECONDS = 0.35f;

        /// <summary>점이 한 바퀴(. → .. → ...) 도는 시간.</summary>
        public const float DOTS_CYCLE_SECONDS = 1.2f;

        /// <summary>지금 가려야 하나(뜬 지 <paramref name="_elapsed"/>초).</summary>
        public static bool ShouldShow(bool _gameStarted, float _elapsed)
        {
            if (_elapsed < MIN_SECONDS) return true;
            return !_gameStarted && _elapsed < MAX_SECONDS;
        }

        /// <summary>뜬 지 <paramref name="_elapsed"/>초의 알파(0→1).</summary>
        public static float FadeInAlpha(float _elapsed)
        {
            if (FADE_IN_SECONDS <= 0f) return 1f;
            return UnityEngine.Mathf.Clamp01(_elapsed / FADE_IN_SECONDS);
        }

        /// <summary>걷히기 시작한 지 <paramref name="_sinceHide"/>초의 알파(1→0).</summary>
        public static float FadeOutAlpha(float _sinceHide)
        {
            if (FADE_OUT_SECONDS <= 0f) return 0f;
            return UnityEngine.Mathf.Clamp01(1f - _sinceHide / FADE_OUT_SECONDS);
        }

        /// <summary>경과 시간의 점 — 한 바퀴를 셋으로 나눠 ".", "..", "..." 순서.</summary>
        public static string Dots(float _elapsed)
        {
            if (_elapsed < 0f) _elapsed = 0f;
            float t = _elapsed % DOTS_CYCLE_SECONDS;
            int count = 1 + (int)(t / (DOTS_CYCLE_SECONDS / 3f));
            if (count > 3) count = 3;
            return new string('.', count);
        }
    }
}
