using System;
using Unity.Collections;
using Unity.Netcode;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 대기실 참가자 1명. NetworkList 요소 조건: INetworkSerializable + IEquatable.
    ///
    /// 인게임의 <see cref="CTC.Runtime.Lobby.LobbyPlayerState"/>와 **일부러 다른 타입**이다 —
    /// 대기실에는 역할이 없고(고양이는 시작할 때 뽑는다) 대신 보여 줄 이름이 있다.
    /// 한 타입으로 합치면 둘 중 하나는 늘 반쪽을 비워 나른다.
    /// </summary>
    public struct RoomMemberState : INetworkSerializable, IEquatable<RoomMemberState>
    {
        public ulong ClientId;

        /// <summary>화면에 뜨는 이름. 스팀 이름이거나 "플레이어 N".</summary>
        public FixedString32Bytes Name;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Name);
        }

        public bool Equals(RoomMemberState _other)
        {
            return ClientId == _other.ClientId && Name.Equals(_other.Name);
        }
    }
}
