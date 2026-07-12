// WES에서 이식(2026-07-12)
using CTC.Runtime.Managers;

namespace CTC.Runtime.Core
{
    public class Managers : MonoSingleton<Managers>
    {
        // 이번 이식(WES ①)에서 배선되는 매니저
        public static ResourceManager Resource => ResourceManager.Instance;
        public static AudioManager Audio => AudioManager.Instance;
        public static GameSceneManager Scene => GameSceneManager.Instance;
        public static ChatManager Chat => ChatManager.Instance;

        // 후속 이식 시 배선 예정 (컴파일 불가 타입 참조 금지 — 자리만 표시)
        // public static InfoManager Info => InfoManager.Instance;
        // public static PopupManager Popup => PopupManager.Instance;
        // public static InputManager Input => InputManager.Instance;
        // public static GameNetworkManager Network => GameNetworkManager.Instance;

        public override void Init()
        {
            base.Init();
            InitializeManagers();
        }

        private void InitializeManagers()
        {
            // 각 매니저 명시적 초기화
            Resource.Init();
            Audio.Init();
            Scene.Init();
            // Chat: NetworkBehaviour 전환(자가 생성 불가) — 씬/프리팹 스폰 시 Awake에서 자체 초기화

            // 후속 이식 시 초기화 예정
            // Info.Init();
            // Popup.Init();
            // Input.Init();
            // Network.Init();
        }

        public override void Clear()
        {
            base.Clear();
            ClearManagers();
        }

        private void ClearManagers()
        {
            // 각 매니저 정리 (Singleton 계열은 ?. / MonoSingleton 계열은 Unity null 체크)
            Resource?.Clear();
            if (Audio) Audio.Clear();
            if (Scene) Scene.Clear();
            // Chat: NetworkBehaviour 전환 — OnDestroy에서 자체 정리(Clear 없음)

            // 후속 이식 시 정리 예정
            // Info?.Clear();
            // if (Popup) Popup.Clear();
            // if (Input) Input.Clear();
            // if (Network) Network.Clear();
        }
    }
}
