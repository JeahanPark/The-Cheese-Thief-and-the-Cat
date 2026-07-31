using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 원시 입력(수평/수직 축)을 평면(XZ) 이동 방향으로 변환한다.
    /// 대각선 입력이 1을 넘으면 단위 길이로 정규화해 대각 가속을 막는다.
    /// MonoBehaviour와 분리된 순수 함수라 EditMode 테스트가 가능하다.
    /// </summary>
    public static class MovementInput
    {
        public static Vector3 ToDirection(float _horizontal, float _vertical)
        {
            var v = new Vector3(_horizontal, 0f, _vertical);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        /// <summary>카메라 yaw(도) 기준으로 회전된 월드 이동 방향을 반환한다.</summary>
        public static Vector3 ToWorldDirection(float _horizontal, float _vertical, float _cameraYawDegrees)
        {
            return Quaternion.Euler(0f, _cameraYawDegrees, 0f) * ToDirection(_horizontal, _vertical);
        }
    }
}
