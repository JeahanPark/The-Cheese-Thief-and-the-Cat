using System;
using Unity.Netcode;

namespace CTC.Runtime.Lobby
{
    /// <summary>로비 참가자 1명의 상태. NetworkList 요소 조건: INetworkSerializable + IEquatable.</summary>
    public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
    {
        public ulong ClientId;
        public RoleId Role;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Role);
        }

        public bool Equals(LobbyPlayerState _other)
        {
            return ClientId == _other.ClientId && Role == _other.Role;
        }
    }
}
