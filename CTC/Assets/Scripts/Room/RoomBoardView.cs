using CTC.Runtime.Core;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 좌측 참가자 보드 (MVC의 View) — 세션이 울릴 때만 다시 그린다(규칙 3).
    /// 상태를 하나도 안 들고, 누가 방장인지도 세션에게 묻는다.
    /// </summary>
    public class RoomBoardView : MonoBehaviour
    {
        [SerializeField] private RoomSession m_Session;
        [SerializeField] private RoomMemberRow[] m_Rows;

        private void Start()
        {
            if (m_Session == null)
            {
                GameDebug.LogWarning("[Room] 보드에 RoomSession이 안 붙어 있다");
                return;
            }

            m_Session.ViewChanged.Subscribe(_ => Render()).AddTo(this);
        }

        private void Render()
        {
            if (m_Rows == null || m_Session == null) return;

            var nm = NetworkManager.Singleton;
            ulong self = nm != null ? nm.LocalClientId : 0UL;
            ulong host = nm != null ? NetworkManager.ServerClientId : 0UL;

            int count = m_Session.MemberCount;
            for (int i = 0; i < m_Rows.Length; i++)
            {
                RoomMemberRow row = m_Rows[i];
                if (row == null) continue;

                if (i >= count)
                {
                    row.BindEmpty();
                    continue;
                }

                RoomMemberState member = m_Session.GetMember(i);
                row.Bind(
                    RoomLogic.DisplayName(member.Name.ToString(), i),
                    member.ClientId == host,
                    member.ClientId == self);
            }
        }
    }
}
