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
            if (!IsOwner) return;

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
