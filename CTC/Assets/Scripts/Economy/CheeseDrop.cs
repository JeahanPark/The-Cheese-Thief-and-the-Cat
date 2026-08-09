using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 바닥에 떨어진 치즈 — 쥐가 **닿기만 하면** 먹고 사라진다 (v3.8, 2026-08-09).
    ///
    /// 벽감을 부수면 여러 덩이가 흩어져 나온다. 채굴(치즈 지점)이 "먼저 온 놈이 눌러앉는"
    /// 경쟁이라면 이쪽은 **달리기 경쟁**이다 — 부순 쥐가 반드시 독식하지는 못한다.
    ///
    /// 고양이는 먹지 못한다. 고양이의 `CheeseWallet`은 장난기 지갑이라 여기에 치즈를 넣으면
    /// "벽 부수기 = 고양이 수입"이 되어 벽감이 장난기를 안 주기로 한 결정과 정면으로 모순된다.
    ///
    /// 판정은 서버만 한다. 쥐 둘이 같은 프레임에 닿아도 서버가 먼저 처리한 쪽에 주고 즉시
    /// despawn하므로 중복 지급이 없다 — 클라가 "내가 먹었다"고 주장하는 경로를 아예 두지 않았다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class CheeseDrop : NetworkBehaviour
    {
        [SerializeField] private int m_Amount = 50;

        private bool m_Taken;

        /// <summary>서버 전용 — 스폰 직전에 벽감이 덩이 크기를 새긴다.</summary>
        public void ServerSetAmount(int _amount)
        {
            m_Amount = Mathf.Max(0, _amount);
        }

        /// <summary>QA·표시용 즉시 값.</summary>
        public int Amount => m_Amount;

        private void OnTriggerEnter(Collider _other)
        {
            if (!IsServer || m_Taken) return;

            NetworkObject no = _other.GetComponentInParent<NetworkObject>();
            if (no == null || !no.IsPlayerObject) return;

            RoleTag tag = no.GetComponent<RoleTag>();
            if (tag == null || tag.Role != RoleId.Mouse) return;

            CheeseWallet wallet = no.GetComponent<CheeseWallet>();
            if (wallet == null) return;

            // 같은 프레임에 둘이 닿아도 두 번 주지 않는다 — despawn은 다음 틱이라 플래그가 필요하다
            m_Taken = true;
            wallet.AddCheese(m_Amount);
            PickupSfxRpc(transform.position);
            NetworkObject.Despawn(true);
        }

        // 획득음은 채굴과 같은 소리를 쓴다 — 뜻이 같으면(치즈를 얻었다) 소리도 같아야 배울 게 없다
        [Rpc(SendTo.Everyone)]
        private void PickupSfxRpc(Vector3 _position)
        {
            Audio.SfxPlayer.PlayAt(Audio.SfxId.CheeseTick, _position);
        }
    }
}
