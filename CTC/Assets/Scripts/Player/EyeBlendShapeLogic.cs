using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>셰이프키 가중치 다섯(유니티 규약 0~100).</summary>
    public readonly struct EyeShapeWeights
    {
        public readonly float LookIn;
        public readonly float LookOut;
        public readonly float LookUp;
        public readonly float LookDown;
        public readonly float Blink;

        public EyeShapeWeights(float _in, float _out, float _up, float _down, float _blink)
        {
            LookIn = _in;
            LookOut = _out;
            LookUp = _up;
            LookDown = _down;
            Blink = _blink;
        }

        /// <summary>QA가 값을 **눈으로 읽을 수 있게** — 구조체 기본 ToString은 타입 이름만 준다.</summary>
        public override string ToString()
        {
            return string.Format("in={0:0.##} out={1:0.##} up={2:0.##} down={3:0.##} blink={4:0.##}",
                LookIn, LookOut, LookUp, LookDown, Blink);
        }
    }

    /// <summary>
    /// 시선 방향을 팩 메시의 셰이프키 가중치로 접는다 — 트랜스폼도 시간도 모른다(EditMode 대상).
    /// 방향은 전부 **머리 본 로컬**이다.
    ///
    /// **왜 두 눈을 평균하는가**(2026-09-13 실측): 팩 셰이프키는 좌우 49정점을 **거울 대칭**으로
    /// 민다(lookIn이 왼눈 x −18.5 · 오른눈 x +18.5). 그래서 「왼눈은 앞, 오른눈은 뒤」 같은
    /// 비대칭 시선은 **표현할 방법이 없다.** 한쪽 눈에 맞추면 반대쪽 눈이 틀린 곳을 보므로,
    /// 표현 가능한 대칭 성분만 남기는 평균이 유일하게 옳다. 버려지는 비대칭 성분이 곧
    /// 이 방식이 구 방식에 비해 못 하는 것이다
    /// (설계: docs/superpowers/specs/2026-09-13-eye-blendshape-design.md §2).
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
        /// 얻는 것은 두께 1%p뿐이다. LOD를 갈아 끼우거나 팩이 갱신되면 이 값을 다시 재라
        /// (`scratchpad/blink_overdrive.py`가 그 자다).
        /// </summary>
        public const float BLINK_FULL_WEIGHT = 140f;

        public static EyeShapeWeights Weights(Vector3 _restR, Vector3 _restL,
                                              Vector3 _gazeR, Vector3 _gazeL,
                                              Vector3 _pivot, float _maxAngleDeg, float _cover)
        {
            float max = Mathf.Max(1f, _maxAngleDeg);

            Vector3 fwd = Safe(_pivot, Vector3.forward);
            // 머리의 오른쪽 축 — 두 쉬는 방향이 x에 대해 거울이라 그 차가 곧 오른쪽이다.
            Vector3 right = Safe(_restR - _restL, Vector3.right);
            Vector3 up = Safe(Vector3.Cross(fwd, right), Vector3.up);
            // right가 fwd와 직교하지 않을 수 있다 — up을 얻은 뒤 직교하게 다시 세운다.
            right = Safe(Vector3.Cross(up, fwd), right);

            float h = 0.5f * (InwardDeg(_restR, _gazeR, fwd, right, up)
                            + InwardDeg(_restL, _gazeL, fwd, right, up)) / max;
            float v = 0.5f * (ElevationDeg(_gazeR, up) - ElevationDeg(_restR, up)
                            + ElevationDeg(_gazeL, up) - ElevationDeg(_restL, up)) / max;
            h = Mathf.Clamp(h, -1f, 1f);
            v = Mathf.Clamp(v, -1f, 1f);

            return new EyeShapeWeights(
                Mathf.Max(0f, h) * 100f,
                Mathf.Max(0f, -h) * 100f,
                Mathf.Max(0f, v) * 100f,
                Mathf.Max(0f, -v) * 100f,
                Mathf.Clamp01(_cover) * BLINK_FULL_WEIGHT);
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
