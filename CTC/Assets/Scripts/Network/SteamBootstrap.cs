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
                Debug.Log($"[Steam] init OK: {Steamworks.SteamClient.Name} ({Steamworks.SteamClient.SteamId})");
            }
            catch (System.Exception e)
            {
                Ready = false;
                Debug.LogWarning($"[Steam] init 실패(로컬 모드로 동작): {e.Message}");
            }
            return Ready;
        }

        private void OnDestroy()
        {
            if (!Ready) return;
            Steamworks.SteamClient.Shutdown();
            Ready = false;
        }
    }
}
