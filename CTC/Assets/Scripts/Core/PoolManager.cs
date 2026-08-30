using System;
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 동적으로 생기고 사라지는 것들(원샷 소리·이펙트)을 재사용으로 관리한다.
    ///
    /// **왜 필요한가**: 발소리 하나마다 `new GameObject` + `AddComponent` + `Destroy`를 하면
    /// 쥐가 여덟이고 고양이가 뛰는 판에서 그 비용이 계속 쌓인다. 풀은 같은 껍데기를 돌려 쓴다.
    ///
    /// 씬을 넘어가도 살아야 해서 DontDestroyOnLoad다 — 처음 쓰일 때 스스로 선다(배선 불필요).
    /// 프리팹으로도, 코드로 세우는 것으로도 쓸 수 있게 두 갈래를 연다:
    ///   - <see cref="Spawn(GameObject, Vector3, Quaternion)"/> — 프리팹 복제
    ///   - <see cref="SpawnFrom"/> — 이름표 + 만드는 법을 직접 주는 경우(원샷 AudioSource 등)
    ///
    /// 돌려주는 것은 <see cref="Despawn"/>이다. 시간이 지나면 알아서 돌아오게 하려면
    /// <see cref="DespawnAfter"/> — 안에서 <see cref="PooledAutoRelease"/>가 시계를 든다.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        private static PoolManager s_Instance;

        /// <summary>처음 부를 때 스스로 선다 — 씬마다 배선할 것이 없다.</summary>
        public static PoolManager Instance
        {
            get
            {
                if (s_Instance != null) return s_Instance;

                var go = new GameObject("PoolManager");
                s_Instance = go.AddComponent<PoolManager>();
                DontDestroyOnLoad(go);
                return s_Instance;
            }
        }

        /// <summary>플레이 종료·도메인 리로드 뒤에도 옛 참조가 남지 않게 지운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_Instance = null;

        private readonly Dictionary<string, SpawnPool> m_Pools = new Dictionary<string, SpawnPool>();

        private Transform m_Idle;

        private Transform IdleRoot
        {
            get
            {
                if (m_Idle != null) return m_Idle;

                var go = new GameObject("Idle");
                go.transform.SetParent(transform, false);
                m_Idle = go.transform;
                return m_Idle;
            }
        }

        /// <summary>프리팹 하나를 꺼내 쓴다. 돌려줄 때는 <see cref="Despawn"/>.</summary>
        public GameObject Spawn(GameObject _prefab, Vector3 _position, Quaternion _rotation)
        {
            if (_prefab == null)
            {
                GameDebug.LogWarning("[Pool] 프리팹이 없다");
                return null;
            }

            string key = PrefabKey(_prefab);
            GameObject go = Take(key, () => Instantiate(_prefab));
            if (go == null) return null;

            go.transform.SetParent(null, false);
            go.transform.SetPositionAndRotation(_position, _rotation);
            return go;
        }

        /// <summary>
        /// 프리팹이 아니라 코드로 세우는 것 — 이름표(<c>_key</c>)가 곧 풀의 구분이다.
        /// 같은 이름표면 같은 풀을 쓰므로, 성격이 다른 것에 같은 이름을 주면 안 된다.
        /// </summary>
        public GameObject SpawnFrom(string _key, Func<GameObject> _factory)
        {
            if (string.IsNullOrEmpty(_key) || _factory == null) return null;
            return Take(_key, _factory);
        }

        public void Despawn(GameObject _instance)
        {
            if (_instance == null) return;

            var member = _instance.GetComponent<PooledMember>();
            if (member == null || string.IsNullOrEmpty(member.Key))
            {
                // 풀에서 나온 물건이 아니다 — 남의 오브젝트를 조용히 삼키지 않는다.
                GameDebug.LogWarning($"[Pool] 풀 소속이 아닌 것을 돌려주려 한다: {_instance.name}");
                return;
            }

            if (m_Pools.TryGetValue(member.Key, out SpawnPool pool)) pool.Release(_instance);
        }

        /// <summary>_seconds 뒤에 스스로 돌아온다. 그 전에 Despawn하면 시계는 취소된다.</summary>
        public void DespawnAfter(GameObject _instance, float _seconds)
        {
            if (_instance == null) return;

            var timer = _instance.GetComponent<PooledAutoRelease>();
            if (timer == null) timer = _instance.AddComponent<PooledAutoRelease>();
            timer.Begin(_seconds);
        }

        /// <summary>판이 시작되기 전에 미리 만들어 둔다 — 첫 발소리에서 끊기지 않게.</summary>
        public void Prewarm(GameObject _prefab, int _count)
        {
            if (_prefab == null || _count <= 0) return;

            string key = PrefabKey(_prefab);
            EnsurePool(key, () => Instantiate(_prefab), _count);
        }

        /// <summary>계기판 — 풀이 새는지 본다(만든 총량이 계속 늘면 어딘가 안 돌려주고 있다).</summary>
        public string DebugSummary()
        {
            var sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, SpawnPool> pair in m_Pools)
            {
                sb.AppendLine($"{pair.Key}: 만듦 {pair.Value.CreatedCount} · 쉼 {pair.Value.IdleCount}");
            }
            return sb.ToString();
        }

        private GameObject Take(string _key, Func<GameObject> _factory)
        {
            SpawnPool pool = EnsurePool(_key, _factory, 0);
            GameObject go = pool.Get();
            if (go == null) return null;

            var member = go.GetComponent<PooledMember>();
            if (member == null) member = go.AddComponent<PooledMember>();
            member.Mark(_key);
            return go;
        }

        private SpawnPool EnsurePool(string _key, Func<GameObject> _factory, int _prewarm)
        {
            if (m_Pools.TryGetValue(_key, out SpawnPool pool)) return pool;

            pool = new SpawnPool(_factory, IdleRoot, _prewarm);
            m_Pools[_key] = pool;
            return pool;
        }

        private static string PrefabKey(GameObject _prefab) => "prefab:" + _prefab.name;
    }
}
