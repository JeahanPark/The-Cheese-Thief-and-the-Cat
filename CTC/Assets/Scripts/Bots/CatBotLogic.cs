using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 고양이 봇이 **무엇을 할지** 고르는 규칙(순수).
    ///
    /// 이 봇의 승률이 곧 "소리 밸런스가 쓸모 있는가"의 답이다. 그래서 판단은
    /// **소리와 눈으로 얻은 것만** 쓰고, 규칙은 단순해야 한다 — 영리해지면 그 순간
    /// 재는 대상이 밸런스가 아니라 봇의 실력이 된다.
    /// </summary>
    public static class CatBotLogic
    {
        /// <summary>
        /// 소리 단서가 여럿이면 무엇부터 갈까. **가까울수록·최근일수록** 값이 크다.
        /// 오래된 소리를 쫓으면 이미 떠난 자리로 가고, 먼 소리를 쫓으면 도착 전에 식는다.
        /// </summary>
        public static float LeadScore(float _distance, float _age)
        {
            return 1f / (Mathf.Max(1f, _distance) * (1f + Mathf.Max(0f, _age)));
        }

        /// <summary>
        /// 조사하러 갔는데 아무도 없었다 — 이만큼 지나면 그 단서를 버린다.
        /// 없으면 봇이 빈 자리를 영원히 서성인다.
        /// </summary>
        public const float GIVE_UP_SECONDS = 5f;

        /// <summary>
        /// **추격이 이길 수 있는 경주인가**(2026-08-24). 요새를 부수는 중에 쥐가 반대편에
        /// 나와 보여도, 쥐가 요새로 되돌아가는 시간이 내가 잡는 시간보다 짧으면 그 추격은
        /// 진 경주다 — 쫓는 순간 벽 부수기 진행만 날리고 쥐는 다시 들어간다(실관찰: 이
        /// 왕복이 무한 반복됐다). 지면 벽을 계속 판다: 벽 HP는 회복이 없어 결심만 유지하면
        /// 반드시 끝난다(480 = 노업 여덟 방 ≈ 16초).
        ///
        /// 쥐 봇 <see cref="MouseBotLogic.ChooseRefuge"/>의 "이길 수 있는 경주만 뛴다"를
        /// 고양이에게 거울로 적용한 것.
        /// </summary>
        public static bool IsChaseWinnable(
            float _catToPrey, float _preyToRefuge, float _catSpeed, float _preySpeed)
        {
            if (_preyToRefuge <= 0f) return false;      // 이미 요새 안 — 벽이 답이다
            float closing = _catSpeed - _preySpeed;
            if (closing <= 0.01f) return false;         // 못 좁힌다 — 쫓아 봐야 평행선
            float catchSeconds = _catToPrey / closing;
            float refugeSeconds = _preyToRefuge / _preySpeed;
            return catchSeconds < refugeSeconds;
        }
    }
}
