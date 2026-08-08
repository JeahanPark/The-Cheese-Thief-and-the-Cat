using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 쥐는 미끼를 통과하고 고양이는 부딪힌다(2026-07-29 요청) — 레이어 충돌 매트릭스로 구현.
    ///
    /// 앞서 두 방법이 실패했다. 둘 다 **CharacterController에는 적용되지 않는다**:
    /// - Physics.IgnoreCollision(미끼, 쥐 CC) → 쥐도 고양이도 미끼 앞 0.7m에서 정지
    /// - CC.excludeLayers |= 미끼 레이어  → 동일하게 정지
    /// CC 이동은 PhysX 캐릭터 컨트롤러의 자체 스윕이라 콜라이더 단위 필터를 보지 않고,
    /// **레이어 충돌 매트릭스만** 존중한다. 그래서 쥐를 전용 레이어로 옮기고 매트릭스에서 끊는다.
    ///
    /// 레이어는 씬 생성기(SetupNetworkingScene)가 만들고 프리팹에 칠한다.
    /// 매트릭스도 이제 생성기가 **프로젝트 세팅에 구워 둔다**(2026-08-01) — Project Settings의
    /// 물리 격자를 열었을 때 진실이 보이게. 그래도 이 런타임 적용은 남긴다:
    /// 세팅을 리셋했거나 생성기를 안 돌린 사본에서도 스스로 복구한다(멱등이라 중복 비용 0).
    /// </summary>
    public static class DecoyLayers
    {
        public const string DECOY = "Decoy";
        public const string MOUSE = "Mouse";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            int decoy = LayerMask.NameToLayer(DECOY);
            int mouse = LayerMask.NameToLayer(MOUSE);
            if (decoy < 0 || mouse < 0)
            {
                Debug.LogWarning($"[DecoyLayers] 레이어 미생성({DECOY}={decoy}, {MOUSE}={mouse}) — Setup Networking Scene 재실행 필요");
                return;
            }

            Physics.IgnoreLayerCollision(mouse, decoy, true);
        }
    }
}
