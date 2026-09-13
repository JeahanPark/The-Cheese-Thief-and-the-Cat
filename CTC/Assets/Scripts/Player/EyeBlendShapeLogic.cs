using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 셰이프키 가중치 — 수평은 **눈마다** 넷, 수직은 두 눈 공통 둘, 감김 하나, 질끈 하나(유니티 규약 0~100, 감김만 140까지).
    /// </summary>
    public readonly struct EyeShapeWeights
    {
        public readonly float InR;
        public readonly float OutR;
        public readonly float InL;
        public readonly float OutL;
        public readonly float Up;
        public readonly float Down;
        public readonly float Blink;
        public readonly float Squint;

        public EyeShapeWeights(float _inR, float _outR, float _inL, float _outL,
                               float _up, float _down, float _blink, float _squint = 0f)
        {
            InR = _inR;
            OutR = _outR;
            InL = _inL;
            OutL = _outL;
            Up = _up;
            Down = _down;
            Blink = _blink;
            Squint = _squint;
        }

        /// <summary>QA가 값을 **눈으로 읽을 수 있게** — 구조체 기본 ToString은 타입 이름만 준다.</summary>
        public override string ToString()
        {
            return string.Format(
                "R(in={0:0.##} out={1:0.##}) L(in={2:0.##} out={3:0.##}) up={4:0.##} down={5:0.##} blink={6:0.##} squint={7:0.##}",
                InR, OutR, InL, OutL, Up, Down, Blink, Squint);
        }
    }

    /// <summary>
    /// 시선 방향을 셰이프키 가중치로 접는다 — 트랜스폼도 시간도 모른다(EditMode 대상).
    /// 방향은 전부 **머리 본 로컬**이다.
    ///
    /// **수평은 눈마다 따로 민다**(2026-09-13). 처음엔 팩 키 <c>eyes.lookIn</c>/<c>lookOut</c>이 두 눈을
    /// 거울 대칭으로 묶어 움직여서 **두 눈의 편차를 평균**할 수밖에 없었다 — 그러면 「옆 목표를 쫓는」
    /// 비대칭 시선이 사라진다. 인게임에서 재 보니 조준 yaw +75도에서 **눈마다 49.3도씩** 어긋났고,
    /// 사람의 조준은 카메라 정면이라 이 각도는 흔했다. 그래서 그 키를 좌우로 쪼갠 파생 메시를 굽고
    /// (<c>EyeSplitMeshBaker</c>) 여기서는 눈마다 제 편차를 그대로 넣는다.
    ///
    /// **수직은 여전히 공통이다.** <c>eyes.lookUp</c>/<c>lookDown</c>은 두 눈을 **같은 쪽**으로 움직여
    /// 원래 맞았다(실측 z +44.3 둘 다). 위아래를 눈마다 다르게 보는 일은 드물어 쪼개지 않았다.
    /// </summary>
    public static class EyeBlendShapeLogic
    {
        /// <summary>
        /// 완전히 감았을 때 <c>eyes.blink</c>에 주는 가중치 — **100이 아니라 140이다**(2026-09-13 실측).
        ///
        /// 팩 키를 100%까지만 밀면 동공 두께가 **31%나 남아** 「감았다」가 아니라 「실눈」으로 읽힌다.
        /// 셰이프키는 선형이라 1.0을 넘겨도 외삽되고 유니티도 100 넘는 가중치를 받는다.
        /// 두께를 재 보니:
        ///     1.00 → 31%   1.40 → 9%   1.44 → 8%(최소)   1.48 → **면이 뒤집힌다**
        /// 뒤집힘은 기본 자세에서 위에 있던 정점이 아래로 내려가는 것이고(노멀 반전),
        /// **쥐와 고양이가 같은 지점(≈1.47)에서** 넘어간다.
        ///
        /// 최소가 1.44인데 140을 쓰는 이유는 **여유**다 — 뒤집힘까지 0.07밖에 안 남고,
        /// 얻는 것은 두께 1%p뿐이다. LOD를 갈아 끼우거나 팩이 갱신되면 이 값을 다시 재라.
        /// </summary>
        public const float BLINK_FULL_WEIGHT = 140f;

        /// <summary>
        /// 쏠 때 질끈 감는 눈 — 팩 키 <c>eyes.squint</c>를 쓴다(2026-09-13).
        ///
        /// 「질끈 감는 쐐기(&gt; &lt;)는 팩에 없다」고 적어 뒀었는데 **틀렸다** — 셰이프키 30개를 렌더로
        /// 하나씩 보니 <c>eyes.squint</c>가 정확히 그 모양이었다(앞선 조사는 동공 **두께**만 재서 모양을 못 봤다).
        /// 그래서 사격 감기는 <c>eyes.blink</c> 140이 아니라 이 키로 간다. 평상 깜빡임은 그대로 blink다.
        ///
        /// **질끈 감는 동안 시선·감김 키를 그만큼 뺀다.** 쐐기는 동공 자리에 새로 그린 선이라, lookOut을
        /// 같이 밀면 선이 토막 나고 blink 140을 같이 밀면 덩어리로 뭉개진다(렌더 확인).
        /// 가중치 s(0~1)만큼 쐐기가 들어오고 나머지가 (1-s)로 물러나 — 중간 프레임도 안 깨진다.
        /// </summary>
        public const float SQUINT_FULL_WEIGHT = 100f;

        public static EyeShapeWeights Weights(Vector3 _restR, Vector3 _restL,
                                              Vector3 _gazeR, Vector3 _gazeL,
                                              Vector3 _pivot, float _maxAngleDeg, float _cover,
                                              float _squint = 0f)
        {
            float max = Mathf.Max(1f, _maxAngleDeg);

            Vector3 fwd = Safe(_pivot, Vector3.forward);
            // 머리의 오른쪽 축 — 두 쉬는 방향이 x에 대해 거울이라 그 차가 곧 오른쪽이다.
            Vector3 right = Safe(_restR - _restL, Vector3.right);
            Vector3 up = Safe(Vector3.Cross(fwd, right), Vector3.up);
            // right가 fwd와 직교하지 않을 수 있다 — up을 얻은 뒤 직교하게 다시 세운다.
            right = Safe(Vector3.Cross(up, fwd), right);

            // 수평: 눈마다 제 편차 그대로(+가 안쪽).
            float hR = Mathf.Clamp(InwardDeg(_restR, _gazeR, fwd, right, up) / max, -1f, 1f);
            float hL = Mathf.Clamp(InwardDeg(_restL, _gazeL, fwd, right, up) / max, -1f, 1f);
            // 수직: 두 눈 공통 키라 평균한다.
            float v = 0.5f * (ElevationDeg(_gazeR, up) - ElevationDeg(_restR, up)
                            + ElevationDeg(_gazeL, up) - ElevationDeg(_restL, up)) / max;
            v = Mathf.Clamp(v, -1f, 1f);

            float s = Mathf.Clamp01(_squint);
            float keep = 1f - s;   // 쐐기가 들어오는 만큼 나머지가 물러난다(SQUINT_FULL_WEIGHT 참조)

            return new EyeShapeWeights(
                Mathf.Max(0f, hR) * 100f * keep,
                Mathf.Max(0f, -hR) * 100f * keep,
                Mathf.Max(0f, hL) * 100f * keep,
                Mathf.Max(0f, -hL) * 100f * keep,
                Mathf.Max(0f, v) * 100f * keep,
                Mathf.Max(0f, -v) * 100f * keep,
                Mathf.Clamp01(_cover) * BLINK_FULL_WEIGHT * keep,
                s * SQUINT_FULL_WEIGHT);
        }

        private static Vector3 Safe(Vector3 _v, Vector3 _fallback)
        {
            return _v.sqrMagnitude > 1e-12f ? _v.normalized : _fallback.normalized;
        }

        /// <summary>수평면 기준 올라간 각. 위가 +.</summary>
        private static float ElevationDeg(Vector3 _dir, Vector3 _up)
        {
            Vector3 d = Safe(_dir, Vector3.forward);
            return Mathf.Asin(Mathf.Clamp(Vector3.Dot(d, _up), -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 코 쪽으로 돈 각(+가 안쪽). **어느 쪽 눈인지 인자로 안 받는다** — 쉬는 방향의
        /// 방위각 부호가 그것을 알려 준다. 그래서 좌우를 뒤바꿔 불러도 안 깨진다.
        /// </summary>
        private static float InwardDeg(Vector3 _rest, Vector3 _gaze,
                                       Vector3 _fwd, Vector3 _right, Vector3 _up)
        {
            float restAz = AzimuthDeg(_rest, _fwd, _right, _up);
            float gazeAz = AzimuthDeg(_gaze, _fwd, _right, _up);
            float side = restAz >= 0f ? 1f : -1f;
            return side * (restAz - gazeAz);
        }

        private static float AzimuthDeg(Vector3 _dir, Vector3 _fwd, Vector3 _right, Vector3 _up)
        {
            Vector3 p = _dir - _up * Vector3.Dot(_dir, _up);
            if (p.sqrMagnitude <= 1e-12f) return 0f;
            return Mathf.Atan2(Vector3.Dot(p, _right), Vector3.Dot(p, _fwd)) * Mathf.Rad2Deg;
        }
    }
}
