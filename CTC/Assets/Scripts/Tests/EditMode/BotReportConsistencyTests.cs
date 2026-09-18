using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using CTC.Runtime.Bots;
using CTC.Runtime.Lobby;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 봇 계측기(<see cref="TestBotManager"/>)의 **보고 한 줄들끼리의 약속**(케이스 E-12·E-15).
    ///
    /// 네트워크·씬 없이 잰다 — 매니저는 빈 GameObject에 붙이고, 봇은 몸 없는 <see cref="BotAgent"/>를
    /// 만들어 계측 칸만 채운다. 몸이 없으니 봇은 전부 「살아 있지 않다」로 보이고, 목록에서 빼면
    /// 사망으로 이월된다(<c>Retire</c>). 살아 있는 봇의 칸은 목록에 남긴 봇이 대신한다.
    /// </summary>
    public class BotReportConsistencyTests
    {
        private const BindingFlags ANY = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private GameObject m_Host;
        private TestBotManager m_Manager;
        private List<BotAgent> m_Agents;

        [SetUp]
        public void SetUp()
        {
            m_Host = new GameObject("BotReportConsistencyTests");
            m_Manager = m_Host.AddComponent<TestBotManager>();
            FieldInfo agents = typeof(TestBotManager).GetField("m_Agents", ANY);
            Assert.IsNotNull(agents, "TestBotManager.m_Agents 이름이 바뀌었다 — 이 하네스를 고친다");
            m_Agents = (List<BotAgent>)agents.GetValue(m_Manager);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Agents != null) m_Agents.Clear();   // OnDestroy→Stop이 몸 없는 봇을 만지지 않게
            if (m_Host != null) Object.DestroyImmediate(m_Host);
        }

        // ───────────────────────────── E-12 같은 수량 = 같은 값 ─────────────────────────────

        [NUnit.Framework.Property("CaseId", "E-12")]
        [Test]
        public void MouseReport_And_TechStats_AgreeOnMiceAndTechs_WithRetiredAndLiving()
        {
            // 판을 떠난 쥐 둘(테크 2 · 0) + 아직 목록에 있는 쥐 하나(테크 1) + 고양이 하나.
            Retire(Bot(RoleId.Mouse, _techs: 2, _cheese: 30));
            Retire(Bot(RoleId.Mouse, _techs: 0, _cheese: 5));
            m_Agents.Add(Bot(RoleId.Mouse, _techs: 1, _cheese: 12));
            m_Agents.Add(Bot(RoleId.Cat, _techs: 3, _cheese: 0));

            string report = m_Manager.MouseReport();
            string tech = m_Manager.MouseTechStats();

            Assert.AreEqual(IntOf(report, "mice"), IntOf(tech, "mice"),
                $"두 훅이 쥐 수를 다르게 센다 — 어느 쪽을 읽느냐로 결론이 뒤집힌다\nreport: {report}\ntech:   {tech}");
            Assert.AreEqual(IntOf(report, "techs"), IntOf(tech, "techs"),
                $"두 훅이 테크 수를 다르게 센다\nreport: {report}\ntech:   {tech}");
            Assert.AreEqual(3, IntOf(tech, "mice"), "고양이가 섞였거나 퇴장한 쥐가 빠졌다");
            Assert.AreEqual(3, IntOf(tech, "techs"));
        }

        [NUnit.Framework.Property("CaseId", "E-12")]
        [Test]
        public void MouseReport_And_TechStats_AgreeWhenEveryMouseIsGone()
        {
            // 전멸한 판 — 예전에는 한쪽만 이월해 `mice=0 techs=0` 대 `mice=4 techs=5`로 갈렸다(감사 F-9).
            for (int i = 0; i < 4; i++) Retire(Bot(RoleId.Mouse, _techs: i == 0 ? 2 : 1, _cheese: 10));

            string report = m_Manager.MouseReport();
            string tech = m_Manager.MouseTechStats();

            Assert.AreEqual(IntOf(report, "mice"), IntOf(tech, "mice"), $"report: {report}\ntech: {tech}");
            Assert.AreEqual(IntOf(report, "techs"), IntOf(tech, "techs"), $"report: {report}\ntech: {tech}");
            Assert.AreEqual(4, IntOf(tech, "mice"));
            Assert.AreEqual(40, m_Manager.MouseCheese(), "지갑 합도 퇴장한 쥐의 마지막 잔고를 센다");
        }

        // ───────────────────────────── E-15 시야 통계·사인 라벨 ─────────────────────────────

        [NUnit.Framework.Property("CaseId", "E-15")]
        [Test]
        public void SightStats_CountsCatsOnly_LivingAndRetired()
        {
            // 쥐 표본을 크게 넣는다 — 섞이면 숫자가 바로 티가 난다(감사 A5).
            m_Agents.Add(Bot(RoleId.Mouse, _sights: 100, _meters: 900f, _max: 90f, _far: 50));
            m_Agents.Add(Bot(RoleId.Cat, _sights: 10, _meters: 200f, _max: 30f, _far: 1));
            Retire(Bot(RoleId.Mouse, _sights: 70, _meters: 700f, _max: 99f, _far: 40));
            Retire(Bot(RoleId.Cat, _sights: 5, _meters: 50f, _max: 40f, _far: 2));

            string stats = m_Manager.SightStats();

            Assert.AreEqual(15, IntOf(stats, "sights"), "쥐 표본이 섞였다: " + stats);
            Assert.AreEqual(3, IntOf(stats, "far"), "쥐의 먼 표본이 섞였다: " + stats);
            Assert.AreEqual(40f, FloatOf(stats, "max"), 0.05f, "최대 거리가 쥐 값이다: " + stats);
            Assert.AreEqual(250f / 15f, FloatOf(stats, "avg"), 0.05f, "평균에 쥐가 섞였다: " + stats);
        }

        [NUnit.Framework.Property("CaseId", "E-15")]
        [Test]
        public void DeathLabel_KeepsBothSlots_DyingActivityAndCalmActivity()
        {
            BotAgent mouse = Bot(RoleId.Mouse, _lived: 42f);
            mouse.Activity = "Shop";
            mouse.Activity = "Flee";      // 위협 가지가 덮는다
            mouse.Activity = "Shelter";
            Assert.AreEqual("Shop", mouse.CalmActivity, "위협 반응 라벨이 「하던 일」을 덮었다(감사 A6)");

            Retire(mouse);
            string report = m_Manager.MouseReport();

            StringAssert.Contains("deaths=42:Shelter>Shop", report,
                "사인 라벨이 두 칸(죽는 순간 > 그 전에 하던 일)이 아니다 — 「Flee 199/202」 동어반복으로 돌아간다");
        }

        // ───────────────────────────── 하네스 ─────────────────────────────

        /// <summary>몸 없는 봇 — 생성자는 몸·InfoManager를 요구하므로 우회하고 계측 칸만 채운다.</summary>
        private static BotAgent Bot(RoleId _role, int _techs = 0, int _cheese = 0, int _sights = 0,
                                    float _meters = 0f, float _max = 0f, int _far = 0, float _lived = 0f)
        {
            var agent = (BotAgent)FormatterServices.GetUninitializedObject(typeof(BotAgent));
            Set(agent, "m_Role", _role);
            Set(agent, "m_Lived", _lived);
            Set(agent, "m_Activity", "Idle");
            Set(agent, "m_CalmActivity", "Idle");
            SetAuto(agent, "LastTechCount", _techs);
            SetAuto(agent, "LastCheese", _cheese);
            SetAuto(agent, "SightSamples", _sights);
            SetAuto(agent, "SightMetersSum", _meters);
            SetAuto(agent, "MaxSightDistance", _max);
            SetAuto(agent, "FarSightSamples", _far);
            SetAuto(agent, "FirstSightSeconds", -1f);
            SetAuto(agent, "FirstSightDistance", -1f);
            Assert.IsFalse(agent.Alive, "몸 없는 봇이 살아 있다고 나온다 — 하네스 전제가 깨졌다");
            return agent;
        }

        private void Retire(BotAgent _agent)
        {
            MethodInfo retire = typeof(TestBotManager).GetMethod("Retire", ANY);
            Assert.IsNotNull(retire, "TestBotManager.Retire 이름이 바뀌었다 — 이 하네스를 고친다");
            retire.Invoke(m_Manager, new object[] { _agent });
        }

        private static void Set(object _target, string _field, object _value)
        {
            FieldInfo f = _target.GetType().GetField(_field, ANY);
            Assert.IsNotNull(f, $"{_target.GetType().Name}.{_field} 이름이 바뀌었다 — 이 하네스를 고친다");
            f.SetValue(_target, _value);
        }

        private static void SetAuto(object _target, string _property, object _value)
            => Set(_target, "<" + _property + ">k__BackingField", _value);

        private static int IntOf(string _line, string _key)
        {
            Match m = Regex.Match(_line, @"\b" + _key + @"=(-?\d+)");
            Assert.IsTrue(m.Success, $"'{_key}=' 칸이 없다: {_line}");
            return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static float FloatOf(string _line, string _key)
        {
            Match m = Regex.Match(_line, @"\b" + _key + @"=(-?\d+(?:[.,]\d+)?)");
            Assert.IsTrue(m.Success, $"'{_key}=' 칸이 없다: {_line}");
            return float.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        }
    }
}
