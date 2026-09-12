// TestManager의 일부 — **총이 실제로 보이고 있나**(2026-09-12 첫 플레이 검증).
//
// 알파 페이드는 0.10초(뽑기)·0.08초(바꿔치기)짜리다. MCP/wesqa 왕복은 그보다 느려서
// 한 번 물어보는 방식으로는 **그 구간을 통째로 놓친다** — `TestGetAnimObserved`와 같은 병이다.
// 그래서 여기서도 **프레임마다 기록해 두고 나중에 읽는다**.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using CTC.Runtime.Player;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>기록 상한 — 60fps로 ~33초. 넘으면 앞에서 버린다.</summary>
        private const int GUN_SAMPLE_CAP = 2000;

        private readonly List<string> m_GunSamples = new List<string>();
        private MouseGunVisual m_GunWatched;
        private PlayerAnimationDriver m_GunWatchedDriver;
        private float m_GunSampleStart;
        private string m_GunLastSample;

        private static MouseGunVisual GunOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            return p != null ? p.GetComponent<MouseGunVisual>() : null;
        }

        /// <summary>
        /// QA: 지금 이 순간의 총 상태 한 줄.
        /// "held=1.00 holstered=0.00 heldOn=1 holsteredOn=0 isOut=1"
        /// 못 찾으면 <see cref="MISSING"/>.
        /// </summary>
        public string TestGetGunVisual(long _clientId)
        {
            MouseGunVisual g = GunOf(_clientId);
            return g != null ? Describe(g) : MISSING;
        }

        /// <summary>QA: 로컬 플레이어 판 — clientId를 모르는 쪽이 쓴다.</summary>
        public string TestGetGunVisualLocal()
        {
            MouseGunVisual g = LocalComponent<MouseGunVisual>();
            return g != null ? Describe(g) : MISSING;
        }

        private static string Describe(MouseGunVisual _g)
        {
            return $"held={_g.HeldAlpha:F2} holstered={_g.HolsteredAlpha:F2} " +
                   $"heldOn={(_g.IsOut && _g.HeldAlpha > 0f ? 1 : 0)} " +
                   $"holsteredOn={(_g.IsHolsteredShowing ? 1 : 0)} " +
                   $"isOut={(_g.IsOut ? 1 : 0)}";
        }

        /// <summary>
        /// QA: 기록 창을 연다 — 이 프레임부터 총 알파·애니 상태를 **매 프레임** 적는다.
        /// 쏘기 직전에 부르고, 끝나면 <see cref="TestGetGunTrace"/>로 읽는다.
        /// false = 그 몸에 총 비주얼이 없다.
        /// </summary>
        public bool TestStartGunTrace(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            m_GunWatched = p != null ? p.GetComponent<MouseGunVisual>() : null;
            m_GunWatchedDriver = p != null ? p.GetComponent<PlayerAnimationDriver>() : null;
            m_GunSamples.Clear();
            m_GunLastSample = null;
            m_GunSampleStart = Time.time;
            return m_GunWatched != null;
        }

        /// <summary>QA: 기록을 끈다.</summary>
        public void TestStopGunTrace()
        {
            m_GunWatched = null;
            m_GunWatchedDriver = null;
        }

        /// <summary>
        /// QA: 기록된 흔적. 한 줄 = "t|허리춤|손|애니상태", 연속 중복은 접힌다.
        /// 알파는 소수 둘째까지 반올림해 비교하므로 미세 흔들림은 한 줄로 접힌다.
        /// </summary>
        public string TestGetGunTrace()
        {
            if (m_GunSamples.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (string s in m_GunSamples)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(s);
            }
            return sb.ToString();
        }

        /// <summary>
        /// QA: 흔적의 판정 요약 한 줄 — 눈으로 훑지 않아도 사고를 집어낸다.
        ///
        /// bothVisible = 둘 다 0.05를 넘긴 프레임 수(바꿔치기 크로스페이드 중엔 합이 1이라
        /// 몇 프레임 생기는 게 정상이다. 길면 「총이 둘로 보인다」).
        /// bothHidden = 총이 나와 있어야 하는데 둘 다 0.05 미만인 프레임 수.
        /// stuck = 마지막 샘플이 0도 1도 아닌 어중간한 알파인가(반투명인 채로 굳음).
        /// </summary>
        public string TestGetGunTraceSummary()
        {
            if (m_GunSamples.Count == 0) return "samples=0";

            int both = 0, neither = 0;
            float maxHeld = 0f, maxHolstered = 0f;
            float lastHeld = 0f, lastHolstered = 0f;
            var states = new List<string>();

            foreach (string s in m_GunSamples)
            {
                string[] f = s.Split('|');
                if (f.Length < 4) continue;
                float.TryParse(f[1], out float hol);
                float.TryParse(f[2], out float held);
                if (hol > 0.05f && held > 0.05f) both++;
                if (hol <= 0.05f && held <= 0.05f) neither++;
                if (held > maxHeld) maxHeld = held;
                if (hol > maxHolstered) maxHolstered = hol;
                lastHeld = held; lastHolstered = hol;
                if (states.Count == 0 || states[states.Count - 1] != f[3]) states.Add(f[3]);
            }

            bool stuck = (lastHeld > 0.05f && lastHeld < 0.95f) || (lastHolstered > 0.05f && lastHolstered < 0.95f);
            return $"samples={m_GunSamples.Count} maxHeld={maxHeld:F2} maxHolstered={maxHolstered:F2} " +
                   $"bothVisible={both} bothHidden={neither} lastHeld={lastHeld:F2} lastHolstered={lastHolstered:F2} " +
                   $"stuck={(stuck ? 1 : 0)} states={string.Join(">", states)}";
        }

        private void Update()
        {
            if (m_GunWatched == null) return;

            float t = Time.time - m_GunSampleStart;
            string state = m_GunWatchedDriver != null ? m_GunWatchedDriver.PlayingState : "-";
            string line = $"{t:F2}|{m_GunWatched.HolsteredAlpha:F2}|{m_GunWatched.HeldAlpha:F2}|{state}";

            // 시각을 뺀 나머지가 같으면 접는다 — 안 그러면 60fps짜리 잡음만 쌓인다.
            string key = line.Substring(line.IndexOf('|'));
            if (key == m_GunLastSample) return;
            m_GunLastSample = key;

            if (m_GunSamples.Count >= GUN_SAMPLE_CAP) m_GunSamples.RemoveAt(0);
            m_GunSamples.Add(line);
        }

        /// <summary>
        /// QA: 총 머티리얼이 **게임에서 진짜 반투명으로 그려지나**.
        /// 미리보기 렌더러(PreviewRenderUtility)는 URP/Lit을 못 그려 Sprites/Default로 바꿔치기했다 —
        /// 즉 **이 값들은 지금까지 한 번도 실제로 확인된 적이 없다**.
        ///
        /// "shader=... surface=1 blend=0 zwrite=0 queue=3000 src=5 dst=10 kw=... alpha=0.40 renderQueueActual=3000"
        /// </summary>
        public string TestGetGunMaterialInfo(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            if (p == null) return MISSING;
            MouseGunVisual visual = p.GetComponent<MouseGunVisual>();
            if (visual == null) return MISSING;

            Renderer r = FindGunRenderer(p, "MouseGun");
            if (r == null) return MISSING;

            Material m = r.sharedMaterial;
            if (m == null) return "NoMaterial";

            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            Color pb = block.GetColor(Shader.PropertyToID("_BaseColor"));

            return $"shader={(m.shader != null ? m.shader.name : "NULL")} " +
                   $"surface={m.GetFloat("_Surface"):F0} blend={m.GetFloat("_Blend"):F0} " +
                   $"zwrite={m.GetFloat("_ZWrite"):F0} src={m.GetFloat("_SrcBlend"):F0} dst={m.GetFloat("_DstBlend"):F0} " +
                   $"queue={m.renderQueue} tag={m.GetTag("RenderType", false, "?")} " +
                   $"kwTransparent={(m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") ? 1 : 0)} " +
                   $"shadow={r.shadowCastingMode} " +
                   $"blockColor=({pb.r:F2},{pb.g:F2},{pb.b:F2},{pb.a:F2})";
        }

        /// <summary>
        /// QA: 든 총의 **월드 크기와 자리**. 본 월드 스케일 100 × 로컬 0.01이라 0.22m가 나와야 한다 —
        /// 스케일 보정이 빠지면 22m다.
        /// "size=(0.05,0.07,0.22) lossy=(1.00,1.00,1.00) distToBone=0.00 pos=(..)"
        /// </summary>
        public string TestGetGunTransform(long _clientId, string _which)
        {
            GameObject p = FindPlayerGo(_clientId);
            if (p == null) return MISSING;

            string name = _which == "holstered" ? "MouseGunHolstered" : "MouseGun";
            Renderer r = FindGunRenderer(p, name);
            if (r == null) return MISSING;

            Transform t = r.transform;
            Bounds b = r.bounds;
            Vector3 ls = t.lossyScale;
            Transform bone = t.parent;
            return $"name={t.name} size=({b.size.x:F3},{b.size.y:F3},{b.size.z:F3}) " +
                   $"lossy=({ls.x:F3},{ls.y:F3},{ls.z:F3}) local={t.localScale.x:F4} " +
                   $"bone={(bone != null ? bone.name : "?")} " +
                   $"boneLossy={(bone != null ? bone.lossyScale.x : 0f):F2} " +
                   $"world=({t.position.x:F2},{t.position.y:F2},{t.position.z:F2}) " +
                   $"fromRoot={(t.position - p.transform.position).magnitude:F3} " +
                   $"active={(t.gameObject.activeInHierarchy ? 1 : 0)}";
        }

        /// <summary>
        /// QA: **표적 없이** 정면으로 한 발. 봇(<c>BotFire</c>)과 같은 입구를 타므로
        /// 쿨다운·발사음·TracerRpc·총 비주얼이 전부 사람 총알과 같은 코드를 지난다.
        ///
        /// 왜 필요한가: <c>TestShootAtCat</c>은 표적이 있어야 하는데, 표적으로 세운 고양이 봇이
        /// **QA 도중 나를 죽여 판을 끝내 버린다**(2026-09-12 실측: 2회 연속 CatWin으로 중단).
        /// 총이 어떻게 보이나를 재는 데 표적은 필요 없다.
        /// </summary>
        public bool TestShootForward()
        {
            Combat.MousePopgun gun = LocalComponent<Combat.MousePopgun>();
            if (gun == null) return false;
            gun.BotFire(gun.transform.forward);
            return true;
        }

        /// <summary>
        /// QA: 한 발 쏘고 <paramref name="_gameSeconds"/>(게임 시간) 뒤에 **화면을 얼린다**.
        ///
        /// 왜: 뽑기 전체가 0.2초다. 페이드 한복판(반투명)이 몇 프레임밖에 안 살아서
        /// 스크린샷 왕복으로는 절대 못 잡는다 — 그런데 「URP 반투명이 게임 화면에서
        /// 진짜로 섞이는가」는 **그 프레임을 봐야만** 알 수 있다(미리보기 렌더러는
        /// URP/Lit을 못 그려 Sprites/Default로 바꿔치기했으므로 검증된 적이 없다).
        /// 얼린 뒤 <see cref="TestGetGunVisualLocal"/>로 그때의 알파를 같이 읽으면
        /// 「무엇을 찍었는지」가 추측이 아니라 숫자로 남는다.
        ///
        /// 푸는 것은 <see cref="TestSetTimeScaleRaw"/>(1) 이다.
        /// </summary>
        public bool TestShootThenFreeze(float _gameSeconds)
        {
            Combat.MousePopgun gun = LocalComponent<Combat.MousePopgun>();
            if (gun == null) return false;

            CancelFreeze();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            gun.BotFire(gun.transform.forward);

            m_FreezeCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(
                destroyCancellationToken);
            FreezeAfterAsync(_gameSeconds, m_FreezeCts.Token).Forget();
            return true;
        }

        /// <summary>
        /// 얼리기 대기 — 순수 C# 객체라 `?.`를 써도 된다(규칙 9 예외).
        ///
        /// **반드시 하나만 산다.** 얼린 동안 <c>Time.deltaTime</c>이 0이라 이 루프는
        /// 영원히 제자리를 돈다 — 다음 호출에서 시간을 다시 흘리면 **쌓여 있던 옛 대기들이
        /// 한꺼번에 목표에 닿아 곧바로 다시 얼린다.** 2026-09-12에 실제로 그랬다:
        /// 「2.6초 뒤에 얼린다」가 사실은 「쏘자마자 얼린다」였고, 수납 페이드를 못 찍었다.
        /// </summary>
        private System.Threading.CancellationTokenSource m_FreezeCts;

        private void CancelFreeze()
        {
            m_FreezeCts?.Cancel();
            m_FreezeCts?.Dispose();
            m_FreezeCts = null;
        }

        private static async Cysharp.Threading.Tasks.UniTaskVoid FreezeAfterAsync(
            float _seconds, System.Threading.CancellationToken _token)
        {
            try
            {
                float elapsed = 0f;
                while (elapsed < _seconds)
                {
                    await Cysharp.Threading.Tasks.UniTask.Yield(
                        Cysharp.Threading.Tasks.PlayerLoopTiming.Update, _token);
                    elapsed += Time.deltaTime;
                }
            }
            catch (System.OperationCanceledException)
            {
                return; // 취소한 쪽이 배속을 정한다
            }
            Time.timeScale = 0f;
        }

        /// <summary>
        /// QA: 시간 배속 — <b>0을 허용한다</b>. <c>TestSetTimeScale</c>은 0.1에서 자르는데,
        /// 페이드 한복판을 찍으려면 **완전히 멈춰야** 한다. 0에서는 <c>Time.deltaTime</c>이
        /// 0이라 페이드 루프도 UniTask 지연도 그 자리에 선다.
        /// </summary>
        public float TestSetTimeScaleRaw(float _scale)
        {
            CancelFreeze();   // 예약된 얼리기가 살아 있으면 푼 시간이 곧바로 다시 멈춘다
            Time.timeScale = Mathf.Clamp(_scale, 0f, 20f);
            if (Time.timeScale > 0f) Time.fixedDeltaTime = 0.02f / Time.timeScale;
            return Time.timeScale;
        }

        /// <summary>
        /// QA: 총 티어를 직접 올린다 — 상점은 **자리와 페이즈** 조건이 있어서
        /// 연사(Tier 2, 쿨다운 0.15초)를 보려고 상점을 통과시키면 그게 실패했을 때
        /// 「연사를 쟀다」가 조용히 거짓이 된다(2026-09-12 실측: 구매가 막혀 Lv0인 채로 잼).
        /// 서버 전용. 돌려주는 값은 적용된 티어, 못 찾으면 -1.
        /// </summary>
        public int TestSetWeaponTier(long _clientId, int _tier)
        {
            if (!RequireServer(nameof(TestSetWeaponTier))) return -1;

            GameObject p = FindPlayerGo(_clientId);
            Combat.MousePopgun gun = p != null ? p.GetComponent<Combat.MousePopgun>() : null;
            if (gun == null) return -1;

            gun.ServerSetTier(_tier);
            return gun.Tier;
        }

        /// <summary>
        /// QA: 궤도 카메라를 **바짝 당긴다** — 0.22m짜리 총이 기본 거리에서 60px밖에 안 돼
        /// 「들었나 꿰뚫었나」를 눈으로 못 가린다. 게임 뷰 그대로라 파이프라인은 진짜다
        /// (미리보기 렌더러와 달리 URP/Lit이 실제로 도는 화면이다).
        /// 돌려주는 값은 적용된 반경, 못 찾으면 -1.
        /// </summary>
        public float TestSetCameraDistance(float _meters)
        {
            var orbital = FindAnyObjectByType<Unity.Cinemachine.CinemachineOrbitalFollow>();
            if (orbital == null) return -1f;
            orbital.Radius = Mathf.Clamp(_meters, 0.2f, 30f);
            return orbital.Radius;
        }

        /// <summary>
        /// QA: 해가 세다/약하다를 잠깐 바꾼다 — 어두운 창고에서 총 실루엣이 안 보여
        /// 「투명이 먹었나 물체가 없나」가 안 갈릴 때 쓴다. 판정을 바꾸지 않는 관찰용이다.
        /// </summary>
        public float TestSetSunIntensity(float _intensity)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.intensity = Mathf.Clamp(_intensity, 0f, 20f);
                return l.intensity;
            }
            return -1f;
        }

        /// <summary>이름으로 총 렌더러를 찾는다 — 꺼져 있어도 찾아야 하므로 비활성 포함.</summary>
        private static Renderer FindGunRenderer(GameObject _root, string _name)
        {
            foreach (Transform t in _root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != _name) continue;
                Renderer r = t.GetComponentInChildren<Renderer>(true);
                if (r != null) return r;
            }
            return null;
        }
    }
}
#endif
