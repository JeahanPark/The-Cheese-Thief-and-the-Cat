using Unity.Netcode;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// "이 몸을 내가 조종하는가"의 단독 판정 (2026-09-01).
    ///
    /// `IsOwner`만 보면 **봇이 내 입력을 같이 먹는다.** 봇은 붙일 클라이언트가 없어 서버가
    /// 소유하는데, 호스트에서는 서버가 곧 로컬이라 `IsOwner`가 true다(실측: 대기실에서 봇을
    /// 채우고 시작하면 내 WASD로 봇이 움직이고 카메라도 봇에게 갔다).
    ///
    /// 사람의 몸만 `SpawnAsPlayerObject`로 서므로 <c>IsPlayerObject</c>가 사람과 봇을 가른다.
    /// </summary>
    public static class LocalControl
    {
        public static bool Mine(NetworkBehaviour _behaviour)
        {
            return _behaviour != null
                   && _behaviour.IsOwner
                   && _behaviour.NetworkObject != null
                   && _behaviour.NetworkObject.IsPlayerObject;
        }
    }
}
