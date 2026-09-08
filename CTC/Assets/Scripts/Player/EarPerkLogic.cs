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

        /// <summary>들은 지 _sinceHeard초 — 아직 귀를 세우고 있어야 하나(음수 시각 = 아직 못 들었다).</summary>
        public static bool Holding(float _sinceHeard, float _holdSeconds)
        {
            return _sinceHeard >= 0f && _sinceHeard < Mathf.Max(0f, _holdSeconds);
        }

        /// <summary>
        /// 소리가 겹쳤을 때 무엇을 따를까 — **더 가까운 쪽이 이긴다.**
        ///
        /// 「최근」을 안 쓰는 이유: 발소리는 매 프레임 다시 '지금'이라 일회성 소리를 항상 눌러 버린다.
        /// 두 종류가 함께 가진 축은 거리뿐이고, 사람 귀가 실제로 향하는 것도 가까워서 큰 쪽이다.
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

