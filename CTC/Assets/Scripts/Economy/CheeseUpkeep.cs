using CTC.Runtime.Combat;
using CTC.Runtime.Match;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// v2 유지비(쥐 전용): 서버가 10초마다 치즈를 소모시키고, 부족하면 HP를 대신 깎는다.
    /// 매치 진행 중(결과 없음)에만 동작 — 결과 화면에서 굶어 죽는 것 방지.
    /// </summary>
    [RequireComponent(typeof(CheeseWallet), typeof(Health))]
    public class CheeseUpkeep : NetworkBehaviour
    {
        private CheeseWallet m_Wallet;
        private Health m_Health;
        private MatchManager m_Match;
        private float m_Timer;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            m_Wallet = GetComponent<CheeseWallet>();
            m_Health = GetComponent<Health>();
            m_Match = FindFirstObjectByType<MatchManager>();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || m_Health.IsDead) return;
            if (m_Match != null && m_Match.Result != MatchResult.None) return;

            m_Timer += Time.deltaTime;
            if (m_Timer < SurvivalLogic.UPKEEP_INTERVAL_SECONDS) return;
            m_Timer -= SurvivalLogic.UPKEEP_INTERVAL_SECONDS;

            int hunger = SurvivalLogic.StarvationDamage(m_Wallet.Cheese);
            m_Wallet.SpendUpTo(SurvivalLogic.UPKEEP_COST);
            if (hunger > 0) m_Health.TakeDamage(hunger);
        }
    }
}
