using UnityEngine;

namespace CTC.Runtime.Map
{
    public enum RoomModuleSocketKind { Door, Cheese, Alcove, Box, Fortress, Light }

    /// <summary>모듈 프리팹 안의 자리 표식(2026-09-07). 1단계는 Door만 쓴다 — 나머지는 2단계(자리 배선)가 읽는다.</summary>
    public class RoomModuleSocket : MonoBehaviour
    {
        [SerializeField] private RoomModuleSocketKind m_Kind;

        /// <summary>Door: 0=N 1=S 2=E 3=W. 다른 종류는 0부터 번호.</summary>
        [SerializeField] private int m_Index;

        public RoomModuleSocketKind Kind => m_Kind;
        public int Index => m_Index;

        public void Set(RoomModuleSocketKind _kind, int _index)
        {
            m_Kind = _kind;
            m_Index = _index;
        }
    }
}
