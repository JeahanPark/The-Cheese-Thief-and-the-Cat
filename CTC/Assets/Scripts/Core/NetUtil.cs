using Unity.Netcode;

namespace CTC.Runtime.Core
{
    /// <summary>넷코드 조회 보조.</summary>
    public static class NetUtil
    {
        /// <summary>
        /// 클라이언트 ID로 플레이어 오브젝트를 찾는다(못 찾으면 null).
        ///
        /// NGO의 SpawnManager.GetPlayerNetworkObject는 **서버에서만** 남의 플레이어를 돌려준다.
        /// 클라이언트에서 남을 조회하면 조용히 null이 나와 "복제가 안 온다"로 오진하기 쉽다
        /// (2026-07-29 따봉 마커가 안 뜨던 원인). 스폰 목록은 클라에도 있으므로 그쪽을 훑는다.
        /// </summary>
        public static NetworkObject FindPlayerObject(ulong _clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;

            foreach (var kv in nm.SpawnManager.SpawnedObjects)
            {
                NetworkObject obj = kv.Value;
                if (obj != null && obj.IsPlayerObject && obj.OwnerClientId == _clientId) return obj;
            }
            return null;
        }
    }
}
