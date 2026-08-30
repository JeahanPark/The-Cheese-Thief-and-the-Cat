using System;
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 한 종류를 재사용하는 풀. MonoBehaviour가 아니다 — 만드는 방법(<c>_factory</c>)만 알면
    /// 프리팹이든 코드로 세운 오브젝트든 똑같이 담을 수 있다.
    ///
    /// 쉬는 것은 <c>SetActive(false)</c>로 접어 두고 부모 밑에 모아 둔다. 씬을 넘나들며 살아야 하므로
    /// 부모는 <see cref="PoolManager"/>가 DontDestroyOnLoad로 들고 있다.
    ///
    /// **꺼내 온 것이 죽어 있을 수 있다** — 씬 언로드나 누군가의 Destroy로. 그래서 Get은
    /// 살아 있는 것이 나올 때까지 버린다(Unity의 == 오버로드를 타야 하므로 `!= null`로 검사).
    /// </summary>
    public class SpawnPool
    {
        private readonly Func<GameObject> m_Factory;
        private readonly Transform m_Parent;
        private readonly Stack<GameObject> m_Idle = new Stack<GameObject>();

        private int m_Created;

        public SpawnPool(Func<GameObject> _factory, Transform _parent, int _prewarm = 0)
        {
            m_Factory = _factory;
            m_Parent = _parent;

            for (int i = 0; i < _prewarm; i++)
            {
                GameObject go = Create();
                if (go != null) Release(go);
            }
        }

        /// <summary>지금까지 만든 총 개수 — 풀이 새고 있는지 보는 계기판이다.</summary>
        public int CreatedCount => m_Created;

        public int IdleCount => m_Idle.Count;

        public GameObject Get()
        {
            while (m_Idle.Count > 0)
            {
                GameObject pooled = m_Idle.Pop();
                if (pooled == null) continue;   // 파괴된 잔해는 버린다

                pooled.SetActive(true);
                return pooled;
            }

            return Create();
        }

        public void Release(GameObject _go)
        {
            if (_go == null) return;

            _go.SetActive(false);
            if (m_Parent != null) _go.transform.SetParent(m_Parent, false);
            m_Idle.Push(_go);
        }

        /// <summary>쉬고 있는 것을 실제로 파괴한다 — 판이 끝나 메모리를 돌려줄 때만.</summary>
        public void Clear()
        {
            while (m_Idle.Count > 0)
            {
                GameObject pooled = m_Idle.Pop();
                if (pooled != null) UnityEngine.Object.Destroy(pooled);
            }
        }

        private GameObject Create()
        {
            if (m_Factory == null) return null;

            GameObject go = m_Factory();
            if (go == null) return null;

            m_Created++;
            if (m_Parent != null) go.transform.SetParent(m_Parent, false);
            return go;
        }
    }
}
