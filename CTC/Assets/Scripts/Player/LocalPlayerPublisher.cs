using CTC.Runtime.Core;
using Unity.Netcode;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 자기가 로컬 플레이어면 스폰/디스폰 시 <see cref="LocalPlayerBinder"/>에 알린다.
    /// 플레이어 프리팹에 붙는 단일 책임 컴포넌트 (개편 3b, 2026-07-31).
    /// </summary>
    public class LocalPlayerPublisher : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (LocalControl.Mine(this)) LocalPlayerBinder.Publish(NetworkObject);
        }

        public override void OnNetworkDespawn()
        {
            if (LocalControl.Mine(this)) LocalPlayerBinder.Withdraw(NetworkObject);
        }
    }
}
