using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 쥐 봇이 **언제 쏘는가**(순수). 2026-09-09 — 승률 진단 §9-3의 4번.
    ///
    /// 그전까지 봇 쥐는 절대 안 쐈다(2026-08-12 `783c5c8`). 여덟이 쉬지 않고 쏘니
    /// 97초에 고양이 둘이 죽어 계측이 안 됐기 때문이다. 그런데 그러면
    /// **「고양이 전멸 = 쥐 승」이라는 승리 경로가 계측에서 통째로 빠진다** —
    /// 재고 있는 승률이 반쪽이 된다.
    ///
    /// 그래서 뺀 게 아니라 **사람 수준으로 되돌린다.** 2026-08-12의 봇이 사람보다 셌던 건
    /// "쏜다"가 아니라 셋이었다.
    ///
    /// | 그때 | 사람 |
    /// |---|---|
    /// | 거리와 무관하게 쐈다(사거리 밖 발사도 소리를 냈다) | 안 맞을 거리에선 안 쏜다 |
    /// | **백발백중**이었다(표적 중심으로 직선) | 뛰면서 뒤로 쏘면 자주 빗나간다 |
    /// | 지나가는 고양이도 쐈다 | 들켰을 때 반격하지, 저격하지 않는다 |
    ///
    /// 이 셋을 각각 <see cref="UsableRange"/> · <see cref="AIM_ERROR_DEG"/> ·
    /// <see cref="ShouldShoot"/>의 「들켰나」 조건이 맡는다.
    ///
    /// **도망치며 쏘는 건 막지 않는다.** 이 게임의 이동은 카메라 기준이고(`MovementInput.ToWorldDirection`)
    /// 총알은 **카메라 앞**으로 나간다(`MousePopgun.Update`) — 사람은 S를 눌러 전속력으로 물러나면서
    /// 뒤를 보고 쏠 수 있다. 이동 속도 손해가 없으니, 봇에게만 그 손해를 매기면 그게 왜곡이다.
    /// </summary>
    public static class MouseCombatLogic
    {
        /// <summary>
        /// 무기 사거리 중 실제로 쏘는 구간의 비율. 끝자락에서는 조준 오차가 표적 폭(반지름 0.35m)을
        /// 훌쩍 넘어 거의 안 맞는데, 빗나간 총알도 **소리는 낸다**(`BotSenseLogic.IsPreySound`에
        /// 총소리가 들어 있다) — 안 맞을 거리에서 쏘는 건 자기 자리를 알려 주는 것뿐이다.
        /// </summary>
        public const float RANGE_USABLE_RATIO = 0.85f;

        /// <summary>
        /// 조준 오차(도, ±). **이 게임에서 봇을 사람 수준으로 묶는 유일한 손잡이다.**
        ///
        /// 0이면 봇이 백발백중이 되어 여덟 마리가 사람보다 조직적인 화력이 된다(2026-08-12 실측).
        /// 각도로 주면 **거리 감쇠가 공짜로 따라온다** — 표적 반지름 0.35m 기준
        /// 3m에서 ~95%, 5m에서 ~57%, 8m에서 ~36%가 맞는다. 뛰면서 뒤로 쏘는 사람의 명중률이 그쯤이다.
        /// 맞았는지는 여기서 정하지 않는다 — **실제 레이캐스트**가 정한다(벽·기둥도 같이 막는다).
        /// </summary>
        public const float AIM_ERROR_DEG = 7f;

        /// <summary>
        /// 고양이가 딴 데를 봐도 이 배수 안이면 쏜다 — 덮치기 사거리(2.8m)의 몇 배인가.
        /// 코앞이면 어차피 다음 순간 들킨다. 「들켰을 때만 반격」의 예외는 이것 하나다.
        ///
        /// **1.5보다 크게 잡으면 안 된다.** 1.5 = 4.2m인데 딱총의 유효 사거리가 6.8m다 —
        /// 2.5(7.0m)로 잡았더니 유효 사거리 전체가 예외에 삼켜져 「저격 금지」 규칙이
        /// 죽은 코드가 됐다(EditMode가 바로 잡아냈다). 고양이는 초당 0.8m씩 좁히므로
        /// 4.2m는 덮치기까지 약 1.7초 — 사람이 "이제 못 도망친다"고 판단하는 그 지점이다.
        /// </summary>
        public const float LAST_STAND_POUNCE_RATIO = 1.5f;

        /// <summary>
        /// **눈으로 잡은 고양이를 몇 초까지 계속 보는가.**
        ///
        /// 이게 없으면 반격이 사실상 안 일어난다(2026-09-09 실측: 2:8 한 판에 3~4발).
        /// 이유는 밸런스가 아니라 **모델이 사람과 다르기 때문**이다 — `BotSenses.CanSee`는
        /// **몸이 향한 쪽**으로 시야를 재는데, 도망치는 쥐의 몸은 고양이 반대쪽을 향한다.
        /// 그런데 사람의 시야는 몸이 아니라 **카메라**고(3인칭), 카메라는 몸과 따로 돈다 —
        /// 사람은 도망치면서 뒤를 본다. 몸 기준으로만 재면 봇 쥐는 영영 뒤를 못 본다.
        ///
        /// 그래서 **한 번 눈으로 잡은 고양이는 이 시간 동안 계속 본다.** 대신
        /// **가림 검사는 그대로 한다** — 벽·기둥 뒤로 사라지면 그 순간 못 쏜다.
        /// 3초는 "고개를 돌린 채 쫓기는 동안"이지 기억력이 아니다.
        /// </summary>
        public const float LOOK_BACK_SECONDS = 3f;

        /// <summary>도망치며 뒤를 보는 중인가 — 마지막으로 눈에 잡은 지 얼마나 됐나.</summary>
        public static bool KeepsEyeOn(float _secondsSinceSeen)
            => _secondsSinceSeen >= 0f && _secondsSinceSeen <= LOOK_BACK_SECONDS;

        /// <summary>실제로 쏘는 사거리(m).</summary>
        public static float UsableRange(float _weaponRange) => _weaponRange * RANGE_USABLE_RATIO;

        /// <summary>
        /// 지금 쏠까.
        ///
        /// <paramref name="_catVisible"/> 눈으로 잡았나 — 소리로만 아는 고양이는 안 쏜다(허공에 쏘는 셈).
        /// <paramref name="_catFacingMe"/> 그 고양이가 나를 보고 있나 — **반격이지 저격이 아니다.**
        ///   지나가는 고양이를 쏘면 안 들켰을 쥐가 스스로 들킨다. 사람은 그 짓을 안 한다.
        /// <paramref name="_distanceToCat"/>·<paramref name="_weaponRange"/> 안 맞을 거리에선 안 쏜다.
        /// <paramref name="_cooldownReady"/> 서버도 같은 판정을 하지만(FireRpc), 모르면 매 틱 죽은 RPC를 보낸다.
        ///
        /// **도망 중인지는 안 본다** — 클래스 주석 참조(뒤로 쏘는 데 이동 손해가 없다).
        /// </summary>
        public static bool ShouldShoot(
            bool _catVisible, bool _catFacingMe, float _distanceToCat,
            float _weaponRange, float _pounceRange, bool _cooldownReady)
        {
            if (!_catVisible || !_cooldownReady) return false;
            if (_distanceToCat > UsableRange(_weaponRange)) return false;

            return _catFacingMe || _distanceToCat <= _pounceRange * LAST_STAND_POUNCE_RATIO;
        }

        /// <summary>
        /// 실제로 겨눈 방향. 표적 방향에서 좌우로 <paramref name="_errorDeg"/>까지 흔든다.
        /// <paramref name="_roll"/>은 −1~+1 — 봇마다 제 난수를 준다(같은 판을 다시 돌리면 같은 결과).
        /// 위아래는 안 흔든다: 총알이 수평으로 나가고(`MousePopgun`이 y를 0으로 눕힌다) 표적도 서 있다.
        /// </summary>
        public static Vector3 AimDirection(Vector3 _from, Vector3 _to, float _errorDeg, float _roll)
        {
            Vector3 to = Flat(_to - _from);
            if (to.sqrMagnitude < 0.0001f) return Vector3.forward;

            float deg = Mathf.Clamp(_roll, -1f, 1f) * _errorDeg;
            return (Quaternion.Euler(0f, deg, 0f) * to.normalized).normalized;
        }

        /// <summary>
        /// 그 오차로 그 거리의 표적을 맞힐 확률(0~1). **판정에 안 쓴다** — 맞았는지는 레이캐스트가
        /// 정한다. 손잡이(<see cref="AIM_ERROR_DEG"/>)를 돌릴 때 "그래서 몇 %인가"를 표로 확인하려고 둔다.
        /// </summary>
        public static float HitChance(float _distance, float _targetRadius, float _errorDeg)
        {
            if (_errorDeg <= 0f) return 1f;
            if (_distance <= 0.0001f) return 1f;

            float needDeg = Mathf.Atan2(Mathf.Max(0f, _targetRadius), _distance) * Mathf.Rad2Deg;
            return Mathf.Clamp01(needDeg / _errorDeg);
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
