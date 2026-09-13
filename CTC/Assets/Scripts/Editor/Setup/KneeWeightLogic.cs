using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 뒷다리 정점을 허벅지·정강이에 나누는 비율 — 순수 static(2026-09-13).
    ///
    /// **블렌더 <c>tools/blender/add_knees.py</c>의 <c>shin_share</c>와 같은 식이다.** 블렌더에서 잰
    /// 바닥·렌더와 게임 화면이 갈리면 안 되므로 상수 둘을 양쪽에 똑같이 둔다(바꾸면 둘 다 바꾼다).
    /// </summary>
    public static class KneeWeightLogic
    {
        /// <summary>
        /// 무릎 자리 — 허벅지 머리(0)에서 원래 발목 끝(1)까지의 비율. 뒷다리 메시가 링 셋짜리
        /// 원기둥(z = 188 · 97 · 6mm)이고 가운데 링이 뼈 155.2mm 중 이 지점이다(2026-09-13 실측).
        /// </summary>
        public const float KNEE = 0.617f;

        /// <summary>반반 섞이는 폭의 절반(= 20mm / 155.2mm). 가운데 링은 반반, 위아래 링은 한쪽 전부.</summary>
        public const float HALF = 0.129f;

        /// <summary>뼈 축 위 정규화 위치 <paramref name="_s"/>의 정점이 정강이에 줄 몫(0~1).</summary>
        public static float ShinShare(float _s)
        {
            float t = Mathf.Clamp01((_s - (KNEE - HALF)) / (2f * HALF));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 허벅지 로컬 좌표 <paramref name="_pointInThigh"/>를 뼈 축 위 정규화 위치로 옮긴다.
        /// <paramref name="_kneeInThigh"/>는 정강이 뼈의 로컬 위치(= 허벅지 로컬에서 본 무릎)다 —
        /// 무릎이 <see cref="KNEE"/> 지점에 있으므로 거기에 사영한 길이를 그 비율로 늘린다.
        /// 뼈 로컬에 스케일(팩 리그의 1/100)이 섞여 있어도 둘이 같은 공간이라 비율은 그대로다.
        /// </summary>
        public static float AxisParam(Vector3 _pointInThigh, Vector3 _kneeInThigh)
        {
            float lengthSq = _kneeInThigh.sqrMagnitude;
            if (lengthSq <= 1e-20f) return 0f;
            return KNEE * Vector3.Dot(_pointInThigh, _kneeInThigh) / lengthSq;
        }
    }
}
