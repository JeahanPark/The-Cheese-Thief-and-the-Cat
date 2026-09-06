using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    public enum RoomModuleRole { Normal, Fortress, Center, Landmark }

    /// <summary>
    /// 방 모듈 목록(2026-09-07). 에디터 메뉴(`Build Room Modules`)가 굽고 SceneComposer가 MapBuilder에 배선한다.
    /// 레이아웃(`RoomRect.ModuleId`)이 여기 id를 가리킨다 — 없으면 MapBuilder는 큐브 벽으로 후퇴한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomModuleCatalog", menuName = "CTC/Room Module Catalog")]
    public class RoomModuleCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string ModuleId;
            public GameObject Prefab;
            public string Footprint;      // RoomModuleLogic.FOOTPRINT_*
            public RoomModuleRole Role;
        }

        [SerializeField] private List<Entry> m_Entries = new List<Entry>();
        [SerializeField] private GameObject m_DoorFrame;
        [SerializeField] private GameObject m_WallPlug;

        public IReadOnlyList<Entry> Entries => m_Entries;
        public GameObject DoorFrame => m_DoorFrame;
        public GameObject WallPlug => m_WallPlug;

        public bool TryGet(string _moduleId, out GameObject _prefab)
        {
            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (m_Entries[i].ModuleId == _moduleId && m_Entries[i].Prefab != null)
                {
                    _prefab = m_Entries[i].Prefab;
                    return true;
                }
            }
            _prefab = null;
            return false;
        }

        /// <summary>발자국·역할이 맞는 모듈 id 목록 — 풀 빌더가 시드로 하나 고른다.</summary>
        public List<string> IdsFor(string _footprint, RoomModuleRole _role)
        {
            var ids = new List<string>();
            foreach (Entry e in m_Entries)
            {
                if (e.Footprint == _footprint && e.Role == _role) ids.Add(e.ModuleId);
            }
            return ids;
        }

        public void Set(List<Entry> _entries, GameObject _doorFrame, GameObject _wallPlug)
        {
            m_Entries = _entries;
            m_DoorFrame = _doorFrame;
            m_WallPlug = _wallPlug;
        }
    }
}
