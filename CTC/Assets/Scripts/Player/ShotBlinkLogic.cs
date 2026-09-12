using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 쏠 때 **제 총소리에 눈을 질끈 감는** 시간표 — 씬 없이 EditMode가 덮는 순수 산수.
    ///
    /// 왜 이 연출인가: 쥐는 제 몸만 한 총을 든 작은 짐승이다. 쏘는 순간 움찔하며 눈을 감는 것은
    /// 「이 무기가 이 캐릭터에게 과하다」를 한 프레임으로 말한다. 반동 클립이 이미 몸을 젖히고 있으니
    /// 눈은 그 동작의 끝을 마무리할 뿐이다.
    ///
    /// **왜 지연이 있나 — 이게 이 파일의 존재 이유다.** RPC가 오는 순간에 감아 버리면
    /// 겨누는 내내 눈이 감겨 있다. 사람은 **눈을 뜨고 겨누고 터질 때 움찔한다.**
    /// 그래서 클립의 **발사 프레임**까지 기다렸다가 감는다(아래 상수).
    ///
    /// **왜 여기 따로 있나**: <see cref="EyeLook"/>가 트랜스폼과 시계를 들고 있어 EditMode가 못 만든다.
    /// 시간 계산만 떼어 두면 「연사 때 눈이 굳지 않나」 같은 질문을 씬 없이 단언할 수 있다 —
    /// <see cref="GunFadeLogic"/>과 같은 이유, 같은 모양이다.
    ///
    /// **위상(phase)으로 적는다.** 이 클래스의 시간축은 *발사 순간*이 0이라 **지연 구간이 음수**다.
    /// 경과(elapsed)로 적으면 지연이 클립마다 달라 「지금 얼마나 감겼나」가 지연에 따라 갈리는데,
    /// 위상으로 두면 감기·붙들기·뜨기가 **지연을 모르는 한 덩어리**가 된다 —
    /// 연사 재개(<see cref="RetriggerPhase"/>)가 한 줄로 떨어지는 것도 그래서다.
    /// </summary>
    public static class ShotBlinkLogic
    {
        /// <summary>클립 프레임률. 두 발사 클립 모두 24fps 10프레임(=0.4167초)이다.</summary>
        public const float CLIP_FPS = 24f;

        /// <summary>
        /// <c>Shoot</c>(반동만)의 **터지는 프레임**, 1부터 센다.
        /// 근거: <c>MouseGunBaker.GUN_LOCAL_EULER</c> 주석의 실측 — 몸 기준 앞다리 본 X가
        /// <c>Shoot</c>에서 bf04~bf05에 301.6° 근처로 평평해진다. 총구가 수평으로 앞을 보는
        /// 그 구간이 겨눠서 쏘는 순간이다. 그 시작인 5프레임째를 잡는다.
        /// </summary>
        public const int SHOOT_FIRE_FRAME = 5;

        /// <summary>
        /// <c>ShootDraw</c>(허리춤에서 뽑아 첫 발)의 터지는 프레임, 1부터 센다.
        /// 같은 실측에서 이 클립은 bf05~bf07에 평평해진다 — 뽑는 동작이 앞에 붙어 **두 프레임 늦다**.
        /// 그래서 상수를 하나로 두면 안 된다: 첫 발에서 눈이 먼저 감겨 버린다.
        /// </summary>
        public const int SHOOT_DRAW_FIRE_FRAME = 7;

        /// <summary>1프레임째가 0초다 — n프레임째는 (n-1)/fps.</summary>
        public const float SHOOT_DELAY_SECONDS = (SHOOT_FIRE_FRAME - 1) / CLIP_FPS;          // 0.1667

        /// <summary>같은 계산, 두 프레임 뒤.</summary>
        public const float SHOOT_DRAW_DELAY_SECONDS = (SHOOT_DRAW_FIRE_FRAME - 1) / CLIP_FPS; // 0.2500

        /// <summary>
        /// 감기는 시간. 움찔은 **빠르다** — 느리게 감으면 졸린 눈이 되고 「놀랐다」가 안 읽힌다.
        /// 선형이다(이징을 안 쓴다): <see cref="RetriggerPhase"/>가 지금 감긴 정도를 다시
        /// 시간으로 **되감아야** 하는데, 선형이어야 그 되감기가 정확한 역이 된다.
        /// </summary>
        public const float CLOSE_SECONDS = 0.05f;

        /// <summary>
        /// 감은 채 버티는 시간. **가장 빠른 무기의 쿨다운보다 길어야 한다** —
        /// <c>WeaponInfo.csv</c> 티어 2·3이 0.15초이고 여기는 0.18초다.
        ///
        /// 왜 «합»이 아니라 이 값 하나인가: 연사 중 다음 발은 <see cref="RetriggerPhase"/>가
        /// **붙들기의 시작**으로 되돌린다(감기 구간은 다시 안 밟는다). 그래서 다음 발까지 버텨야
        /// 하는 것은 <c>CLOSE + SHUT</c>이 아니라 <b>SHUT 하나</b>다. 0.12초로 잡았다가
        /// 계산해 보니 매 발 사이에 0.03초씩 눈이 11%쯤 떴다 — 파르르 떠는 그림이다.
        /// 0.15초 정확히도 안 된다: 다음 발과 구간 끝이 같은 순간에 걸려 어느 쪽이 이길지
        /// 프레임마다 갈린다(CLAUDE.md 함정 #7과 같은 부류). 0.18이면 20% 여유가 남는다.
        /// </summary>
        public const float SHUT_SECONDS = 0.18f;

        /// <summary>
        /// 뜨는 시간. 감기(0.05)보다 **느긋하다** — 감는 것은 놀람이라 빨라야 하고,
        /// 뜨는 것은 아무 사건도 아니라 눈이 따라갈 필요가 없다(<c>GunFadeLogic</c>의 수납과 같은 이유).
        /// 여기만 <c>SmoothStep</c>을 쓴다: 되감기의 역이 될 필요가 없는 유일한 구간이다.
        /// </summary>
        public const float OPEN_SECONDS = 0.14f;

        /// <summary>발사 순간부터 다시 다 뜰 때까지.</summary>
        public const float TOTAL_SECONDS = CLOSE_SECONDS + SHUT_SECONDS + OPEN_SECONDS;

        /// <summary>
        /// 이 아래는 「이미 다 떴다」로 본다. 다 뜬 눈에 새 발이 오면 **지연부터 다시** 밟아야 한다 —
        /// 안 그러면 겨누기도 전에 감긴다. 0으로 두면 뜨는 구간의 꼬리(0.001)에서도
        /// 이어 감기로 빠져 지연이 통째로 사라진다.
        /// </summary>
        public const float OPEN_EPSILON = 0.02f;

        /// <param name="_alreadyOut">
        /// 쏘기 직전에 총이 이미 나와 있었나 — <see cref="MouseGunVisualLogic.ClipForShot"/>와
        /// **같은 값**을 받는다. 클립과 지연이 한 갈림길에서 갈려야 둘이 어긋날 수가 없다.
        /// </param>
        public static float DelayFor(bool _alreadyOut)
            => _alreadyOut ? SHOOT_DELAY_SECONDS : SHOOT_DRAW_DELAY_SECONDS;

        /// <summary>
        /// 지금 얼마나 감겼나(0 = 활짝, 1 = 완전히 감음). <paramref name="_phaseSeconds"/>는
        /// **발사 순간이 0**이라 지연 구간은 음수다.
        /// </summary>
        public static float Cover(float _phaseSeconds)
        {
            // 아직 겨누는 중이다 — 눈을 뜨고 있어야 한다. 이 한 줄이 이 연출의 전부다.
            if (_phaseSeconds <= 0f) return 0f;

            if (_phaseSeconds < CLOSE_SECONDS) return _phaseSeconds / CLOSE_SECONDS;

            float held = _phaseSeconds - CLOSE_SECONDS;
            if (held < SHUT_SECONDS) return 1f;

            float k = (held - SHUT_SECONDS) / OPEN_SECONDS;
            return k >= 1f ? 0f : Mathf.SmoothStep(1f, 0f, k);
        }

        /// <summary>다 떴나 — 루프를 멈추는 자리.</summary>
        public static bool IsComplete(float _phaseSeconds) => _phaseSeconds >= TOTAL_SECONDS;

        /// <summary>
        /// 새 발이 왔다 — 시간표를 **어느 위상부터** 다시 돌릴까. 세 갈래다.
        ///
        /// <list type="number">
        /// <item><b>아직 지연 중이면 그대로 둔다.</b> 예약된 감기를 **미루지 않는다** —
        /// 기관총 쿨다운(0.15초)이 지연(0.1667초)보다 **짧아서**, 매 발마다 지연을 다시 걸면
        /// 감을 차례가 영원히 오지 않는다(연사 내내 눈을 뜨고 있다). 다만 새 클립의 지연이
        /// 더 짧으면 그 쪽으로 당긴다 — 늦추지만 않으면 된다.</item>
        /// <item><b>이미 감겨 있으면 그 자리에서 이어 감고 다시 붙든다.</b> 감긴 정도를
        /// 감기 구간의 시간으로 되감아 돌려주므로 **끊기는 프레임이 없고, 절대 다시 뜨지 않는다.**
        /// 연사 중 눈은 계속 감겨 있고, 마지막 발 뒤에야 한 번 뜬다.</item>
        /// <item><b>다 떠 있으면 처음부터.</b> 지연부터 밟는다.</item>
        /// </list>
        /// </summary>
        /// <param name="_runningPhase">
        /// 지금 돌고 있는 시간표의 위상. 도는 것이 없으면 <c>float.PositiveInfinity</c>
        /// (= 이미 다 뜬 것과 같다).
        /// </param>
        public static float RetriggerPhase(float _runningPhase, float _newDelaySeconds)
        {
            float start = -Mathf.Max(0f, _newDelaySeconds);

            if (float.IsNaN(_runningPhase)) return start;

            // 1) 아직 감기 전 — 둘 중 **이른 쪽**(위상이 큰 쪽)을 남긴다.
            if (_runningPhase < 0f) return Mathf.Max(_runningPhase, start);

            // 3) 다 떴다(도는 것이 없는 경우도 여기로 온다 — 위상이 +무한이면 Cover가 0이다).
            float cover = Cover(_runningPhase);
            if (cover <= OPEN_EPSILON) return start;

            // 2) 감겨 있다 — 감기 구간의 같은 지점으로 되감아 붙든다(선형이라 정확한 역이다).
            return cover * CLOSE_SECONDS;
        }
    }
}
