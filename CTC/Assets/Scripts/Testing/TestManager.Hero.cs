// TestManager의 일부 — 용사 칼·스턴·무적 (2026-09-19)
// 개발 빌드에도 실린다 — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Core;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    /// <summary>
    /// 용사 QA 훅(2026-09-19). **이 인스턴스의 화면에서** 그 몸이 어떻게 보이는지를 한 줄로 —
    /// 칼 모습·무적 테두리는 복제값·원샷 구독으로 켜지는 기능이라 다른 인스턴스에서 읽어야 전파가 검증된다.
    /// </summary>
    public partial class TestManager
    {
        /// <summary>
        /// "hero=True transforming=False motion=swing swordA=1.00 swordOn=True state=SwordSwing" 꼴. 몸이 없으면 "missing".
        /// <c>swordOn</c>은 칼 오브젝트가 **실제로 켜져 있나**(계층에서) — 알파 값만 보면 배선 누락을 못 잡는다.
        /// </summary>
        public string TestGetHeroLook(long _clientId)
        {
            var body = NetUtil.FindPlayerObject((ulong)_clientId);
            if (body == null) return "missing";

            var look = body.GetComponent<Player.HeroLook>();
            var driver = body.GetComponent<Player.PlayerAnimationDriver>();
            bool swordOn = false;
            foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != Player.HeroLook.SWORD_NAME) continue;
                swordOn = t.gameObject.activeInHierarchy;
                break;
            }
            return $"hero={(look != null && look.Active)} transforming={(look != null && look.Transforming)} " +
                   $"motion={(look != null ? look.Motion : "")} swordA={(look != null ? look.SwordAlpha : -1f):0.00} " +
                   $"swordOn={swordOn} state={(driver != null ? driver.PlayingState : "")}";
        }

        /// <summary>그 몸의 남은 스턴 초(굳지 않았으면 0). 몸이나 <c>Stun</c>이 없으면 -1.</summary>
        public float TestGetStunRemaining(long _clientId)
        {
            var body = NetUtil.FindPlayerObject((ulong)_clientId);
            var stun = body != null ? body.GetComponent<Combat.Stun>() : null;
            return stun != null ? stun.Remaining : -1f;
        }

        /// <summary>
        /// 서버 전용 — 그 몸에 스턴을 건다(칼을 거치지 않고 **겹침 규칙**만 볼 때). 서버가 아니거나 몸이 없으면 false.
        /// </summary>
        public bool TestApplyStun(long _clientId, float _seconds)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return false;
            var body = NetUtil.FindPlayerObject((ulong)_clientId);
            var stun = body != null ? body.GetComponent<Combat.Stun>() : null;
            if (stun == null) return false;
            stun.ServerApply(_seconds);
            return true;
        }

        /// <summary>
        /// "on=True active=2.40 cd=9.40 rim=1.00" — 그 쥐의 무적. <c>rim</c>은 **이 인스턴스 화면의** 테두리 세기다.
        /// 몸이 없으면 "missing".
        /// </summary>
        public string TestGetHeroInvincible(long _clientId)
        {
            var body = NetUtil.FindPlayerObject((ulong)_clientId);
            if (body == null) return "missing";
            var inv = body.GetComponent<Player.HeroInvincible>();
            var rim = body.GetComponent<Player.HeroInvincibleVisual>();
            if (inv == null) return "missing";
            return $"on={inv.On} active={inv.ActiveRemaining:0.00} cd={inv.CooldownRemaining:0.00} " +
                   $"rim={(rim != null ? rim.RimLevel : -1f):0.00}";
        }

        /// <summary>
        /// **이 인스턴스 화면의** 스턴 테두리 세기(0~0.75) — 내 몸이 굳었을 때만 선다. 씬에 없으면 -1.
        /// </summary>
        public float TestGetStunVignette()
        {
            var vignette = FindAnyObjectByType<UI.StunVignette>();
            return vignette != null ? vignette.Level : -1f;
        }

        /// <summary>QA: 로컬 용사가 무적을 켠다 — Space와 같은 Rpc를 탄다(용사가 아니면 서버가 거절한다).</summary>
        public void TestUseInvincible()
        {
            var inv = LocalComponent<Player.HeroInvincible>();
            if (inv != null) inv.DevUse();
        }
    }
}
#endif
