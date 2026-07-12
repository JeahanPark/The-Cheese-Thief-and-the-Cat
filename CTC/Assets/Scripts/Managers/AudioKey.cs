// WES에서 이식(2026-07-12) — WES 게임 전용 키(sfx_hit/ambient_rain/stinger_boss_appear 등) 전부 제거,
// CTC용 최소 placeholder 세트로 교체. 키 추가 시 이 파일에만 상수를 늘리면 된다.
namespace CTC.Runtime.Managers
{
    /// <summary>
    /// 사운드 트리거 키 단일소스. 호출부는 이 상수를 쓰고, Addressable에 같은 키(Address = 키 문자열
    /// 그대로)로 AudioClip을 등록하면 소리가 난다. 키 미등록 = 무음(AudioManager null 가드).
    /// </summary>
    public static class AudioKey
    {
        // ── BGM (루프) ──
        public const string BGM_LOBBY = "bgm_lobby";
        public const string BGM_GAME = "bgm_game";

        // ── SFX (원샷) ──
        public const string SFX_CLICK = "sfx_click";
    }
}
