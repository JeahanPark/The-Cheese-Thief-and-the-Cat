using Unity.Netcode;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 가시 벽(요새 Lv3) — 이 요새를 치는 고양이가 타격당 반사 피해를 받는다.
    /// 값은 서버가 스폰 **전에** 넣는다(<see cref="CTC.Runtime.Economy.ShopController"/>).
    /// 요새 프리팹에는 레벨과 무관하게 항상 붙어 있다 — 0이면 아무 일도 없다.
    /// 프리팹을 레벨별로 늘리지 않으려는 선택이다.
    /// </summary>
    public class ThornWall : NetworkBehaviour
    {
        private readonly NetworkVariable<int> m_Reflect = new NetworkVariable<int>();

        // 스폰 전에 받아 두는 값 — 스폰 전 NetworkVariable에 직접 쓰면 NGO가
        // "doesn't know its NetworkBehaviour yet" 경고를 매 Lv3 요새마다 뱉는다.
        private int m_PendingReflect;

        /// <summary>타격 1회당 되돌려주는 피해. 0이면 가시 벽이 아니다.</summary>
        public int Reflect => m_Reflect.Value;

        /// <summary>서버 전용 — 스폰 전에 부른다.</summary>
        public void ServerSet(int _reflect)
        {
            // 스폰 전이면 보관만 하고 OnNetworkSpawn에서 쓴다(경고 회피).
            m_PendingReflect = _reflect;
            if (IsSpawned && IsServer) m_Reflect.Value = _reflect;
        }

        public override void OnNetworkSpawn()
        {
            // 스폰 전에 받아 둔 값을 여기서 한 번에 올린다 — 서버만 쓴다.
            if (IsServer) m_Reflect.Value = m_PendingReflect;
        }
    }
}
