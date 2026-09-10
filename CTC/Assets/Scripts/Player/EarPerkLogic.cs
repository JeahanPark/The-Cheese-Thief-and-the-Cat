using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 귀 계산의 순수 부분 — 트랜스폼도 시간도 소리도 모른다(EditMode 테스트 대상).
    /// 각도는 전부 **몸 정면 기준 도(度)**, 좌 음수 · 우 양수다.
    /// </summary>
    public static class EarPerkLogic
    {
        /// <summary>
        /// 소리 쪽으로 돌릴 각(도). 수평면만 본다 — 귀는 위아래로 안 돌고,
        /// 들리는지 판정(<see cref="Bots.BotSenseLogic.CanHear"/>)도 수평 거리다.
        /// 최대 각도로 클램프한다: 넘으면 귀가 뒤로 넘어가 머리를 뚫는다.
        /// </summary>
        public static float TurnDegrees(Vector3 _forward, Vector3 _toSound, float _maxDeg)
        {
            Vector3 forward = Flat(_forward);
            Vector3 to = Flat(_toSound);
            if (forward.sqrMagnitude <= 1e-8f || to.sqrMagnitude <= 1e-8f) return 0f;

            float max = Mathf.Clamp(_maxDeg, 0f, 180f);
            return Mathf.Clamp(Vector3.SignedAngle(forward, to, Vector3.up), -max, max);
        }

        /// <summary>스냅 금지 — 남은 값에 비례해 따라간다(프레임률과 무관한 지수 감쇠).</summary>
        public static float Follow(float _current, float _target, float _speed, float _deltaTime)
        {
            if (_speed <= 0f || _deltaTime <= 0f) return _current;
            return Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-_speed * _deltaTime));
        }

        /// <summary>
        /// **쫑긋 한 번**의 세기 곡선 0~1 — 딜레이 → 빠르게 섬 → 천천히 풀림.
        ///
        /// 「지속 상태」가 아니라 곡선인 이유(2026-09-10 사용자 요구): 상태로 두면 상대가 걷는
        /// 내내 귀가 **가만히 서 있어서** 눈에 안 띄고, 「새로운 소리」도 아니다. 걸음마다 이 곡선을
        /// 다시 트리거하면 뛸 때 여러 번 튕겨 **쫑긋쫑긋**이 되고, 딜레이도 여기 들어간다.
        ///
        /// <paramref name="_from"/>은 트리거 순간의 값이다 — 풀리는 도중에 또 들으면 **0으로 뚝
        /// 떨어졌다 다시 오르지 않고** 그 자리에서 이어 오른다(연속이라 튀지 않는다).
        /// </summary>
        public static float PerkEnvelope(float _elapsed, float _from, float _delay, float _rise, float _fall)
        {
            float from = Mathf.Clamp01(_from);
            if (_elapsed < 0f) return from;

            float delay = Mathf.Max(0f, _delay);
            if (_elapsed < delay) return from;          // 듣고 나서 잠깐 — 반응 지연

            float rise = Mathf.Max(1e-4f, _rise);
            float t = _elapsed - delay;
            if (t < rise) return Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, t / rise));

            float fall = Mathf.Max(1e-4f, _fall);
            float k = (t - rise) / fall;
            return k >= 1f ? 0f : Mathf.SmoothStep(1f, 0f, k);
        }

        /// <summary>
        /// 쫑긋이 **눈에 보이는 정점**에 새로 닿았나 — QA가 「몇 번 쫑긋했나」를 셀 때 쓰는 문턱.
        /// 걸음 단위가 된 뒤로는 「그 순간 서 있나」를 표본으로 재면 대부분 0이 나온다(걸음 사이엔
        /// 풀려 있으니까). 세야 하는 것은 **횟수**다.
        /// </summary>
        public static bool CrossedPeak(float _previous, float _current, float _threshold)
        {
            return _previous < _threshold && _current >= _threshold;
        }

        /// <summary>
        /// 소리가 겹쳤을 때 무엇을 따를까 — **더 가까운 쪽이 이긴다.**
        /// 걸음은 이제 이벤트라 나중 것이 앞의 것을 덮지만, **같은 프레임에 둘 이상** 들리면
        /// 순서에 기대면 안 된다 — 그때 이 규칙이 정한다.
        /// </summary>
        public static bool Closer(float _candidateDistance, float _bestDistance)
        {
            return _candidateDistance < _bestDistance;
        }

        /// <summary>
        /// 두 귀가 같은 양으로 도는 것은 뻣뻣하다 — **소리 쪽 귀가 더 크게 돈다**(실제 동물과 같다).
        /// 소리 쪽(부호가 같은 쪽) 귀는 1, 반대쪽 귀는 <paramref name="_farRatio"/>만큼만 돈다.
        ///
        /// 반대쪽 귀를 덜 돌리는 것이 **실루엣을 지키는 장치이기도 하다**(2026-09-08):
        /// 먼 귀를 같은 각으로 돌리면 그 끝이 머리 정중선을 가로질러 접힌 것처럼 보였다.
        /// </summary>
        public static float SideScale(float _turnDeg, float _earLocalX, float _farRatio)
        {
            if (Mathf.Abs(_turnDeg) < 0.001f) return 1f;
            bool nearSide = (_turnDeg >= 0f) == (_earLocalX >= 0f);
            return nearSide ? 1f : Mathf.Clamp01(_farRatio);
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
