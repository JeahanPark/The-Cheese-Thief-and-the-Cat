using System.Text;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// B-02 — 덮치기 피해 × 광분·불 배율과 쥐 최대 HP의 즉사 경계(「몇 대에 죽는가」)를 표로 못박는다.
    /// B-03 — 치즈 방패 최고 레벨에 잔액이 충분한 쥐는 즉사 피해 한 방을 견딘다.
    ///
    /// 값은 전부 **실제 CSV**(InfoManager)에서 읽고, 계산은 `CatPounce.ServerPounce`와 같은 함수 사슬
    /// (`ShopLogic.PounceDamage` → `CatClassLogic.PounceMultiplier(광분, 불)` → `RoundToInt` → `CombatLogic.ApplyDamage`)을 탄다.
    /// **표가 빨개지면 CSV가 「몇 대에 죽는가」를 바꾼 것이다** — 의도한 변경이면 표를 새 값으로 고친다.
    /// </summary>
    public class PounceLethalityTests
    {
        private const string MOUSE_PREFAB = "Assets/Prefabs/PlayerMouse.prefab";

        /// <summary>표의 열 순서 — 광분 Lv0~3 × 불 꺼짐/켜짐.</summary>
        private static readonly string[] COLUMNS =
        {
            "F0", "F1", "F2", "F3", "F0+Fire", "F1+Fire", "F2+Fire", "F3+Fire",
        };

        /// <summary>
        /// 쥐 기본 HP(현재 100)를 죽이는 데 드는 덮치기 수. 행 = 발톱 스택 0~3, 열 = <see cref="COLUMNS"/>.
        /// 2026-09-14 CSV 기준: 덮치기 60 · 발톱 +10/스택 · 광분 ×1.5/×2/×2.5 · 불 ×2 · 쥐 HP 100.
        /// </summary>
        private static readonly int[,] MOUSE_HITS =
        {
            { 2, 2, 1, 1, 1, 1, 1, 1 },   // 발톱 0 (60)  — 광분 Lv1(90)은 아직 두 대
            { 2, 1, 1, 1, 1, 1, 1, 1 },   // 발톱 1 (70)  — 광분 Lv1(105)부터 한 방
            { 2, 1, 1, 1, 1, 1, 1, 1 },   // 발톱 2 (80)
            { 2, 1, 1, 1, 1, 1, 1, 1 },   // 발톱 3 (90)  — 맨손 만렙은 여전히 두 대
        };

        /// <summary>용사 쥐(HP × HeroHpMultiplier, 현재 200) 기준 같은 표.</summary>
        private static readonly int[,] HERO_HITS =
        {
            { 4, 3, 2, 2, 2, 2, 2, 2 },
            { 3, 2, 2, 2, 2, 2, 2, 2 },
            { 3, 2, 2, 1, 2, 2, 2, 1 },   // 발톱 2 × 광분 Lv3 = 200 — 용사도 한 방
            { 3, 2, 2, 1, 2, 2, 2, 1 },
        };

        private static int CatBaseDamage => InfoManager.Instance.PlayerOf(RoleId.Cat).PounceDamage;

        /// <summary>`CatPounce`가 한 번에 넣는 피해 — 같은 함수 사슬을 탄다.</summary>
        private static int PounceHit(int _clawStacks, int _frenzyLevel, bool _fire)
        {
            float frenzy = CatFrenzyLogic.PounceMultiplier(_frenzyLevel > 0,
                TechLogic.ParamOf(TechId.CatFrenzy, _frenzyLevel, 1));
            float fire = _fire ? CatClassLogic.FireDamageMultiplier : 1f;
            float multiplier = CatClassLogic.PounceMultiplier(frenzy, fire);
            int damage = ShopLogic.PounceDamage(CatBaseDamage, _clawStacks);
            return Mathf.RoundToInt(damage * multiplier);
        }

        private static int HitsToKill(int _maxHp, int _hit)
        {
            Assert.Greater(_hit, 0, "피해 0이면 영영 안 죽는다 — CSV를 확인하라");
            int hp = _maxHp, hits = 0;
            while (!CombatLogic.IsDead(hp))
            {
                hp = CombatLogic.ApplyDamage(hp, _hit);
                hits++;
            }
            return hits;
        }

        private static void AssertTable(string _label, int _maxHp, int[,] _expected)
        {
            int maxStacks = ShopLogic.MAX_UPGRADE_STACKS;
            int maxFrenzy = TechLogic.MaxLevel(TechId.CatFrenzy);
            Assert.AreEqual(_expected.GetLength(0) - 1, maxStacks,
                "발톱 스택 상한이 바뀌었다 — 표의 행 수를 맞춰라");
            Assert.AreEqual(_expected.GetLength(1), (maxFrenzy + 1) * 2,
                "광분 최고 레벨이 바뀌었다 — 표의 열 수를 맞춰라");

            var actual = new StringBuilder();
            bool same = true;
            for (int claw = 0; claw <= maxStacks; claw++)
            {
                actual.Append($"  발톱 {claw}: ");
                for (int col = 0; col < _expected.GetLength(1); col++)
                {
                    bool fire = col > maxFrenzy;
                    int frenzy = fire ? col - (maxFrenzy + 1) : col;
                    int hit = PounceHit(claw, frenzy, fire);
                    int hits = HitsToKill(_maxHp, hit);
                    actual.Append($"{COLUMNS[col]}={hits}({hit}) ");
                    if (hits != _expected[claw, col]) same = false;
                }
                actual.AppendLine();
            }

            Assert.IsTrue(same,
                $"{_label}(HP {_maxHp})의 「몇 대에 죽는가」가 바뀌었다 — CSV 변경이 의도한 것이면 표를 고쳐라.\n" +
                $"실제 (대수(한 방 피해)):\n{actual}");
        }

        [NUnit.Framework.Property("CaseId", "B-02")]
        [Test]
        public void HitsToKill_Mouse_ByClawFrenzyFire_IsPinned()
        {
            AssertTable("쥐", InfoManager.Instance.PlayerOf(RoleId.Mouse).MaxHp, MOUSE_HITS);
        }

        [NUnit.Framework.Property("CaseId", "B-02")]
        [Test]
        public void HitsToKill_HeroMouse_ByClawFrenzyFire_IsPinned()
        {
            // MouseJob.ApplyHeroBody와 같은 계산
            int heroHp = Mathf.RoundToInt(InfoManager.Instance.PlayerOf(RoleId.Mouse).MaxHp * MouseJobLogic.HeroHpMultiplier);
            AssertTable("용사 쥐", heroHp, HERO_HITS);
        }

        [NUnit.Framework.Property("CaseId", "B-02")]
        [Test]
        public void OneShotBoundary_BareCatNeedsTwo_FrenzyOrFireOneShots()
        {
            int mouseHp = InfoManager.Instance.PlayerOf(RoleId.Mouse).MaxHp;
            int maxStacks = ShopLogic.MAX_UPGRADE_STACKS;

            // 맨손 고양이는 발톱을 다 사도 쥐를 한 방에 못 잡는다 — 덮치기 한 번을 피하면 도망칠 여지가 있다.
            Assert.Less(PounceHit(maxStacks, 0, false), mouseHp,
                "발톱 만렙 맨손 덮치기가 쥐를 한 방에 죽인다 — 배율 없이 즉사가 되면 광분·불의 값어치가 사라진다");
            // 불 하나만 켜도 기본 덮치기가 한 방이다.
            Assert.GreaterOrEqual(PounceHit(0, 0, true), mouseHp, "불(전직) 기본 덮치기가 한 방이 아니게 됐다");
            // 광분과 불은 곱하지 않는다 — 둘 다 켜도 큰 쪽.
            Assert.AreEqual(PounceHit(maxStacks, TechLogic.MaxLevel(TechId.CatFrenzy), false),
                PounceHit(maxStacks, TechLogic.MaxLevel(TechId.CatFrenzy), true),
                "광분 Lv3(×2.5)이 불(×2)보다 커서 둘을 같이 켜도 광분 값이어야 한다");
        }

        // ───────────────────────── B-03 ─────────────────────────

        [NUnit.Framework.Property("CaseId", "B-03")]
        [Test]
        public void CheeseShieldMaxLevel_EnoughCheese_SurvivesEveryPounceFromFullHp()
        {
            int mouseHp = InfoManager.Instance.PlayerOf(RoleId.Mouse).MaxHp;
            int maxShield = TechLogic.MaxLevel(TechId.CheeseShield);
            float ratio = TechLogic.ParamOf(TechId.CheeseShield, maxShield, 1);
            int rate = InfoManager.Instance.Balance.CheeseShieldRate;
            Assert.Greater(maxShield, 0, "치즈 방패 행이 없다");
            Assert.Greater(ratio, 0f, "치즈 방패 최고 레벨 비율이 0이다");
            Assert.Greater(rate, 0, "CheeseShieldRate가 0이다");

            int maxStacks = ShopLogic.MAX_UPGRADE_STACKS;
            int maxFrenzy = TechLogic.MaxLevel(TechId.CatFrenzy);
            int lethalSeen = 0;
            for (int claw = 0; claw <= maxStacks; claw++)
            for (int frenzy = 0; frenzy <= maxFrenzy; frenzy++)
            foreach (bool fire in new[] { false, true })
            {
                int hit = PounceHit(claw, frenzy, fire);
                if (hit < mouseHp) continue;   // 즉사 피해만 본다
                lethalSeen++;

                // 「잔액이 충분하다」 = 방패가 원하는 몫을 전부 치즈로 낼 수 있다.
                int wantHp = Mathf.Min(hit, Mathf.RoundToInt(hit * ratio));
                int cheese = wantHp * rate;

                CheeseShieldLogic.Absorb(hit, ratio, rate, cheese, mouseHp, out int hpDamage, out int cheeseCost);
                int after = CombatLogic.ApplyDamage(mouseHp, hpDamage);

                Assert.IsFalse(CombatLogic.IsDead(after),
                    $"발톱 {claw} · 광분 Lv{frenzy} · 불 {fire}: 한 방 {hit} → 방패 Lv{maxShield}(×{ratio}) " +
                    $"치즈 {cheese}로도 HP 피해 {hpDamage} ≥ {mouseHp} — 방패의 생존 하한이 무너졌다");
                Assert.AreEqual(cheese, cheeseCost, "살아남는 타격이면 원하는 치즈를 전부 받아야 한다");
            }

            Assert.Greater(lethalSeen, 0, "즉사 피해 조합이 하나도 없다 — B-02 표와 함께 전제를 다시 보라");
        }

        [NUnit.Framework.Property("CaseId", "B-03")]
        [Test]
        [NUnit.Framework.Category("Asset")]
        public void CheeseShield_IsTheMouseDamageAbsorber()
        {
            // Health.TakeDamage는 GetComponent<IDamageAbsorber>로 방패를 집는다 — 인터페이스가 빠지면 조용히 안 막는다.
            Assert.IsTrue(typeof(IDamageAbsorber).IsAssignableFrom(typeof(CheeseShield)));

            GameObject mouse = AssetDatabase.LoadAssetAtPath<GameObject>(MOUSE_PREFAB);
            Assert.IsTrue(mouse != null, $"프리팹 없음: {MOUSE_PREFAB}");
            Assert.IsTrue(mouse.GetComponent<CheeseShield>() != null, "쥐 프리팹에 CheeseShield가 없다");
            Assert.IsTrue(mouse.GetComponent<Health>() != null, "쥐 프리팹에 Health가 없다");
            Assert.IsTrue(mouse.GetComponent<IDamageAbsorber>() != null, "쥐 프리팹에서 IDamageAbsorber를 못 찾는다");
        }
    }
}
