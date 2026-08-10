using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상자에 들어갈 수 있는 물건. **값은 배치 데이터에 직렬화되므로 바꾸지 않는다** —
    /// 새 물건은 뒤에 추가만 한다.
    ///
    /// 지금은 치즈뿐이다. 늘어날 자리를 미리 만들어 두지 않는 이유는, 없는 물건의 규칙을
    /// 지금 정하면 나중에 그 규칙에 맞춰 물건을 만들게 되기 때문이다.
    /// </summary>
    public enum LootId
    {
        Cheese = 0,
    }

    /// <summary>상자 안에 든 물건 하나 — 무엇을 얼마나.</summary>
    [System.Serializable]
    public struct LootEntry
    {
        public LootId Id;
        public int Amount;
    }

    /// <summary>
    /// 상자 자리와 **그 상자에 든 것**(v3.9). 내용물은 배치 시점에 정해진다(고정 방식) —
    /// 확률 표가 아니라서 "저기 가면 그게 있다"를 외울 수 있고, 레벨디자인이 보상을 설계한다.
    /// </summary>
    [System.Serializable]
    public struct BoxPlacement
    {
        public Vector2 Pos;
        public float YawDeg;
        public List<LootEntry> Contents;
    }
}
