using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 따봉 마커의 **표식**. 하는 일은 없다 — 「이것이 따봉 마커다」를 코드로 말하는 게 전부다.
    ///
    /// 계측이 `gameObject.name == "CheerMarker"`로 세던 것을 대신한다(2026-09-09 감사).
    /// 이름 문자열로 세면 마커를 개명하는 순간 수가 **영원히 0**이 되고,
    /// 「고양이 화면에는 따봉이 0이어야 한다」는 검사가 **항상 통과**한다 —
    /// 즉 개명이 검사를 조용히 무력화한다. 컴포넌트는 개명해도 따라온다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CheerMarker : MonoBehaviour
    {
    }
}
