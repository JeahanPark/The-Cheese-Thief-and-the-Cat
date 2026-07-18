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
        public bool Contains(Vector2 p) =>
            p.x >= Min.x && p.x <= Max.x && p.y >= Min.y && p.y <= Max.y;
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

    /// <summary>미로 프리셋 — 세그먼트 묶음 + 배타 방 id(같은 영역이면 둘 중 하나만).</summary>
    [System.Serializable]
    public struct MazePreset
    {
        public string Id;
        public string ExclusiveRoomId;   // 이 방과 동시 선택 금지
        public List<CorridorSeg> Segments;
    }
}
