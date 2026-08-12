#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using CTC.Runtime.Ai.Bt;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// **고양이 팀의 지휘관** (2026-08-12).
    ///
    /// 기술소개서(리그오브레전드 10p)의 명령계통구조다. 지휘관도 행동트리를 갖고,
    /// 그 잎이 `BotOrders`에 명령을 써 넣는다. 봇은 제 트리의 '지휘관명령' 가지에서 그걸 읽는다 —
    /// 둘은 서로를 모른다.
    ///
    /// **지휘관은 쥐 좌표를 모른다.** 고양이들이 **실제로 보고 들은 것**과 화면에 뜨는 것
    /// (갇힌 수·남은 시간)만 본다. 서버는 전지적이라 그냥 읽을 수 있지만, 그 순간 봇 승률이
    /// "밸런스가 좋다"가 아니라 "봇이 반칙했다"를 뜻하게 된다 — 이 봇의 존재 이유가 무너진다.
    ///
    /// 사람 둘이 채팅으로 공유하는 딱 그만큼이다.
    /// </summary>
    public class BotCommander
    {
        /// <summary>명령을 다시 짜는 간격. 매 틱 바꾸면 고양이가 갈팡질팡한다.</summary>
        private const float ORDER_INTERVAL = 2f;

        private readonly BotOrders m_Orders;
        private readonly List<BotAgent> m_Cats = new List<BotAgent>();
        private readonly BtNode m_Tree;
        private readonly System.Random m_Rng;

        /// <summary>
        /// 수색 지점에 이만큼 붙으면 다 훑은 것으로 보고 새 자리를 준다.
        /// **도착하기 전에는 안 바꾼다** — 2초마다 새 좌표를 주면 고양이가 목표만 갈아타다
        /// 아무 데도 못 간다(2026-08-12 실측: 경로 소진 상태로 28초).
        /// </summary>
        private const float SEARCH_ARRIVE = 6f;

        /// <summary>그래도 이만큼 지나면 새 자리를 준다 — 못 가는 자리를 붙들고 있지 않게.</summary>
        private const float SEARCH_TIMEOUT = 20f;

        private readonly Dictionary<ulong, Vector3> m_SearchPoints = new Dictionary<ulong, Vector3>();
        private readonly Dictionary<ulong, float> m_SearchSince = new Dictionary<ulong, float>();

        private float m_Timer;
        private float m_HalfExtent = 60f;
        private int m_JailedMice;
        private int m_FreeMice;

        /// <summary>QA — 지금 몇 개의 명령이 나가 있나.</summary>
        public int OrderCount => m_Orders != null ? m_Orders.Count : 0;

        public BotCommander(BotOrders _orders, int _seed)
        {
            m_Orders = _orders;
            m_Rng = new System.Random(_seed);
            m_Tree = Build();
        }

        /// <summary>지휘할 고양이를 등록한다(매치마다 다시).</summary>
        public void SetCats(IReadOnlyList<BotAgent> _cats)
        {
            m_Cats.Clear();
            foreach (BotAgent cat in _cats)
            {
                if (cat != null && cat.Role == RoleId.Cat) m_Cats.Add(cat);
            }
            if (m_Orders != null) m_Orders.Clear();
            m_SearchPoints.Clear();
            m_SearchSince.Clear();
        }

        public void SetMapExtent(float _halfExtent)
        {
            if (_halfExtent > 0f) m_HalfExtent = _halfExtent;
        }

        public void Tick(float _deltaSeconds)
        {
            if (m_Cats.Count == 0 || m_Orders == null) return;

            m_Timer -= _deltaSeconds;
            if (m_Timer > 0f) return;

            m_Timer = ORDER_INTERVAL;
            CountMice();
            m_Tree.Tick(ORDER_INTERVAL);
        }

        /// <summary>
        /// <code>
        /// Selector
        /// ├ [갇힌 쥐 있다] → 한 마리에게 "감옥 지켜", 나머지는 구역 수색
        /// └ 기본            → 맵을 갈라 각자 수색
        /// </code>
        /// </summary>
        private BtNode Build()
        {
            return new BtSelector(
                new BtSequence(
                    new BtCondition(() => BotOrderLogic.ShouldGuardJail(m_JailedMice, m_FreeMice)),
                    BtAction.Once(GuardAndSearch)),
                BtAction.Once(SearchZones));
        }

        // 갇힌 쥐가 있으면 한 마리는 감옥 앞을 지킨다 — 구하러 오는 쥐가 곧 다음 사냥감이다.
        // 둘 다 지키면 맵이 통째로 비므로 **한 마리만** 보낸다.
        private void GuardAndSearch()
        {
            for (int i = 0; i < m_Cats.Count; i++)
            {
                if (BotOrderLogic.IsJailGuard(i, m_Cats.Count) && TryNearestJail(m_Cats[i], out Vector3 jail))
                {
                    m_Orders.Set(m_Cats[i].Id, new BotOrder(BotOrderId.GuardJail, jail));
                    continue;
                }
                m_Orders.Set(m_Cats[i].Id, new BotOrder(BotOrderId.Search, SearchPointFor(i)));
            }
        }

        // 맵을 세로로 갈라 각자 한쪽을 맡는다. 이것만으로도 **둘이 한 몸처럼 붙어 다니는 것**이
        // 사라진다(2026-08-12 실측: 두 고양이의 목표가 늘 같은 좌표였다).
        private void SearchZones()
        {
            for (int i = 0; i < m_Cats.Count; i++)
            {
                m_Orders.Set(m_Cats[i].Id, new BotOrder(BotOrderId.Search, SearchPointFor(i)));
            }
        }

        /// <summary>
        /// 맡은 구역 안에서 훑을 자리. **도착하거나 시간이 지나기 전에는 같은 자리를 유지한다** —
        /// 명령을 2초마다 새로 뽑으면 고양이가 목표만 갈아타다 아무 데도 도착하지 못한다.
        /// </summary>
        private Vector3 SearchPointFor(int _index)
        {
            BotAgent cat = m_Cats[_index];
            ulong id = cat.Id;

            bool arrived = m_SearchPoints.TryGetValue(id, out Vector3 point)
                           && cat.DistanceTo(point) <= SEARCH_ARRIVE;
            bool stale = !m_SearchSince.TryGetValue(id, out float since)
                         || Time.time - since >= SEARCH_TIMEOUT;

            if (!m_SearchPoints.ContainsKey(id) || arrived || stale)
            {
                Vector3 zone = BotOrderLogic.ZoneCenter(_index, m_Cats.Count, m_HalfExtent);
                point = BotOrderLogic.SearchPoint(zone, m_HalfExtent, m_Cats.Count, m_Rng);

                // 뽑은 좌표가 벽 속일 수 있다 — 걸을 수 있는 자리로 당긴다
                if (UnityEngine.AI.NavMesh.SamplePosition(
                        point, out UnityEngine.AI.NavMeshHit hit, 12f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    point = hit.position;
                }

                m_SearchPoints[id] = point;
                m_SearchSince[id] = Time.time;
            }
            return point;
        }

        // 감옥 자리는 화면에 뜨는 정보다(맵에 고정된 구조물). 갇힌 쥐가 있는 곳을 고른다.
        private static bool TryNearestJail(BotAgent _cat, out Vector3 _jail)
        {
            _jail = Vector3.zero;
            float bestSqr = float.MaxValue;
            bool found = false;

            foreach (JailedTag jailed in Object.FindObjectsByType<JailedTag>(FindObjectsSortMode.None))
            {
                if (!jailed.Jailed) continue;

                float sqr = (jailed.transform.position - _cat.Position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                _jail = jailed.transform.position;
                found = true;
            }
            return found;
        }

        // 갇힌 수·자유 쥐 수는 화면(감옥 HUD)에 뜨는 값이라 지휘관이 알아도 된다.
        private void CountMice()
        {
            m_JailedMice = 0;
            m_FreeMice = 0;

            foreach (RoleTag tag in Object.FindObjectsByType<RoleTag>(FindObjectsSortMode.None))
            {
                if (tag.Role != RoleId.Mouse) continue;
                if (tag.GetComponent<Player.PlayerMovement>() == null) continue;

                var health = tag.GetComponent<Combat.Health>();
                if (health == null || health.IsDead) continue;

                var jailed = tag.GetComponent<JailedTag>();
                if (jailed != null && jailed.Jailed) m_JailedMice++;
                else m_FreeMice++;
            }
        }
    }
}
#endif
