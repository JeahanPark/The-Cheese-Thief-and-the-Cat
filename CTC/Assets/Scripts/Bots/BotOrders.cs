#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>지휘관이 봇 하나에게 내린 명령.</summary>
    public readonly struct BotOrder
    {
        public readonly BotOrderId Id;
        public readonly Vector3 Point;

        public BotOrder(BotOrderId _id, Vector3 _point)
        {
            Id = _id;
            Point = _point;
        }

        public static readonly BotOrder None = new BotOrder(BotOrderId.None, Vector3.zero);
        public bool HasOrder => Id != BotOrderId.None;
    }

    /// <summary>
    /// **명령판.** 지휘관이 쓰고 봇이 읽는다 (2026-08-12).
    ///
    /// 기술소개서(10p)의 `TEAM_ORDER`에 해당한다 — 지휘관 트리가 여기에 써 넣으면
    /// 봇 트리의 '지휘관명령' 가지가 그걸 읽어 수행한다. 둘은 서로를 모른다.
    /// </summary>
    public class BotOrders
    {
        private readonly Dictionary<ulong, BotOrder> m_Orders = new Dictionary<ulong, BotOrder>();

        public int Count => m_Orders.Count;

        public void Set(ulong _bot, BotOrder _order)
        {
            m_Orders[_bot] = _order;
        }

        public BotOrder Get(ulong _bot)
        {
            return m_Orders.TryGetValue(_bot, out BotOrder order) ? order : BotOrder.None;
        }

        public void Clear()
        {
            m_Orders.Clear();
        }
    }
}
#endif
