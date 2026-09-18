using System;
using System.Threading;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상점 철문(2026-09-19, 기획 `2026-09-19-shop-booth-design.md` §2) — 고양이가 가까이 오면 점원이 창구 철문을 쾅 내리고,
    /// 멀어지면 드르륵 올린다. **닫힌 동안 그 상점에서는 못 산다**(<see cref="ShopSpot.AnyOpenContains"/>).
    ///
    /// 서버가 0.2초마다 고양이 거리를 보고(살금살금이면 더 가까이 와야 한다) 복제값 <see cref="Closed"/> 하나를 바꾼다.
    /// 전 클라가 그 값의 변경을 보고 **각자** 철문을 움직이고 소리를 낸다(통신 추가 0). 늦게 들어온 사람은 연출 없이 지금 모습.
    /// 판정은 <see cref="ShopShutterLogic"/>, 반경·시간은 CSV `ShopShutter*`.
    ///
    /// 철문 판(<see cref="PANEL_NAME"/>)은 윗변이 원점인 메시라 **세로 배율**로 말렸다 내려온다(<c>ShopBoothBuilder</c>가 굽는다).
    /// </summary>
    public class ShopShutter : NetworkBehaviour
    {
        public const string PANEL_NAME = "ShutterPanel";

        /// <summary>가게 전등(점광원 + 전구) — 철문이 내려오면 따라 꺼진다(<c>ShopBoothBuilder</c>가 굽는다).</summary>
        public const string LAMP_NAME = "ShopLamp";

        /// <summary>다 올라갔을 때의 세로 배율 — 셔터 상자 밑에 말린 두께만 남는다.</summary>
        private const float ROLLED_SCALE = 0.04f;

        /// <summary>열릴 때 소리 크기 — 닫힐 때(쾅)보다 작게.</summary>
        private const float OPEN_VOLUME = 0.45f;

        private readonly NetworkVariable<bool> m_Closed = new NetworkVariable<bool>(false);

        private Transform m_Panel;
        private Light m_Lamp;
        private Renderer m_Bulb;
        private float m_LampIntensity;
        private float m_Lowered;
        private float m_QuietSince;
        private CancellationTokenSource m_AnimCts;
        private CancellationTokenSource m_ServerCts;

        /// <summary>지금 닫혀 있나(복제값).</summary>
        public bool Closed => m_Closed.Value;

        /// <summary>QA: 철문이 내려온 정도(0 = 열림 · 1 = 닫힘) — 이 인스턴스 화면의 값.</summary>
        public float Lowered => m_Lowered;

        /// <summary>닫힘이 바뀐 순간(전 클라) — 점원이 구독한다. 스폰 때 지금 값으로 한 번 울린다.</summary>
        public event Action<bool> ClosedChanged;

        private void Awake()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == PANEL_NAME) m_Panel = t;
                else if (t.name == LAMP_NAME)
                {
                    m_Lamp = t.GetComponent<Light>();
                    m_Bulb = t.GetComponentInChildren<Renderer>(true);
                }
            }
            if (m_Lamp != null) m_LampIntensity = m_Lamp.intensity;
            SetLowered(0f);
        }

        public override void OnNetworkSpawn()
        {
            m_Closed.OnValueChanged += OnClosedValueChanged;
            SetLowered(m_Closed.Value ? 1f : 0f);
            ClosedChanged?.Invoke(m_Closed.Value);

            if (!IsServer) return;
            m_QuietSince = Time.time;
            m_ServerCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            ServerLoopAsync(m_ServerCts.Token).Forget();
        }

        public override void OnNetworkDespawn()
        {
            m_Closed.OnValueChanged -= OnClosedValueChanged;
            Cancel(ref m_ServerCts);
            Cancel(ref m_AnimCts);
        }

        private async UniTaskVoid ServerLoopAsync(CancellationToken _token)
        {
            int tickMs = Mathf.RoundToInt(ShopShutterLogic.TICK_SECONDS * 1000f);
            while (true)
            {
                ServerEvaluate();
                bool cancelled = await UniTask.Delay(tickMs, cancellationToken: _token).SuppressCancellationThrow();
                if (cancelled) return;
            }
        }

        // 고양이 거리만 본다 — 강아지(제3세력)는 상점을 노리지 않는다. 죽은 고양이는 안 무섭다.
        private void ServerEvaluate()
        {
            Info.BalanceInfo b = Info.InfoManager.Instance.Balance;
            Vector3 here = transform.position;
            bool anyAlarm = false, anyNear = false;
            foreach (RoleTag tag in FindObjectsByType<RoleTag>())
            {
                if (tag == null || tag.Role != RoleId.Cat) continue;
                var health = tag.GetComponent<Health>();
                if (health != null && health.IsDead) continue;

                Vector3 d = tag.transform.position - here;
                d.y = 0f;
                float distance = d.magnitude;
                var sneak = tag.GetComponent<Player.Sneak>();
                bool sneaking = sneak != null && sneak.Sneaking;
                anyAlarm |= ShopShutterLogic.Alarms(distance, sneaking, b.ShopShutterCloseMeters, b.ShopShutterSneakMeters);
                anyNear |= distance < b.ShopShutterOpenMeters;
            }

            if (anyAlarm || anyNear) m_QuietSince = Time.time;
            bool next = ShopShutterLogic.NextClosed(m_Closed.Value, anyAlarm, anyNear,
                Time.time - m_QuietSince, b.ShopShutterReopenSeconds);
            if (next != m_Closed.Value) m_Closed.Value = next;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>QA 전용(서버) — 고양이 없이 철문을 강제로 여닫는다. 다음 판정(0.2초)에서 거리 규칙이 다시 덮는다.</summary>
        public void DevForceClosed(bool _closed)
        {
            if (!IsServer) return;
            m_Closed.Value = _closed;
        }
#endif

        // 복제값이 바뀌는 자리 = 전 클라가 도는 길 — 소리·연출은 여기서만(서버 판정 자리 금지).
        private void OnClosedValueChanged(bool _old, bool _new)
        {
            if (_old == _new) return;
            Audio.SfxPlayer.PlayAt(Audio.SfxId.ShopShutter, transform.position, _new ? 1f : OPEN_VOLUME);
            Animate(_new);
            ClosedChanged?.Invoke(_new);
        }

        private void Animate(bool _closing)
        {
            Cancel(ref m_AnimCts);
            m_AnimCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            AnimateAsync(_closing, m_Lowered, m_AnimCts.Token).Forget();
        }

        private async UniTaskVoid AnimateAsync(bool _closing, float _from, CancellationToken _token)
        {
            float start = Time.time;
            while (true)
            {
                float since = Time.time - start;
                SetLowered(ShopShutterLogic.Lowered(_from, _closing, since));
                if (ShopShutterLogic.Settled(_closing, since)) break;
                bool cancelled = await UniTask.NextFrame(_token).SuppressCancellationThrow();
                if (cancelled) return;
            }
        }

        private void SetLowered(float _lowered)
        {
            m_Lowered = _lowered;
            // 전등은 철문을 따라 꺼진다 — 닫힌 가게는 어둡다(멀리서도 「닫았다」가 읽힌다)
            if (m_Lamp != null)
            {
                m_Lamp.intensity = m_LampIntensity * (1f - _lowered);
                m_Lamp.enabled = _lowered < 0.99f;
            }
            if (m_Bulb != null) m_Bulb.enabled = _lowered < 0.5f;
            if (m_Panel == null) return;
            Vector3 s = m_Panel.localScale;
            s.y = Mathf.Lerp(ROLLED_SCALE, 1f, _lowered);
            m_Panel.localScale = s;
        }

        private static void Cancel(ref CancellationTokenSource _cts)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
