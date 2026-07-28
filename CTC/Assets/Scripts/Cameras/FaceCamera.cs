using UnityEngine;

namespace CTC.Runtime.Cameras
{
    /// <summary>
    /// 월드 라벨이 항상 카메라를 향하게 한다(빌보드). 치즈 지점의 "등급 · 잔량" 표시가
    /// 어느 방향에서 봐도 읽혀야 자리를 고를 정보가 된다.
    /// </summary>
    public class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // 카메라의 시선 방향을 그대로 쓰면 라벨이 기울어진다 — 수평 성분만 사용
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return;

            transform.rotation = Quaternion.LookRotation(forward);
        }
    }
}
