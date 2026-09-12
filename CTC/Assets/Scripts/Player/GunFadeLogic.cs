namespace CTC.Runtime.Player
{
    /// <summary>
    /// 총이 **얼마나 보이나**(알파)를 정하는 순수 규칙 — 씬 없이 EditMode가 덮는다.
    ///
    /// 왜 페이드인가: 총은 평소에 꺼져 있다가 쏠 때만 나온다
    /// (<see cref="MouseGunVisualLogic"/> 참조 — 상시로 보이면 「저 쥐는 무장했다」가 샌다).
    /// 그런데 <c>SetActive</c>만으로 껐다 켜면 **한 프레임 만에 튀어나온다.** 0.22m짜리
    /// 물건이 화면에서 갑자기 생기면 눈은 그걸 「등장」이 아니라 **「팝」**으로 읽는다.
    /// 알파로 0→1을 밟으면 같은 정보가 같은 시간에 전달되면서 등장으로 읽힌다.
    ///
    /// 왜 시간 계산만 여기 있나: 런타임(<see cref="MouseGunVisual"/>)이 UniTask 루프에서
    /// 부르고, 미리보기 렌더러(<c>ClipPreviewRender</c>)가 같은 함수로 알파를 찍는다.
    /// 둘이 갈리면 미리보기가 거짓말을 한다 — <see cref="MouseGunVisualLogic.HolsteredAt"/>와
    /// 같은 이유로 계산을 한곳에만 둔다.
    /// </summary>
    public static class GunFadeLogic
    {
        /// <summary>
        /// 뽑기 시작 → 허리춤 총이 다 보이기까지. 바꿔치기 시각(3/24 ≈ 0.125초)보다
        /// **짧아야** 한다 — 다 나타나기 전에 손으로 넘어가면 허리춤 총은 반쯤 뜬 채로만
        /// 지나가고, 「허리에 총이 있었다」가 안 읽힌다.
        /// </summary>
        public const float DRAW_FADE_IN_SECONDS = 0.10f;

        /// <summary>
        /// 바꿔치기 크로스페이드. 허리춤이 사라지는 만큼 든 총이 나타난다.
        /// 짧은 이유: 이 순간은 손이 허리에 가장 가까운 한 프레임대라, 길게 끌면
        /// **둘이 동시에 반투명으로 보이는 시간**이 길어져 총이 둘이라는 게 들킨다.
        /// </summary>
        public const float SWAP_CROSSFADE_SECONDS = 0.08f;

        /// <summary>
        /// 수납 페이드아웃. 뽑기(0.10)보다 **느긋하다** — 뽑기는 "지금 쏜다"는 신호라
        /// 빨라야 하고, 넣기는 아무 사건도 아니라 눈이 따라갈 필요가 없다.
        /// 0.3초면 교전이 끝난 뒤 총이 스르르 없어지는 것으로 읽힌다.
        /// </summary>
        public const float HOLSTER_FADE_OUT_SECONDS = 0.30f;

        /// <summary>
        /// 진행률 0~1. **0으로 나누지 않는다** — 길이가 0이나 음수면 「이미 끝났다」(1)로 본다.
        /// 그래야 부르는 쪽이 길이를 0으로 줘도 알파가 NaN이 되어 총이 사라지는 대신
        /// 목표 알파에 즉시 도달한다(페이드 끄기 = 길이 0).
        /// </summary>
        public static float Progress(float _elapsedSeconds, float _durationSeconds)
        {
            if (_durationSeconds <= 0f) return 1f;
            if (_elapsedSeconds <= 0f) return 0f;              // 음수 경과도 여기서 잘린다
            if (_elapsedSeconds >= _durationSeconds) return 1f; // 넘겨도 오버슈트하지 않는다
            return _elapsedSeconds / _durationSeconds;
        }

        /// <param name="_fadingIn">참이면 0→1(나타남), 거짓이면 1→0(사라짐).</param>
        public static float Alpha(float _elapsedSeconds, float _durationSeconds, bool _fadingIn)
        {
            float p = Progress(_elapsedSeconds, _durationSeconds);
            return _fadingIn ? p : 1f - p;
        }

        /// <summary>
        /// 지금 알파에서 목표 알파로. 수납이 **뽑는 도중에** 끼어들 수 있어서 필요하다 —
        /// 그때 든 총은 1이 아니라 0.4쯤에 있고, 거기서 1로 튀었다가 내려가면 깜빡인다.
        /// </summary>
        public static float AlphaBetween(float _elapsedSeconds, float _durationSeconds, float _from, float _to)
            => _from + (_to - _from) * Progress(_elapsedSeconds, _durationSeconds);

        /// <summary>
        /// 바꿔치기 한 쌍. **합이 항상 1이다** — 한쪽이 사라지는 만큼만 다른 쪽이 나타나므로
        /// 「둘 다 진해져 총이 둘로 보이는」 순간도, 「둘 다 옅어져 총이 없어지는」 순간도 없다.
        /// 이 불변식이 곧 바꿔치기가 안 튀는 이유라서 두 값을 한 함수에서 같이 낸다.
        /// </summary>
        public static void CrossfadeAlphas(
            float _elapsedSeconds, float _durationSeconds, out float _outgoing, out float _incoming)
        {
            _incoming = Alpha(_elapsedSeconds, _durationSeconds, true);
            _outgoing = 1f - _incoming;
        }

        /// <summary>페이드가 끝났나 — 루프를 멈추는 자리.</summary>
        public static bool IsComplete(float _elapsedSeconds, float _durationSeconds)
            => Progress(_elapsedSeconds, _durationSeconds) >= 1f;

        /// <summary>
        /// 이 알파에서 오브젝트를 켜 둘 가치가 있나. 완전히 투명한 총은 **끈다** —
        /// 안 그러면 안 보이는 물건이 매 프레임 드로우콜과 그림자를 먹는다.
        /// </summary>
        public static bool ShouldRender(float _alpha) => _alpha > 0f;
    }
}
