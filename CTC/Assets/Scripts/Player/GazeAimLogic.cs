using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 조준 방향의 **양자화·포장** — 트랜스폼도 네트워크도 모른다(EditMode 테스트 대상).
    ///
    /// 방향 하나를 `short` **2바이트**에 담는다. 눈은 연출이라 정밀도보다 대역폭이 중요하고,
    /// 눈이 실제로 쓰는 각도는 <c>EyeMaxAngleDeg</c>(30°)뿐이라 1° 남짓의 오차는 안 보인다.
    ///
    /// - **yaw** [-180,180) → 상위 바이트. 256칸이라 한 칸 1.40625°, 왕복 오차 ≤ 0.71°.
    ///   360°를 한 바퀴로 감아 **±180° 경계에서 안 튄다**(179.9°와 -179.9°가 이웃 칸이다).
    /// - **pitch** [-90,90] → 하위 바이트. 127칸이라 한 칸 0.7087°, 왕복 오차 ≤ 0.36°.
    ///   위아래는 감기지 않으므로 clamp한다(카메라가 수직을 넘지 않는다).
    ///
    /// 기준 프레임은 **몸 로컬**이다(머리 본이 아니다) — 이유는 <see cref="GazeAim"/> 주석 참조.
    /// </summary>
    public static class GazeAimLogic
    {
        /// <summary>yaw 한 칸의 각도(도).</summary>
        public const float YAW_STEP_DEG = 360f / 256f;

        /// <summary>pitch 한 칸의 각도(도).</summary>
        public const float PITCH_STEP_DEG = 90f / 127f;

        /// <summary>정면(yaw 0 · pitch 0)의 포장값. NetworkVariable 기본값이 이것이다.</summary>
        public const short FORWARD = 0;

        /// <summary>로컬 방향 → 2바이트. 영벡터는 정면으로 본다.</summary>
        public static short Pack(Vector3 _localDir)
        {
            return PackAngles(YawOf(_localDir), PitchOf(_localDir));
        }

        /// <summary>2바이트 → 단위 로컬 방향.</summary>
        public static Vector3 Unpack(short _packed)
        {
            UnpackAngles(_packed, out float yaw, out float pitch);
            return Direction(yaw, pitch);
        }

        /// <summary>각도 → 2바이트. yaw는 감고 pitch는 자른다.</summary>
        public static short PackAngles(float _yawDeg, float _pitchDeg)
        {
            int yaw = QuantizeYaw(_yawDeg);
            int pitch = QuantizePitch(_pitchDeg);
            return unchecked((short)(((yaw & 0xFF) << 8) | (pitch & 0xFF)));
        }

        /// <summary>2바이트 → 각도.</summary>
        public static void UnpackAngles(short _packed, out float _yawDeg, out float _pitchDeg)
        {
            int yaw = unchecked((sbyte)((_packed >> 8) & 0xFF));
            int pitch = unchecked((sbyte)(_packed & 0xFF));
            _yawDeg = yaw * YAW_STEP_DEG;
            _pitchDeg = pitch * PITCH_STEP_DEG;
        }

        /// <summary>yaw·pitch(도) → 단위 방향. +z가 정면, +y가 위.</summary>
        public static Vector3 Direction(float _yawDeg, float _pitchDeg)
        {
            float yaw = _yawDeg * Mathf.Deg2Rad;
            float pitch = _pitchDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(pitch);
            return new Vector3(cos * Mathf.Sin(yaw), Mathf.Sin(pitch), cos * Mathf.Cos(yaw));
        }

        /// <summary>방향의 yaw(도) — 정면이 0, 오른쪽이 양수. 영벡터는 0.</summary>
        public static float YawOf(Vector3 _localDir)
        {
            if (_localDir.sqrMagnitude <= 1e-12f) return 0f;
            return Mathf.Atan2(_localDir.x, _localDir.z) * Mathf.Rad2Deg;
        }

        /// <summary>방향의 pitch(도) — 위가 양수. 영벡터는 0.</summary>
        public static float PitchOf(Vector3 _localDir)
        {
            float len = _localDir.magnitude;
            if (len <= 1e-6f) return 0f;
            return Mathf.Asin(Mathf.Clamp(_localDir.y / len, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>[-128,127] 칸. **감는다** — 180°와 -180°가 같은 칸이라 경계에서 안 튄다.</summary>
        public static int QuantizeYaw(float _yawDeg)
        {
            if (float.IsNaN(_yawDeg) || float.IsInfinity(_yawDeg)) return 0;
            int q = Mathf.RoundToInt(_yawDeg / YAW_STEP_DEG);
            return ((q % 256) + 256 + 128) % 256 - 128;
        }

        /// <summary>[-127,127] 칸. **자른다** — 수직을 넘어 뒤집히면 눈이 뒤통수를 본다.</summary>
        public static int QuantizePitch(float _pitchDeg)
        {
            if (float.IsNaN(_pitchDeg)) return 0;
            float clamped = Mathf.Clamp(_pitchDeg, -90f, 90f);
            return Mathf.Clamp(Mathf.RoundToInt(clamped / PITCH_STEP_DEG), -127, 127);
        }
    }
}
