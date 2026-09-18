namespace CTC.Runtime.Player
{
    /// <summary>
    /// 수평 이동 속도를 Quirky Series 애니메이터 상태 이름으로 매핑한다.
    /// AC_Cat/AC_Mouse는 파라미터 없이 상태만 있어 CrossFade(상태명)로 구동한다.
    /// </summary>
    public static class AnimationStateLogic
    {
        public const string Idle = "Idle_A";
        public const string Walk = "Walk";
        public const string Run = "Run";
        public const string Attack = "Attack"; // AC_Cat·AC_Mouse 공통 원샷 상태
        public const string Hit = "Hit";       // AC_Cat·AC_Mouse 공통 원샷 상태

        // 테크 v3 계획 2b Task 5 — **새 클립은 하나도 만들지 않았다.**
        // Quirky 팩(AC_Cat·AC_Mouse)이 이미 가지고 있으면서 안 쓰던 상태 여섯을 동작에 붙인 것뿐이다.
        // 이름이 틀리면 CrossFadeInFixedTime은 **에러 없이 조용히 아무 일도 안 한다** —
        // 그래서 문자열을 호출부에 흘리지 않고 여기 모아 EditMode가 철자를 묶어 둔다.
        public const string Roll = "Roll";       // 팩 원본 앞구르기 — **대시에서는 더 이상 안 쓴다**(Dash 참조)
        public const string Bounce = "Bounce";   // 팩 원본 반복 뜀 — **끈끈이에서는 더 이상 안 쓴다**(Sticky 참조)
        public const string Jump = "Jump";       // 팩 원본 도약 — **요새에서는 더 이상 안 쓴다**(Fortress 참조)
        public const string Spin = "Spin";       // 팩 원본 발레 회전 — **광분에서는 더 이상 안 쓴다**(Frenzy 참조)
        public const string Clicked = "Clicked"; // 구매 — 짧은 끄덕임
        public const string Eat = "Eat";         // 채굴 — 갉아먹기(반복)

        // 쥐 사격(2026-09-12) — **AC_Mouse에만** 있다. 고양이는 총을 쏘지 않는다.
        // 블렌더 왕복 FBX(CTC_Mouse_Clips.fbx)에서 새로 만든 클립 둘이다.
        // 둘로 나뉜 이유는 MouseGunVisualLogic에 적어 뒀다 — 기관총이 5배 빨라서
        // 뽑는 동작을 매 발에 넣을 수 없다.
        public const string Shoot = "Shoot";         // 반동만 — 총은 이미 손에 있다
        public const string ShootDraw = "ShootDraw"; // 허리춤에서 뽑아 첫 발

        /// <summary>
        /// 쥐 대시(2026-09-13) — **AC_Mouse에만** 있다. 고양이는 대시를 사지 않는다.
        ///
        /// 예전엔 팩의 <see cref="Roll"/>을 빌려 썼는데 그건 **앞구르기 공중제비**다
        /// (렌더로 확인 — 3~8프레임에서 몸이 완전히 뒤집힌다). 0.2초짜리 직선 돌진에
        /// 붙으면 「회피 구르기」로 읽히고, 루트가 통째로 도는 바람에 CharacterController가
        /// 만드는 실제 전진과 따로 논다. 그래서 블렌더로 새로 만들었다
        /// (원본: <c>tools/blender/clips/dash.py</c>).
        /// </summary>
        public const string Dash = "Dash";

        /// <summary>
        /// 쥐 끈끈이·요새(2026-09-13) — **AC_Mouse에만** 있다. 둘 다 쥐 테크다.
        ///
        /// 예전엔 팩의 <see cref="Bounce"/>·<see cref="Jump"/>을 빌렸는데 둘 다 **루트째 뜬다** —
        /// Bounce는 공중 363mm에서 시작하는 반복 뜀, Jump는 395mm 도약이라 CharacterController는
        /// 바닥인데 몸만 허공에 떴다. 새 클립은 루트를 안 건드린다
        /// (원본: <c>tools/blender/clips/sticky.py</c> · <c>fortress.py</c>).
        /// </summary>
        public const string Sticky = "Sticky";     // 앞발로 버티고 뒷발로 뒤로 걷어찬다
        public const string Fortress = "Fortress"; // 일어서 앞발을 양옆으로 벌렸다가 네 발로 쾅

        /// <summary>
        /// 쥐 창고 설치(2026-09-13) — **AC_Mouse에만** 있다. 예전엔 설치할 때 몸이 아무것도 안 했다.
        /// 앞발로 앞쪽 바닥을 번갈아 토닥인다. 창고·숨은 창고가 **같은 클립**이다(종류를 몸으로 흘리지 않는다).
        /// 원본: <c>tools/blender/clips/warehouse.py</c>.
        /// </summary>
        public const string Warehouse = "Warehouse";

        /// <summary>
        /// 깡패 쥐(2026-09-14) — **AC_Mouse에만** 있다. 2페이즈에서 깡패 편이 되면 두 발로 서서 걷고 몽둥이로 친다
        /// (설계 docs/superpowers/specs/2026-09-14-gang-mouse-look-design.md). 원본: <c>tools/blender/clips/biped_*.py</c>·<c>club_swing.py</c>.
        /// 이동 셋은 <see cref="FromSpeed"/>가 두 발 모드일 때 고른다.
        /// </summary>
        public const string BipedIdle = "BipedIdle";
        public const string BipedWalk = "BipedWalk";
        public const string BipedRun = "BipedRun";
        public const string ClubSwing = "ClubSwing";
        /// <summary>깡패판이 열린 순간 네 발 → 두 발 변신(원샷 2초, 2026-09-14). 시간표는 <see cref="GangTransformLogic"/>.</summary>
        public const string GangTransform = "GangTransform";

        // 용사 칼(2026-09-19) — 네 발 그대로 쓴다(두 발 서기는 깡패의 정체성이다). 원본: tools/blender/clips/*.py.
        // 칼 소품의 방향은 클립이 아니라 코드가 준다(리그에 손목이 없다) — HeroSwordVisualLogic·HeroTransformLogic.
        public const string SwordSwing = "SwordSwing";   // 칼을 오른쪽 위로 치켜들었다 앞으로 내리벤다(0.58초) — sword_swing.py
        public const string HeroDraw = "HeroDraw";       // 엉덩이에서 칼을 뽑아 하늘을 찌른다(1.5초) — hero_draw.py

        /// <summary>
        /// 칼에 맞아 굳은 자세(2026-09-19) — **쥐·고양이 둘 다** 갖는다(스턴은 진영을 가리지 않는다).
        /// 머리가 원을 그리며 휘청이는 0.5초 반복이라 스턴이 몇 초든 그동안 돈다.
        /// 두 컨트롤러에 **각자의 FBX에서** 넣는다(<see cref="CustomShared"/>) — 원본: stunned.py · cat_stunned.py.
        /// </summary>
        public const string Stunned = "Stunned";

        /// <summary>
        /// 고양이 광분·전직 스킬(2026-09-13) — **AC_Cat에만** 있다(원본: <c>art/blender/cat.blend</c> ·
        /// <c>tools/blender/clips/cat_frenzy.py</c>·<c>cat_skill.py</c> → <c>CTC_Cat_Clips.fbx</c>).
        /// 광분은 팩 <see cref="Spin"/>(한 발로 서서 발레처럼 한 바퀴)을 걷어낸 것이고, 전직 스킬은 예전엔 몸이 아무것도 안 했다.
        /// </summary>
        public const string Frenzy = "Frenzy";     // 낮게 버티며 꼬리 곧추세우고 하악

        // 전직 스킬 — **전직마다 동작이 다르다**(2026-09-13 사용자 결정). 스킬 효과가 서로 달라 동작도 그 효과를 말한다.
        // 처음엔 넷이 한 클립(CatSkill)이었다. 원본: tools/blender/clips/cat_skill_*.py
        public const string CatSkillFire = "CatSkillFire";   // 불(덮치기 피해) — 일어서 두 앞발로 내려친다
        public const string CatSkillWind = "CatSkillWind";   // 바람(이속) — 앞뒤로 길게 늘여 발진
        public const string CatSkillLight = "CatSkillLight"; // 빛(벽 너머 감지) — 앞발 하나 들고 꼬리 세워 살핀다
        public const string CatSkillDark = "CatSkillDark";   // 어둠(쥐 화면 암전) — 머리 묻고 꼬리 말아 웅크린다

        /// <summary>전직 → 스킬 동작. 전직이 없으면 <c>null</c>(스킬 자체가 안 나간다).</summary>
        public static string CatSkillFor(CatClassId _class)
        {
            switch (_class)
            {
                case CatClassId.Fire: return CatSkillFire;
                case CatClassId.Wind: return CatSkillWind;
                case CatClassId.Light: return CatSkillLight;
                case CatClassId.Dark: return CatSkillDark;
                default: return null;
            }
        }

        /// <summary>
        /// 이 게임이 이름으로 부르는 상태 전부. `Animator`가 주는 것은 이름이 아니라 해시라서,
        /// 「지금 무엇이 도는가」를 글자로 되돌리려면 후보 목록이 있어야 한다
        /// (<see cref="PlayerAnimationDriver.PlayingState"/>).
        ///
        /// 여기 없는 상태가 돌면 그 자리는 `?해시`로 나온다 — **없는 것을 있다고 읽지 않는다.**
        /// </summary>
        public static readonly string[] All =
        {
            Idle, Walk, Run, Attack, Hit, Roll, Bounce, Jump, Spin, Clicked, Eat,
            Shoot, ShootDraw, Dash, Sticky, Fortress, Warehouse, Frenzy,
            CatSkillFire, CatSkillWind, CatSkillLight, CatSkillDark,
            BipedIdle, BipedWalk, BipedRun, ClubSwing, GangTransform,
            SwordSwing, HeroDraw, Stunned,
        };

        /// <summary>
        /// **AC_Mouse에만** 있는 상태. 고양이 컨트롤러에 이 이름이 생기면 그건 실수다 —
        /// 고양이는 총을 쏘지 않는다. EditMode가 양쪽을 이 목록으로 갈라 검사한다.
        /// </summary>
        public static readonly string[] MouseOnly = { Shoot, ShootDraw, Dash, Sticky, Fortress, Warehouse, BipedIdle, BipedWalk, BipedRun, ClubSwing, GangTransform, SwordSwing, HeroDraw };

        /// <summary>**AC_Cat에만** 있는 상태(2026-09-13). 쥐 컨트롤러에 생기면 실수다.</summary>
        public static readonly string[] CatOnly = { Frenzy, CatSkillFire, CatSkillWind, CatSkillLight, CatSkillDark };

        /// <summary>
        /// **자작 클립인데 두 컨트롤러가 다 갖는** 상태(2026-09-19). 팩 상태(Idle·Walk…)와 달리 컨트롤러에 원래 없어서
        /// 굽는 메뉴가 넣어 준다 — 쥐는 <c>CTC_Mouse_Clips.fbx</c>, 고양이는 <c>CTC_Cat_Clips.fbx</c>의 같은 이름 클립으로
        /// (<c>RoleClipStates.AddMouse·AddCat</c>). <see cref="Shared"/>에 들어가므로 두 컨트롤러에 있는지는 거기서 검사된다.
        /// </summary>
        public static readonly string[] CustomShared = { Stunned };

        /// <summary>두 컨트롤러가 **둘 다** 가져야 하는 상태 — <see cref="All"/>에서 쥐 전용·고양이 전용을 뺀 것.</summary>
        public static readonly string[] Shared = Except(All, MouseOnly, CatOnly);

        private static string[] Except(string[] _all, params string[][] _remove)
        {
            var kept = new System.Collections.Generic.List<string>();
            foreach (string s in _all)
            {
                bool removed = false;
                foreach (string[] list in _remove)
                {
                    if (System.Array.IndexOf(list, s) >= 0) { removed = true; break; }
                }
                if (!removed) kept.Add(s);
            }
            return kept.ToArray();
        }

        public static string FromSpeed(float _speed, float _walkThreshold, float _runThreshold, bool _biped = false)
        {
            if (_speed >= _runThreshold) return _biped ? BipedRun : Run;
            if (_speed >= _walkThreshold) return _biped ? BipedWalk : Walk;
            return _biped ? BipedIdle : Idle;
        }
    }
}
