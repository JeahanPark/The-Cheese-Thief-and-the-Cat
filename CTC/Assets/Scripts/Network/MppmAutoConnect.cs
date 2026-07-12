using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 개발용: MPPM 가상 플레이어(클론 에디터)에서 자동으로 클라이언트 접속을 시도한다.
    /// 클론은 Library/VP/ 하위 복제 프로젝트에서 실행되는 점으로 판별.
    /// 메인 에디터/빌드에서는 아무것도 하지 않는다 (ConnectionUI 수동 조작).
    /// </summary>
    public class MppmAutoConnect : MonoBehaviour
    {
        private const float RetryInterval = 2f;

        private IEnumerator Start()
        {
            if (!Application.isEditor) yield break;

            string dataPath = Application.dataPath.Replace('\\', '/');
            bool isClone = dataPath.Contains("/Library/VP/");
            if (!isClone) yield break;

            // 호스트(메인 에디터)가 먼저 떠 있지 않을 수 있어 접속될 때까지 재시도
            while (true)
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && !nm.IsClient && !nm.IsServer)
                {
                    // 호스트가 포트 폴백으로 다른 포트를 쓸 수 있어 공유 파일에서 읽는다
                    HostPortFallback.ApplyHostPortToTransport(nm);
                    nm.StartClient();
                }
                yield return new WaitForSeconds(RetryInterval);
                nm = NetworkManager.Singleton;
                if (nm != null && nm.IsConnectedClient) yield break;
            }
        }
    }
}
