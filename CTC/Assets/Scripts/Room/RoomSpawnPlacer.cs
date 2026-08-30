using CTC.Runtime.Core;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 대기실 몸이 **자기 자리로 스스로 간다**(소유 클라이언트에서만).
    ///
    /// 왜 서버가 놓은 자리로 안 되는가: `ClientNetworkTransform`은 소유자 권위라
    /// 소유 클라이언트의 로컬 초기 위치가 서버의 스폰 위치를 덮는다. 인게임에서
    /// `PlayerMovement`가 같은 이유로 같은 일을 한다 — 여기서도 공식이 서버와 같아야 한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class RoomSpawnPlacer : NetworkBehaviour
    {
        [SerializeField] private float m_Radius = 5f;

        private CharacterController m_Controller;

        private void Awake() => m_Controller = GetComponent<CharacterController>();

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            var session = FindFirstObjectByType<RoomSession>();
            if (session == null)
            {
                GameDebug.LogWarning("[Room] RoomSession이 씬에 없다 — 자리 계산 생략");
                return;
            }

            int slot = session.IndexOf(OwnerClientId);
            if (slot < 0) return;

            // CC가 켜져 있으면 transform 직접 대입이 씹힌다(프로젝트 규약) — 잠깐 끄고 옮긴다.
            m_Controller.enabled = false;
            transform.position = RoomLogic.StandPosition(slot, RoomLogic.MAX_PLAYERS, m_Radius);
            transform.rotation = Quaternion.Euler(0f, RoomLogic.StandYaw(slot, RoomLogic.MAX_PLAYERS), 0f);
            m_Controller.enabled = true;
        }
    }
}
