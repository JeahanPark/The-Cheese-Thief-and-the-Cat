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

        /// <summary>
        /// 스팀을 실제로 들고 있는 인스턴스. **둘째부터는 아무것도 하지 않는다**(2026-08-30).
        ///
        /// 대기실에서 인게임으로 넘어오면 `Game.unity`에 박힌 NetworkManager 오브젝트가
        /// 중복이 되어 <see cref="NetworkRootGuard"/>가 지운다. 그런데 파괴는 프레임 끝이라
        /// 거기 동승한 이 컴포넌트의 `Awake`가 먼저 한 번 돌고, 이어서 `OnDestroy`가
        /// **살아 있는 쪽의 스팀까지** 내려 버린다(실측: init 경고가 두 번 찍혔다).
        /// </summary>
        private static SteamBootstrap s_Owner;

        private void Awake()
        {
            if (s_Owner != null && s_Owner != this) return;

            s_Owner = this;
            // runInBackground는 Core.AppBootstrap이 씬 로드 전에 설정 (여기선 중복 설정 안 함)
            DontDestroyOnLoad(gameObject);
            TryInit();
        }

        /// <summary>도메인 리로드를 끄고 플레이하면 옛 인스턴스 참조가 남는다 — 진입마다 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Owner = null;
            Ready = false;
        }

        /// <summary>초기화 재시도 — 게임 실행 후 스팀을 켠 경우 UI '다시 연결' 버튼이 호출.</summary>
        public static bool TryInit()
        {
            if (Ready) return true;

            try
            {
#if !UNITY_EDITOR
                // **스팀을 거쳐 다시 켠다**(2026-08-30). exe를 직접 실행하면 스팀이 우리를 게임으로
                // 인식하지 못해 오버레이·초대·플레이시간이 전부 죽는다. true를 돌려주면 스팀이
                // 새 프로세스를 띄운 것이므로 이쪽은 조용히 끝내야 한다.
                //
                // ⚠ **exe 옆에 `steam_appid.txt`가 있으면 이 함수는 언제나 false다.** 그 파일은
                // 개발용 우회로라 뎁포에서 반드시 빼야 한다(`app_build.vdf`의 FileExclusion).
                if (Steamworks.SteamClient.RestartAppIfNecessary(APP_ID))
                {
                    Debug.Log("[Steam] 스팀을 거쳐 다시 켠다 — 이 프로세스는 종료");
                    Application.Quit();
                    return false;
                }
#endif
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
            if (s_Owner != this) return;   // 남의 스팀을 끄지 않는다

            s_Owner = null;
            if (!Ready) return;
            Steamworks.SteamClient.Shutdown();
            Ready = false;
        }
    }
}
