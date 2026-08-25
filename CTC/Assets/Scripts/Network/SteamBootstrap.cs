using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 스팀 초기화(AppID 480 테스트 — 정식 입점 시 교체). 스팀 미실행/미로그인이면
    /// 조용히 로컬 모드 — 기존 UTP 경로는 영향 없음. MPPM 클론은 동일 계정 중복이라
    /// 초기화 실패가 정상이며, 클론 QA는 UTP 로컬 직결로만 돈다.
    /// </summary>
    public class SteamBootstrap : MonoBehaviour
    {
        public const uint APP_ID = 480; // Spacewar

        public static bool Ready { get; private set; }

        private void Awake()
        {
            // runInBackground는 Core.AppBootstrap이 씬 로드 전에 설정 (여기선 중복 설정 안 함)
            DontDestroyOnLoad(gameObject);
            TryInit();
        }

        /// <summary>초기화 재시도 — 게임 실행 후 스팀을 켠 경우 UI '다시 연결' 버튼이 호출.</summary>
        public static bool TryInit()
        {
            if (Ready) return true;

            try
            {
                Steamworks.SteamClient.Init(APP_ID, true);
                Ready = true;

                // 릴레이 예열(밸브 권장: 앱 시작 시). 트랜스포트도 부르지만 그건 StartClient가
                // ConnectRelay를 던진 **다음** 프레임이라, 첫 접속이 식은 릴레이 위에서 10초
                // 기본 타임아웃과 경주하게 된다(실측: 2인 테스트 양방향 모두 핸드셰이크 사망).
                // 여기서 미리 예열하고, 첫 접속 타임아웃도 25초로 늘린다.
                Steamworks.SteamNetworkingUtils.InitRelayNetworkAccess();
                Steamworks.SteamNetworkingUtils.ConnectionTimeout = 25_000;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // 릴레이 핸드셰이크가 "신원 0"으로 죽는 원인 추적(2026-08-24) — 스팀 네트워킹의
                // 내부 진단(인증서·릴레이 회선·타임아웃 사유)을 로그로 흘린다. 개발 빌드 전용.
                Steamworks.SteamNetworkingUtils.OnDebugOutput -= OnSteamNetDebug;
                Steamworks.SteamNetworkingUtils.OnDebugOutput += OnSteamNetDebug;
                Steamworks.SteamNetworkingUtils.DebugLevel = Steamworks.NetDebugOutput.Msg;
#endif

                Debug.Log($"[Steam] init OK: {Steamworks.SteamClient.Name} ({Steamworks.SteamClient.SteamId})");
            }
            catch (System.Exception e)
            {
                Ready = false;
                Debug.LogWarning($"[Steam] init 실패(로컬 모드로 동작): {e.Message}");
            }
            return Ready;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void OnSteamNetDebug(Steamworks.NetDebugOutput _type, string _message)
        {
            Debug.Log($"[SteamNet] {_type}: {_message}");
        }
#endif

        private void OnDestroy()
        {
            if (!Ready) return;
            Steamworks.SteamClient.Shutdown();
            Ready = false;
        }
    }
}
