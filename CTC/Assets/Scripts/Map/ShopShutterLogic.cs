using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상점 철문 판정 — 순수 함수(2026-09-19, 기획 `2026-09-19-shop-booth-design.md` §2).
    ///
    /// 고양이가 가까이 오면 점원이 철문을 **쾅** 내리고, 멀어지면 **드르륵** 올린다.
    /// 닫는 쪽은 즉시(경보 반경 안에 한 마리라도), 여는 쪽은 느긋하다(열림 반경 밖에 일정 시간) —
    /// 경계선에서 고양이가 서성여도 철문이 떨지 않게 **닫힘 반경 &lt; 열림 반경**(히스테리시스)이다.
    /// 반경·시간은 CSV(`ShopShutter*`)가 원본이다.
    /// </summary>
    public static class ShopShutterLogic
    {
        /// <summary>서버 판정 주기.</summary>
        public const float TICK_SECONDS = 0.2f;

        /// <summary>닫힐 때 — 쾅(가속). 짧아야 「놀라서 내렸다」로 읽힌다.</summary>
        public const float CLOSE_SECONDS = 0.35f;

        /// <summary>열릴 때 — 드르륵(감속). 조심스럽게 연다.</summary>
        public const float OPEN_SECONDS = 0.8f;

        /// <summary>
        /// 이 고양이가 점원을 놀라게 하나 — **살금살금이면 더 가까이 와야 한다**(점원도 발소리를 듣는다).
        /// </summary>
        public static bool Alarms(float _distance, bool _sneaking, float _closeMeters, float _sneakMeters)
            => _distance < (_sneaking ? _sneakMeters : _closeMeters);

        /// <summary>
        /// 다음 닫힘 상태.
        /// </summary>
        /// <param name="_anyAlarm">경보 반경 안에 고양이가 있나 — 있으면 무조건 닫는다.</param>
        /// <param name="_anyNear">열림 반경 안에 고양이가 있나 — 닫혀 있으면 계속 닫아 둔다.</param>
        /// <param name="_quietSeconds">모든 고양이가 열림 반경 밖으로 나간 지 몇 초.</param>
        public static bool NextClosed(bool _closed, bool _anyAlarm, bool _anyNear, float _quietSeconds, float _reopenSeconds)
        {
            if (_anyAlarm) return true;
            if (!_closed) return false;
            if (_anyNear) return true;
            return _quietSeconds < _reopenSeconds;
        }

        /// <summary>
        /// 철문이 내려온 정도(0 = 다 올라감 · 1 = 다 내려옴). 방향이 바뀐 순간의 정도 <paramref name="_from"/>에서
        /// 바뀐 지 <paramref name="_since"/>초 — 내려가는 중이면 가속(쾅), 올라가는 중이면 감속(드르륵).
        /// 도중에 뒤집혀도 그 자리에서 이어 가므로 튀지 않는다.
        /// </summary>
        public static float Lowered(float _from, bool _closing, float _since)
        {
            if (_closing)
            {
                float t = CLOSE_SECONDS <= 0f ? 1f : Mathf.Clamp01(_since / CLOSE_SECONDS);
                return Mathf.Lerp(_from, 1f, t * t);
            }
            float u = OPEN_SECONDS <= 0f ? 1f : Mathf.Clamp01(_since / OPEN_SECONDS);
            return Mathf.Lerp(_from, 0f, 1f - (1f - u) * (1f - u));
        }

        /// <summary>움직임이 끝났나.</summary>
        public static bool Settled(bool _closing, float _since) => _since >= (_closing ? CLOSE_SECONDS : OPEN_SECONDS);
    }
}
