using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 이 오브젝트가 어느 풀에서 나왔는지 적어 두는 표식.
    ///
    /// 인스턴스 id로 사전을 들 수도 있지만, 유니티가 그 id 타입을 바꾸는 중이라
    /// (GetInstanceID → GetEntityId → int 캐스트도 폐기 예정) 표식을 오브젝트에 직접 붙인다.
    /// 사전이 사라져 **돌려준 물건이 미아가 되는 일**도 이 방식에선 생기지 않는다.
    /// </summary>
    public class PooledMember : MonoBehaviour
    {
        [SerializeField] private string m_Key;

        public string Key => m_Key;

        public void Mark(string _key) => m_Key = _key;
    }
}
