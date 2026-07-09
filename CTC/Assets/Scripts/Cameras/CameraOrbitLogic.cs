using UnityEngine;

namespace CTC.Runtime.Cameras
{
    /// <summary>
    /// 궤도 카메라 각도 계산(클램프·랩·마우스 델타 적용).
    /// MonoBehaviour와 분리된 순수 함수라 EditMode 테스트가 가능하다.
    /// yaw 규약: Cinemachine OrbitalFollow HorizontalAxis와 동일한 [-180, 180).
    /// </summary>
    public static class CameraOrbitLogic
    {
        public static float ClampPitch(float pitchDeg, float minDeg, float maxDeg)
        {
            return Mathf.Clamp(pitchDeg, minDeg, maxDeg);
        }

        public static float WrapYaw(float yawDeg)
        {
            return Mathf.Repeat(yawDeg + 180f, 360f) - 180f;
        }

        /// <summary>current=(yaw,pitch)에 마우스 델타를 적용한 다음 각도를 반환. 비반전(마우스 위=시선 위).</summary>
        public static Vector2 Step(Vector2 current, Vector2 mouseDelta, float sensitivity, float minPitch, float maxPitch)
        {
            float yaw = WrapYaw(current.x + mouseDelta.x * sensitivity);
            float pitch = ClampPitch(current.y - mouseDelta.y * sensitivity, minPitch, maxPitch);
            return new Vector2(yaw, pitch);
        }
    }
}
