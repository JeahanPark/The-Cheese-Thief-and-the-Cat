using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>강아지가 무엇을 할지. 상태는 서버만 갖는다.</summary>
    public enum DogState
    {
        /// <summary>표적이 없다 — 그래프를 따라 돌아다닌다.</summary>
        Roam = 0,

        /// <summary>표적에게 간다.</summary>
        Chase = 1,

        /// <summary>사거리 안이다 — 쿨다운마다 문다.</summary>
        Bite = 2,
    }

    /// <summary>
    /// 강아지가 볼 수 있는 표적 하나. 순수 로직이 씬을 몰라도 되게 값만 담는다.
    /// `Id`는 `NetworkObjectId` — 프레임이 바뀌어도 같은 표적임을 알아보는 데 쓴다.
    /// </summary>
    public readonly struct DogTarget
    {
        public readonly ulong Id;
        public readonly Vector3 Pos;
        public readonly bool IsCat;
        public readonly bool Visible;

        public DogTarget(ulong _id, Vector3 _pos, bool _isCat, bool _visible)
        {
            Id = _id;
            Pos = _pos;
            IsCat = _isCat;
            Visible = _visible;
        }
    }

    /// <summary>
    /// 강아지의 판단 규칙. MonoBehaviour와 분리된 순수 함수(EditMode TDD).
    ///
    /// **누구 편도 아니다.** 표적은 역할로 고르지 않고 **거리로만** 고른다 — 문을 열어 준 쪽도
    /// 예외가 아니다. 그래야 강아지를 푸는 게 "쓰면 이기는 카드"가 아니라 무리수가 된다.
    ///
    /// 피해만 역할을 본다. 쥐 HP 100 · 고양이 HP 1000이라 한 값으로 물면 한쪽만 죽는다.
    /// </summary>
    public static class DogAiLogic
    {
        // 수치 원본은 CSV(BalanceInfo.csv)
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        public static int Hp => Balance.DogHp;
        public static float MoveSpeed => Balance.DogMoveSpeed;
        public static float DetectRadius => Balance.DogDetectRadius;
        public static float AggroDropSeconds => Balance.DogAggroDropSeconds;
        public static float RepathSeconds => Balance.DogRepathSeconds;
        public static float BiteRange => Balance.DogBiteRangeMeters;
        public static float BiteCooldown => Balance.DogBiteCooldown;
        public static float BarkInterval => Balance.DogBarkIntervalSeconds;

        /// <summary>없는 표적. `ulong`이라 -1을 못 쓴다.</summary>
        public const ulong NO_TARGET = ulong.MaxValue;

        /// <summary>
        /// 감지 반경 안에서 **보이는** 것 중 가장 가까운 표적. 없으면 <see cref="NO_TARGET"/>.
        /// 후보를 거르는 일(벽감 제외 등)은 부르는 쪽이 한다 — 여기는 씬을 모른다.
        /// </summary>
        public static ulong ChooseTarget(IReadOnlyList<DogTarget> _candidates, Vector3 _dogPos, float _detectRadius)
        {
            if (_candidates == null || _candidates.Count == 0) return NO_TARGET;

            ulong best = NO_TARGET;
            float bestSqr = _detectRadius * _detectRadius;

            for (int i = 0; i < _candidates.Count; i++)
            {
                DogTarget t = _candidates[i];
                if (!t.Visible) continue;

                float sqr = Flat(t.Pos - _dogPos).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                best = t.Id;
            }
            return best;
        }

        /// <summary>
        /// 쫓던 표적을 놓을 때인가. **보이는 동안에는 절대 안 놓는다** — 시야를 잃고
        /// `_dropSeconds`가 지나야 놓는다. 안 그러면 기둥 뒤를 스칠 때마다 표적이 튄다.
        /// </summary>
        public static bool ShouldDropAggro(bool _visibleNow, float _secondsSinceSeen, float _dropSeconds)
        {
            if (_visibleNow) return false;
            return _secondsSinceSeen >= _dropSeconds;
        }

        /// <summary>표적과의 거리로 정해지는 상태.</summary>
        public static DogState NextState(bool _hasTarget, float _distanceToTarget, float _biteRange)
        {
            if (!_hasTarget) return DogState.Roam;
            return _distanceToTarget <= _biteRange ? DogState.Bite : DogState.Chase;
        }

        /// <summary>
        /// 물기 피해. 구조물은 `RoleTag.Mouse`라 쥐 값을 받는다 — 규칙을 하나로 유지한다.
        /// </summary>
        public static int BiteDamage(bool _targetIsCat)
        {
            return _targetIsCat ? Balance.DogBiteDamageCat : Balance.DogBiteDamageMouse;
        }

        /// <summary>쿨다운을 흘린다. 0 이하면 물 수 있다.</summary>
        public static float TickCooldown(float _remaining, float _deltaSeconds)
        {
            float next = _remaining - _deltaSeconds;
            return next < 0f ? 0f : next;
        }

        /// <summary>제자리걸음으로 친 시간. 실제로 나아갔으면 0으로 되돌린다.</summary>
        public static float AccumulateStuck(
            float _stuckSeconds, float _movedThisTick, float _deltaSeconds, float _minSpeed)
        {
            if (_deltaSeconds <= 0f) return _stuckSeconds;
            return _movedThisTick >= _minSpeed * _deltaSeconds ? 0f : _stuckSeconds + _deltaSeconds;
        }

        /// <summary>이만큼 제자리면 길을 다시 짠다. 안 그러면 벽에 붙어 영원히 안 움직인다.</summary>
        public const float STUCK_SECONDS = 1.5f;

        /// <summary>제자리 판정 기준 속도 — 이 이하로 움직이면 안 나아간 것으로 본다.</summary>
        public const float STUCK_MIN_SPEED = 0.5f;

        /// <summary>y를 버린다 — 층이 없는 맵이라 높이차로 표적을 놓치면 안 된다.</summary>
        public static Vector3 Flat(Vector3 _v) => new Vector3(_v.x, 0f, _v.z);
    }
}
