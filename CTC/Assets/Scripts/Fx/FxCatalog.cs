using UnityEngine;

namespace CTC.Runtime.Fx
{
    /// <summary>
    /// <see cref="FxId"/> → 프리팹 표(2026-09-14). 원본은 `Resources/Fx/FxCatalog.asset`이고
    /// 에디터 메뉴 `Tools/CTC/Fx/Build Skill Effects`가 굽는다.
    /// Resources에 두는 이유는 맵 분위기와 같다 — 네트워크 씬·프리팹을 다시 굽지 않고 모든 씬에서 쓴다.
    /// </summary>
    public class FxCatalog : ScriptableObject
    {
        public const string RESOURCE_PATH = "Fx/FxCatalog";
        public const int SIZE = 32;

        [SerializeField] private GameObject[] m_Prefabs = new GameObject[SIZE];

        public GameObject PrefabOf(FxId _id)
        {
            int i = (int)_id;
            return m_Prefabs != null && i >= 0 && i < m_Prefabs.Length ? m_Prefabs[i] : null;
        }
    }
}
