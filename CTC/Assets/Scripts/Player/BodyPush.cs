using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 서버가 플레이어 몸을 밀어내는 유일한 통로 (2026-08-15, 요새 즉발 설치용).
    ///
    /// 이동은 소유자 권위(ClientNetworkTransform)라 서버가 남의 transform을 옮겨도 다음 틱에
    /// 소유자 값으로 덮인다. 그래서 서버는 "얼마나 밀어라"만 소유자에게 보내고 소유자가 옮긴다.
    /// 봇은 서버가 소유자라 같은 코드가 그 자리에서 돈다.
    ///
    /// **`CC.Move`가 아니라 텔레포트다**(qa_v36 실측 2026-08-15): 요새가 몸을 감싸며 서기 때문에
    /// 밀려날 방향에 방금 생긴 벽이 있고, 스윕 이동은 그 벽에 막혀 제자리다. 벽을 통과해 밖으로
    /// 내보내야 하므로 CC를 잠깐 끄고 위치를 옮긴다 — 텔레포트 헬퍼(TestManager·QaProbe)와 같은 꼴.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class BodyPush : NetworkBehaviour
    {
        private CharacterController m_Controller;

        private void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
        }

        /// <summary>서버 전용 — 소유자에게 밀림을 전달한다.</summary>
        public void ServerPush(Vector3 _delta)
        {
            if (!IsServer || _delta.sqrMagnitude < 1e-6f) return;
            PushRpc(_delta);
        }

        [Rpc(SendTo.Owner)]
        private void PushRpc(Vector3 _delta)
        {
            if (m_Controller == null) return;
            bool wasEnabled = m_Controller.enabled;
            m_Controller.enabled = false;
            transform.position += _delta;
            m_Controller.enabled = wasEnabled;
        }
    }
}
