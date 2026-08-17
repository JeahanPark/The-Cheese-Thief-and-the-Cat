// TestManager의 일부 — 고양이 전직 (v3.9)
#if UNITY_EDITOR
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

        /// <summary>QA: 로컬 플레이어의 전직 — "None"|"Fire"|"Wind"|"Light"|"Dark".</summary>
        public string TestGetCatClass()
        {
            var cls = LocalComponent<CatClass>();
            return cls != null ? cls.Value.ToString() : "None";
        }

        /// <summary>QA: 지정 클라이언트의 전직(복제 확인용).</summary>
        public string TestGetCatClassOf(long _clientId)
        {
            var player = NetUtil.FindPlayerObject((ulong)_clientId);
            var cls = player != null ? player.GetComponent<CatClass>() : null;
            return cls != null ? cls.Value.ToString() : "None";
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

        /// <summary>서버 전용 — 공격 강화만 만렙으로. "하나만 올리면 안 열린다"를 재는 데 쓴다.</summary>
        public void TestMaxOneUpgrade()
        {
            var pounce = LocalComponent<CatPounce>();
            if (pounce == null) return;

            for (int i = 0; i < Economy.ShopLogic.MAX_UPGRADE_STACKS; i++) pounce.ServerAddDamageStack();
        }

        /// <summary>서버 전용 — 강화를 둘 다 만렙으로.</summary>
        public void TestMaxAllUpgrades()
        {
            TestMaxOneUpgrade();

            var move = LocalComponent<PlayerMovement>();
            if (move == null) return;

            for (int i = 0; i < Economy.ShopLogic.MAX_UPGRADE_STACKS; i++) move.ServerAddSpeedStack();
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

        public bool TestGetCatSkillActive()
        {
            var skill = LocalComponent<CatSkill>();
            return skill != null && skill.Active;
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
