using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 머리 위 이름표. 몸의 주인을 <see cref="RoomSession"/>에서 찾아 이름을 붙이고,
    /// 매 프레임 카메라를 향해 돌린다(빌보드).
    ///
    /// 이름은 참가자 목록이 바뀔 때만 다시 읽는다 — 매 프레임 목록을 뒤지지 않는다(규칙 3).
    /// </summary>
    public class RoomNameTag : NetworkBehaviour
    {
        [SerializeField] private TMP_Text m_Label;
        [SerializeField] private Transform m_Pivot;

        private RoomSession m_Session;
        private System.IDisposable m_Subscription;

        public override void OnNetworkSpawn()
        {
            m_Session = FindFirstObjectByType<RoomSession>();
            if (m_Session == null) return;

            m_Subscription = m_Session.ViewChanged.Subscribe(_ => Refresh());
        }

        public override void OnNetworkDespawn()
        {
            m_Subscription?.Dispose(); // 순수 C# 객체 — 규칙 9 예외
            m_Subscription = null;
        }

        private void Refresh()
        {
            if (m_Label == null || m_Session == null) return;

            int idx = m_Session.IndexOf(OwnerClientId);
            if (idx < 0)
            {
                m_Label.text = "";
                return;
            }

            RoomMemberState member = m_Session.GetMember(idx);
            m_Label.text = RoomLogic.DisplayName(member.Name.ToString(), idx);
        }

        private void LateUpdate()
        {
            if (m_Pivot == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            m_Pivot.rotation = Quaternion.LookRotation(m_Pivot.position - cam.transform.position);
        }
    }
}
