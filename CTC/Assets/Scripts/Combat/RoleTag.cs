using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>프리팹에 박아두는 역할 식별 — 컴포넌트 유무 추론 대신 명시 태그.</summary>
    public class RoleTag : MonoBehaviour
    {
        [SerializeField] private RoleId m_Role = RoleId.None;

        public RoleId Role => m_Role;
    }
}
