using Unity.Netcode.Components;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 소유자 권위 NetworkTransform. 소유 클라이언트가 위치를 기록하고
    /// 서버는 이를 다른 클라이언트로 중계한다. (NGO 공식 샘플과 동일 패턴)
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
