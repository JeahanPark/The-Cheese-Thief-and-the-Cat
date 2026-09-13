namespace CTC.Runtime.Player
{
    /// <summary>
    /// 원샷 동작 → 얼굴 표정 매핑(2026-09-13). 순수 static — EditMode 대상.
    ///
    /// 팩 컨트롤러(AC_Mouse·AC_Cat)의 두 번째 레이어 <c>Shapekey</c>에 표정 상태가 스무 개 넘게 있고
    /// 기본으로 <see cref="DEFAULT"/>(반쯤 감긴 눈)이 돈다. 이 목록이 그중 **무엇을 언제 켜는가**의 원본이다.
    ///
    /// ── 정보 설계(기획서 §11): 「이미 있는 것을 그림으로 바꾸는 것은 되고, 없던 것을 만드는 것은 안 된다」 ──
    ///
    /// **표정은 이미 모두에게 보이는 원샷에만 묶는다.** 원샷 RPC가 전원에게 가고 동작이 이미 몸에
    /// 드러나므로, 같은 시간 동안 얼굴이 따라 바뀌는 것은 새 채널이 아니다. 지속 시간도 **원샷과 같다** —
    /// 더 길게 남기면 「방금 무슨 일이 있었나」가 동작이 끝난 뒤에도 읽혀 없던 정보가 된다.
    ///
    /// 기각한 것(<see cref="REJECTED"/>) — 전부 **보이지 않던 상태**를 얼굴로 흘린다:
    ///   Eyes_Sad·Eyes_Cry·Sweat·Teardrop  체력이 낮다 / 쫓기는 중이다
    ///   Eyes_Sleep                        오래 가만히 있었다(숨은 자리·자리 비움)
    ///   Eyes_Dead                         묶을 사건이 없다(Death 동작을 안 쓴다)
    ///   Eyes_Trauma                       맞을 때 후보였지만 **시선 키와 섞이면 윤곽이 찢어진다**(렌더 확인)
    ///   Eyes_Spin                         어지러움 — 묶을 사건이 없다
    ///   Eyes_Squint                       사격 감기가 <see cref="EyeLook"/>에서 직접 민다(레이어가 덮으면 둘이 싸운다)
    /// </summary>
    public static class FaceExpressionLogic
    {
        /// <summary>표정 상태가 사는 애니메이터 레이어 이름(팩 원본).</summary>
        public const string LAYER_NAME = "Shapekey";

        /// <summary>평상시 얼굴 — 팩 컨트롤러의 기본 상태. **이걸 바꾸면 모든 얼굴이 바뀐다.**</summary>
        public const string DEFAULT = "Eyes_Annoyed";

        public const string SHRINK = "Eyes_Shrink";   // 콩알 동공 — 움찔
        public const string RABID = "Eyes_Rabid";     // 세로 동공 — 사냥
        public const string HAPPY = "Eyes_Happy";     // 초승달 — 맛있다
        public const string EXCITED = "Eyes_Excited"; // 반짝 — 샀다

        /// <summary>이 게임이 켜는 표정 전부(기본 포함). 컨트롤러에 실제로 있는지 EditMode가 본다.</summary>
        public static readonly string[] Used = { DEFAULT, SHRINK, RABID, HAPPY, EXCITED };

        /// <summary>정보를 흘려서 **일부러 안 쓰는** 표정. 여기 있는 이름이 매핑에 나오면 테스트가 깨진다.</summary>
        public static readonly string[] REJECTED =
        {
            "Eyes_Sad", "Eyes_Cry", "Eyes_Sleep", "Eyes_Dead", "Eyes_Trauma", "Eyes_Spin", "Eyes_Squint",
            "Teardrop_L", "Teardrop_R", "Sweat_L", "Sweat_R",
        };

        /// <summary>
        /// 원샷 상태 이름 → 표정 상태 이름. 표정이 없는 원샷이면 <c>null</c>(얼굴은 그대로 둔다).
        ///
        /// 쥐의 액티브 셋(대시·끈끈이·요새)과 사격은 **일부러 비워 둔다** — 사격은 눈을 질끈 감는 것이
        /// 이미 있고, 나머지는 몸 동작이 크게 읽혀 얼굴까지 바꾸면 소란하다.
        /// </summary>
        public static string ForOneShot(string _state)
        {
            switch (_state)
            {
                case AnimationStateLogic.Hit: return SHRINK;       // 맞았다(HitFeedback — 전원)
                case AnimationStateLogic.Attack: return RABID;     // 덮치기·물기(CatPounce·DogBite — 전원)
                case AnimationStateLogic.Frenzy: return RABID;     // 광분 발동(CatFrenzy — 전원)
                case AnimationStateLogic.CatSkillFire:              // 전직 스킬 넷(CatSkill — 전원)
                case AnimationStateLogic.CatSkillWind:
                case AnimationStateLogic.CatSkillLight:
                case AnimationStateLogic.CatSkillDark: return RABID;
                case AnimationStateLogic.Eat: return HAPPY;        // 채굴(CheeseSpot — 각 클라)
                case AnimationStateLogic.Clicked: return EXCITED;  // 구매(ShopController — 전원)
                default: return null;
            }
        }
    }
}
