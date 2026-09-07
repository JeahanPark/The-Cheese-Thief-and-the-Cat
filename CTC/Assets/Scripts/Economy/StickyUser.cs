using Unity.Netcode;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 끈끈이 쿨다운(테크 v3) — 쥐 프리팹에 붙는다. 시계는 <c>NetworkManager.ServerTime</c>이라
    /// 서버가 적어 둔 값을 소유자가 그대로 읽는다(핫바가 남은 시간을 표시한다).
    /// 레벨은 <see cref="TechSlots"/>가 들고, 쿨 길이는 살 때가 아니라 쓸 때 TechInfo P3에서 읽는다.
    /// </summary>
    public class StickyUser : NetworkBehaviour
    {
        private readonly NetworkVariable<double> m_ReadyAt = new NetworkVariable<double>();

        /// <summary>남은 쿨다운(초). 0이면 지금 쓸 수 있다.</summary>
        public float CooldownRemaining
        {
            get
            {
                if (NetworkManager == null) return 0f;
                return (float)System.Math.Max(0.0, m_ReadyAt.Value - NetworkManager.ServerTime.Time);
            }
        }

        public bool Ready
        {
            get
            {
                if (NetworkManager == null) return false;
                return StickyLogic.Ready(m_ReadyAt.Value, NetworkManager.ServerTime.Time);
            }
        }

        /// <summary>서버 전용 — 장판을 깐 순간 부른다.</summary>
        public void ServerStartCooldown(float _seconds)
        {
            if (!IsServer) return;
            m_ReadyAt.Value = NetworkManager.ServerTime.Time + _seconds;
        }
    }
}
