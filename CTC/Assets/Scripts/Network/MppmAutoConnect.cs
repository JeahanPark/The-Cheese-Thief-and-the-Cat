using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 개발용: MPPM 가상 플레이어(클론 에디터)에서 자동으로 클라이언트 접속을 시도한다.
    /// 클론은 Library/VP/ 하위 복제 프로젝트에서 실행되는 점으로 판별.
    /// 메인 에디터/빌드에서는 아무것도 하지 않는다 (ConnectionUI 수동 조작).
    ///
    /// 비동기는 UniTask(CTC 규칙 1). `destroyCancellationToken`을 물려 플레이 종료 시
    /// 대기가 그대로 끊긴다 — 코루틴 시절엔 종료 후에도 재시도가 한 박자 더 돌았다.
    /// </summary>
    public class MppmAutoConnect : MonoBehaviour
    {
        private const float RETRY_INTERVAL_SECONDS = 2f;
        private const float ROLE_WAIT_TIMEOUT_SECONDS = 10f;
        private const float ROLE_POLL_SECONDS = 0.5f;

        private void Start()
        {
            if (!Application.isEditor) return;

            string dataPath = Application.dataPath.Replace('\\', '/');
            if (!dataPath.Contains("/Library/VP/")) return;

            RunAsync(destroyCancellationToken).Forget();
        }

        private static async UniTaskVoid RunAsync(CancellationToken _token)
        {
            await ConnectAsync(_token);

            // 클론은 항상 쥐 — 3인 로컬 테스트(메인=고양이, 클론2=쥐)에서 역할 클릭을 없앤다.
            await SelectMouseAsync(_token);
        }

        // 호스트(메인 에디터)가 먼저 떠 있지 않을 수 있어 접속될 때까지 재시도
        private static async UniTask ConnectAsync(CancellationToken _token)
        {
            while (!_token.IsCancellationRequested)
            {
                NetworkManager nm = NetworkManager.Singleton;
                if (nm != null && !nm.IsClient && !nm.IsServer)
                {
                    // 호스트가 포트 폴백으로 다른 포트를 쓸 수 있어 공유 파일에서 읽는다
                    HostPortFallback.ApplyHostPortToTransport(nm);
                    nm.StartClient();
                }

                await UniTask.Delay(TimeSpan.FromSeconds(RETRY_INTERVAL_SECONDS), cancellationToken: _token);

                nm = NetworkManager.Singleton;
                if (nm != null && nm.IsConnectedClient) return;
            }
        }

        private static async UniTask SelectMouseAsync(CancellationToken _token)
        {
            for (float waited = 0f; waited < ROLE_WAIT_TIMEOUT_SECONDS; waited += ROLE_POLL_SECONDS)
            {
                Lobby.LobbyController lobby = FindFirstObjectByType<Lobby.LobbyController>();
                if (lobby != null && lobby.IsSpawned)
                {
                    // 대기실에서 온 판이면 역할이 이미 서버 추첨으로 정해져 있다(2026-08-30).
                    // 그걸 모르고 "쥐"를 눌러 버리면 **뽑힌 고양이가 스스로 쥐로 바뀐다.**
                    NetworkManager net = NetworkManager.Singleton;
                    int idx = net != null ? lobby.IndexOfClient(net.LocalClientId) : -1;
                    if (idx >= 0 && lobby.GetPlayer(idx).Role != Lobby.RoleId.None)
                    {
                        Debug.Log("[MppmAutoConnect] 역할이 이미 배정돼 있다 — 손대지 않는다");
                        return;
                    }

                    lobby.SelectRole(Lobby.RoleId.Mouse);
                    Debug.Log("[MppmAutoConnect] 역할 자동 선택 → 쥐");
                    return;
                }
                await UniTask.Delay(TimeSpan.FromSeconds(ROLE_POLL_SECONDS), cancellationToken: _token);
            }
            Debug.LogWarning("[MppmAutoConnect] LobbyController 스폰 대기 초과 — 역할 수동 선택 필요");
        }
    }
}
