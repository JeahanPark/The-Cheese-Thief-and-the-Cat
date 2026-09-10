// TestManager의 일부 — 고양이 전직 (v3.9)
// 개발 빌드에도 실린다(2026-08-31) — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Player;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>QA: 지금 전직할 수 있는가(로컬 플레이어).</summary>
        public bool TestGetCanAdvance()
        {
            var cls = LocalComponent<CatClass>();
            return cls != null && cls.CanAdvance;
        }

        /// <summary>
        /// QA: 로컬 플레이어의 전직 — "None"|"Fire"|"Wind"|"Light"|"Dark".
        /// **컴포넌트를 못 찾으면 "Missing"**(2026-09-09 감사 T9) — `CatClassId.None`은
        /// 「아직 전직 안 했다」는 정상값이라, 미배선을 같은 글자로 주면
        /// 「처음에는 직업이 없다」가 빈 몸에서도 통과한다.
        /// </summary>
        public string TestGetCatClass()
        {
            var cls = LocalComponent<CatClass>();
            return cls != null ? cls.Value.ToString() : MISSING;
        }

        /// <summary>QA: 지정 클라이언트의 전직(복제 확인용). 못 찾으면 "Missing".</summary>
        public string TestGetCatClassOf(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            var cls = player != null ? player.GetComponent<CatClass>() : null;
            return cls != null ? cls.Value.ToString() : MISSING;
        }

        /// <summary>QA: 강화 중첩 "damage/speed".</summary>
        public string TestGetUpgradeStacks()
        {
            var pounce = LocalComponent<CatPounce>();
            var move = LocalComponent<PlayerMovement>();
            int d = pounce != null ? pounce.DamageStacks : -1;
            int s = move != null ? move.SpeedStacks : -1;
            return $"{d}/{s}";
        }

        /// <summary>
        /// QA: 누적 획득 장난기(전직 문턱이 보는 값)와 문턱을 "번값/문턱"으로.
        /// 잔액이 아니다 — 사도 안 줄어드는지 재는 데 쓴다.
        /// </summary>
        public string TestGetAdvanceMischief()
        {
            var wallet = LocalComponent<Economy.CheeseWallet>();
            int earned = wallet != null ? wallet.EarnedValue : -1;
            return $"{earned}/{CatClassLogic.MischiefThreshold}";
        }

        /// <summary>
        /// 서버 전용 — 전직 문턱만큼 **벌게 한다**(아무것도 사지 않고 전직이 열리는지 재는 훅).
        /// 이미 넘었으면 아무것도 안 한다. 지급은 <c>AddCheese</c>를 지나므로 잔액도 같이 오른다.
        /// </summary>
        public void TestGrantAdvanceMischief()
        {
            if (!RequireServer(nameof(TestGrantAdvanceMischief))) return;

            var wallet = LocalComponent<Economy.CheeseWallet>();
            if (wallet == null) { GameDebug.LogWarning("[TestManager] CheeseWallet 없음"); return; }

            int need = CatClassLogic.MischiefThreshold - wallet.EarnedValue;
            if (need > 0) wallet.AddCheese(need);
        }

        /// <summary>서버 전용 — 공격 강화만 만렙으로(덮치기 피해 기준선용). **전직과는 무관하다**.</summary>
        public void TestMaxOneUpgrade()
        {
            if (!RequireServer(nameof(TestMaxOneUpgrade))) return;

            var pounce = LocalComponent<CatPounce>();
            if (pounce == null) { GameDebug.LogWarning("[TestManager] CatPounce 없음"); return; }

            for (int i = 0; i < Economy.ShopLogic.MAX_UPGRADE_STACKS; i++) pounce.ServerAddDamageStack();
        }

        /// <summary>
        /// 서버 전용 — 강화를 둘 다 만렙으로. **이것만으로는 전직이 안 열린다**(2026-09-09):
        /// 문턱은 누적 획득이라 <c>TestGrantAdvanceMischief</c>를 따로 불러야 한다.
        /// </summary>
        public void TestMaxAllUpgrades()
        {
            if (!RequireServer(nameof(TestMaxAllUpgrades))) return;

            TestMaxOneUpgrade();

            var move = LocalComponent<PlayerMovement>();
            if (move == null) { GameDebug.LogWarning("[TestManager] PlayerMovement 없음"); return; }

            for (int i = 0; i < Economy.ShopLogic.MAX_UPGRADE_STACKS; i++) move.ServerAddSpeedStack();
        }

        /// <summary>QA: 전직 선택 창이 지금 떠 있는가 — 조건을 채우면 저절로 떠야 한다.</summary>
        public bool TestGetCatClassPopupOpen()
        {
            var popup = FindFirstObjectByType<UI.CatClassPopup>();
            if (popup == null)
            {
                GameDebug.LogWarning("[TestManager] TestGetCatClassPopupOpen — 씬에 CatClassPopup이 없다");
                return false;
            }
            return popup.IsOpen;
        }

        /// <summary>
        /// QA: 전직 선택 창의 버튼을 **실제로 누른다**(0=불 · 1=바람 · 2=빛 · 3=어둠).
        /// 창의 onClick 배선을 지나므로 「창은 뜨는데 버튼이 안 먹는다」를 잡는다 —
        /// <see cref="TestChooseCatClass"/>는 Rpc로 우회해 그 자리를 통째로 건너뛴다.
        /// </summary>
        public bool TestClickCatClassButton(int _index)
        {
            var popup = FindFirstObjectByType<UI.CatClassPopup>();
            if (popup == null || !popup.IsOpen)
            {
                GameDebug.LogWarning("[TestManager] TestClickCatClassButton — 창이 안 떠 있다");
                return false;
            }
            System.Reflection.FieldInfo info = typeof(UI.CatClassPopup).GetField(
                "m_Buttons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var buttons = info != null ? info.GetValue(popup) as UnityEngine.UI.Button[] : null;
            if (buttons == null || _index < 0 || _index >= buttons.Length || buttons[_index] == null)
            {
                GameDebug.LogWarning($"[TestManager] TestClickCatClassButton — {_index}번 버튼이 없다");
                return false;
            }
            buttons[_index].onClick.Invoke();
            return true;
        }

        /// <summary>QA: 전직 선택 — UI를 거치지 않고 같은 Rpc를 탄다(서버가 조건을 다시 본다).</summary>
        public void TestChooseCatClass(string _id)
        {
            var cls = LocalComponent<CatClass>();
            if (cls == null) return;
            if (!System.Enum.TryParse(_id, out CatClassId parsed)) return;

            cls.ChooseRpc(parsed);
        }

        public void TestUseCatSkill()
        {
            var skill = LocalComponent<CatSkill>();
            if (skill != null) skill.DevUse();
        }

        /// <summary>
        /// QA: 전직 스킬 키(Q) 한 번 — <see cref="CatSkill.PressSkillKey"/>가 곧 사람이 타는 분기다.
        /// <c>TestUseCatSkill</c>은 가드를 건너뛰므로 입력 계약을 재는 스위트는 이쪽을 쓴다
        /// (<c>TestPressSlotKey</c>와 같은 이유 — 2026-09-09 감사 G-1).
        /// <c>false</c> = 스킬이 없거나 가드에 막혔다(커서 미잠금·입력창 포커스).
        /// </summary>
        public bool TestPressSkillKey()
        {
            var skill = LocalComponent<CatSkill>();
            if (skill == null)
            {
                GameDebug.LogWarning("[TestManager] TestPressSkillKey — CatSkill 없음");
                return false;
            }
            return skill.PressSkillKey();
        }

        public bool TestGetCatSkillActive()
        {
            var skill = LocalComponent<CatSkill>();
            if (skill == null)
            {
                GameDebug.LogWarning("[TestManager] CatSkill 없음 — false는 「꺼짐」이 아니다");
                return false;
            }
            return skill.Active;
        }

        /// <summary>
        /// QA: 스킬 상태를 **세 값**으로 — "On"|"Off"|"Missing" (2026-09-09 감사 T9·E-06).
        /// </summary>
        public string TestGetCatSkillState()
        {
            var skill = LocalComponent<CatSkill>();
            if (skill == null) return MISSING;
            return skill.Active ? "On" : "Off";
        }

        /// <summary>QA: 지정 클라이언트의 스킬 활성(복제 확인용) — 쥐 화면 효과가 이걸 본다.</summary>
        public bool TestGetCatSkillActiveOf(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            var skill = player != null ? player.GetComponent<CatSkill>() : null;
            return skill != null && skill.Active;
        }

        /// <summary>QA: 남은 쿨다운 — 서버 시각 NetworkVariable이라 클론에서도 같은 값(2026-08-17).</summary>
        public float TestGetCatSkillCooldown()
        {
            var skill = LocalComponent<CatSkill>();
            return skill != null ? skill.CooldownRemaining : -1f;
        }

        /// <summary>QA: 지정 클라이언트 고양이의 남은 쿨다운 — **다른 클라에서 읽어** 복제를 증명한다.</summary>
        public float TestGetCatSkillCooldownOf(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            var skill = player != null ? player.GetComponent<CatSkill>() : null;
            return skill != null ? skill.CooldownRemaining : -1f;
        }

        private static T LocalComponent<T>() where T : Component
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var local = nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            return local != null ? local.GetComponent<T>() : null;
        }
    }
}
#endif
