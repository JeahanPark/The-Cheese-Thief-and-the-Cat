using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Cameras
{
    /// <summary>플레이어 프리팹에 붙어, 로컬 소유자 스폰 시 씬의 궤도 카메라를 자신에게 바인딩한다.</summary>
    public class PlayerCameraBinder : NetworkBehaviour
    {
        [SerializeField] private float m_CameraFov = 60f;
        [SerializeField] private float m_CameraDistance = 4f;

        public override void OnNetworkSpawn()
        {
            // 봇은 서버 소유라 호스트에선 IsOwner가 true다 — 그대로 두면 **카메라가 봇을 따라간다**
            // (2026-09-03 실측: 고양이로 시작했는데 화면엔 봇 쥐가 보였다).
            if (!Player.LocalControl.Mine(this)) return;

            var controller = FindFirstObjectByType<OrbitCameraController>();
            if (controller == null)
            {
                Debug.LogWarning("[PlayerCameraBinder] 씬에 OrbitCameraController가 없다");
                return;
            }
            controller.Bind(transform, m_CameraFov, m_CameraDistance);
        }
    }
}
