using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>방(지형) 사각형 — center/size는 x(동)·z(북) 평면. 경계 = center ± size/2.</summary>
    [System.Serializable]
    public struct RoomRect
    {
        public string Id;
        public Vector2 Center;
        public Vector2 Size;

        public Vector2 Min => Center - Size * 0.5f;
        public Vector2 Max => Center + Size * 0.5f;
        public bool Contains(Vector2 _p) =>
            _p.x >= Min.x && _p.x <= Max.x && _p.y >= Min.y && _p.y <= Max.y;
    }

    /// <summary>통로/미로 세그먼트 — 중심선 두 끝점(x,z) + 폭. 굽기 시 폭만큼의 바닥 통로 박스.</summary>
    [System.Serializable]
    public struct CorridorSeg
    {
        public Vector2 From;
        public Vector2 To;
        public float Width;
    }

    /// <summary>치즈 지점 — 위치(x,z)·등급 1~7·반경.</summary>
    [System.Serializable]
    public struct CheesePlacement
    {
        public Vector2 Pos;
        public int Grade;
        public float Radius;
    }

    /// <summary>요새 자리 — 위치(x,z)·입구 yaw(도). 입구 앞 1.8m가 쥐 스폰.</summary>
    [System.Serializable]
    public struct FortressPlacement
    {
        public Vector2 Pos;
        public float YawDeg;
    }

    /// <summary>
    /// 벽감 자리 — 위치(x,z)·벽을 등지는 yaw(도)·뒤에 막혀 있는 것 (v3.8).
    /// 방 벽면에 붙인다. 복도에 두지 않는 이유: 부수는 19초 동안 한 자리에 묶이는데
    /// 복도에서는 도망칠 방향이 둘뿐이라 사형선고가 된다.
    ///
    /// <see cref="BoxContents"/>는 <see cref="AlcovePayload.Box"/>일 때 그 상자가 품는 것이다
    /// (2026-08-11). 자리마다 정해 두므로 "저 벽 뒤에 깡패권이 있다"를 외울 수 있다.
    /// </summary>
    [System.Serializable]
    public struct AlcovePlacement
    {
        public Vector2 Pos;
        public float YawDeg;
        public AlcovePayload Payload;
        public System.Collections.Generic.List<LootEntry> BoxContents;
    }

    /// <summary>
    /// 방 안 엄폐물 (2026-08-11). 기둥·상자더미처럼 **시야를 끊는 물건**이다.
    ///
    /// 격자 맵은 방과 복도가 축에 정렬돼 있어 문 구멍이 일직선으로 뚫린다 —
    /// 봇 실측에서 고양이가 33m 떨어진 쥐를 **첫 판단 틱에** 봤다(2026-08-11).
    /// 그러면 소리로 찾을 이유가 없어져 발소리·파밍 소리가 아무 일도 하지 않는다.
    ///
    /// 시야 판정은 눈높이 1.0m의 레이 하나라(`SightLine`) 그보다 높으면 끊긴다.
    /// </summary>
    [System.Serializable]
    public struct ObstaclePlacement
    {
        public Vector2 Pos;
        public Vector2 Size;
    }

    /// <summary>미로 프리셋 — 세그먼트 묶음 + 배타 방 id(같은 영역이면 둘 중 하나만).</summary>
    [System.Serializable]
    public struct MazePreset
    {
        public string Id;
        public string ExclusiveRoomId;   // 이 방과 동시 선택 금지
        public List<CorridorSeg> Segments;
    }
}
