using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 능력 바(2026-08-17) — 상점 핫바 아래 줄, 하단 중앙 슬롯 묶음. 좌하단 글자 칩(ControlsHud)을 대체한다.
    /// 슬롯 구성은 스트림(직업·대시 보유·전직)으로, 채움(쿨·지속)은 시간 파생값이라
    /// **살아 있는 동안만** EveryUpdate로 읽는다 — 아무 쿨도 없으면 구독 자체가 없다.
    /// </summary>
    public class AbilityBar : MonoBehaviour
    {
        private const float SLOT_STEP = 84f + 12f;   // 슬롯 폭 + 간격 — 빌더 상수와 같다
        // 핫바 칸 치수(빌더 SLOT_WIDTH·SLOT_GAP과 같다). 핫바는 6칸 폭에서 왼쪽부터 채우므로
        // 역할별로 '쓰는 칸'의 가운데가 다르다 — 능력 칸은 그 가운데 아래에 놓는다.
        private const float HOTBAR_SLOT_WIDTH = 132f;
        private const float HOTBAR_SLOT_GAP = 8f;

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private GameObject m_Root;
        [SerializeField] private AbilitySlotView[] m_Slots;   // 빌더가 2칸 — 남는 칸은 끄고 가운데 정렬

        private readonly SerialDisposable m_JobSub = new SerialDisposable();
        private readonly SerialDisposable m_DashSub = new SerialDisposable();
        private readonly SerialDisposable m_ClassSub = new SerialDisposable();
        private readonly SerialDisposable m_Tick = new SerialDisposable();

        private bool m_GameStarted;
        private RoleId m_Role = RoleId.None;
        private MouseJobId m_Job;
        private bool m_DashOwned;
        private CatClassId m_Class;
        private MouseDash m_Dash;
        private CatSkill m_Skill;
        private AbilitySlot[] m_Built = System.Array.Empty<AbilitySlot>();

        public AbilitySlot[] Built => m_Built;

        private void Awake()
        {
            m_Root.SetActive(false);
        }

        private void Start()
        {
            m_JobSub.AddTo(this);
            m_DashSub.AddTo(this);
            m_ClassSub.AddTo(this);
            m_Tick.AddTo(this);
            m_Lobby.GameStartedChanged.Subscribe(s => { m_GameStarted = s; Rebuild(); }).AddTo(this);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            RoleTag tag = _player != null ? _player.GetComponent<RoleTag>() : null;
            m_Role = tag != null ? tag.Role : RoleId.None;

            var job = _player != null ? _player.GetComponent<MouseJob>() : null;
            m_Job = MouseJobId.None;
            m_JobSub.Disposable = job != null ? job.JobChanged.Subscribe(j => { m_Job = j; Rebuild(); }) : null;

            m_Dash = _player != null ? _player.GetComponent<MouseDash>() : null;
            m_DashOwned = false;
            m_DashSub.Disposable = m_Dash != null ? m_Dash.OwnedChanged.Subscribe(o => { m_DashOwned = o; Rebuild(); }) : null;

            var catClass = _player != null ? _player.GetComponent<CatClass>() : null;
            m_Skill = _player != null ? _player.GetComponent<CatSkill>() : null;
            m_Class = CatClassId.None;
            m_ClassSub.Disposable = catClass != null ? catClass.ClassChanged.Subscribe(c => { m_Class = c; Rebuild(); }) : null;

            Rebuild();
        }

        private void Rebuild()
        {
            bool show = m_GameStarted && m_Role != RoleId.None;
            if (m_Root.activeSelf != show) m_Root.SetActive(show);
            if (!show)
            {
                m_Built = System.Array.Empty<AbilitySlot>();
                m_Tick.Disposable = null;
                return;
            }

            m_Built = AbilityBarLogic.Build(m_Role, m_Job, m_DashOwned, m_Class);
            if (m_Built.Length == 0)   // 해금된 게 없으면 바 자체를 안 그린다
            {
                m_Root.SetActive(false);
                m_Tick.Disposable = null;
                return;
            }
            var rootRt = (RectTransform)m_Root.transform;
            rootRt.anchoredPosition = new Vector2(HotbarUsedCenterX(ShopLogic.RoleCatalogIndices(m_Role).Count), rootRt.anchoredPosition.y);
            float left = -(m_Built.Length - 1) * SLOT_STEP * 0.5f;   // 쓰는 칸만 가운데 정렬
            for (int i = 0; i < m_Slots.Length; i++)
            {
                bool used = i < m_Built.Length;
                if (m_Slots[i].gameObject.activeSelf != used) m_Slots[i].gameObject.SetActive(used);
                if (!used) continue;
                ((RectTransform)m_Slots[i].transform).anchoredPosition = new Vector2(left + i * SLOT_STEP, 0f);
                m_Slots[i].SetSlot(m_Built[i]);
            }
            Tick(0f);
            EnsureTicker();
        }

        /// <summary>핫바가 실제로 쓰는 n칸의 가운데 x(핫바 루트 중심 기준) — 순수.</summary>
        public static float HotbarUsedCenterX(int _usedSlots)
        {
            float total = HOTBAR_SLOT_WIDTH * ShopLogic.HOTBAR_SLOTS + HOTBAR_SLOT_GAP * (ShopLogic.HOTBAR_SLOTS - 1);
            float used = HOTBAR_SLOT_WIDTH * _usedSlots + HOTBAR_SLOT_GAP * Mathf.Max(0, _usedSlots - 1);
            return -total * 0.5f + used * 0.5f;
        }

        private void EnsureTicker()
        {
            if (m_Tick.Disposable != null) return;
            m_Tick.Disposable = Observable.EveryUpdate().Subscribe(_ =>
            {
                Tick(Time.deltaTime);
                if (!AnyAlive()) m_Tick.Disposable = null;
            });
        }

        private bool AnyAlive()
        {
            bool alive = false;
            for (int i = 0; i < m_Built.Length; i++) alive |= m_Slots[i].IsAnimating;
            if (m_Dash != null && m_DashOwned) alive |= m_Dash.CooldownRemaining > 0f || m_Dash.ActiveRemaining > 0f;
            if (m_Skill != null && m_Class != CatClassId.None) alive |= m_Skill.CooldownRemaining > 0f || m_Skill.ActiveRemaining > 0f;
            return alive;
        }

        private void Tick(float _dt)
        {
            for (int i = 0; i < m_Built.Length; i++)
            {
                AbilitySlot slot = m_Built[i];
                float cd = 0f, cdTotal = 0f, act = 0f, actTotal = 0f;
                if (slot.Key == AbilityBarLogic.KEY_DASH && m_Dash != null)
                {
                    cd = m_Dash.CooldownRemaining; cdTotal = m_Dash.CooldownTotal;
                    act = m_Dash.ActiveRemaining; actTotal = m_Dash.ActiveTotal;
                }
                else if (slot.Key == AbilityBarLogic.KEY_SKILL && m_Skill != null)
                {
                    cd = m_Skill.CooldownRemaining; cdTotal = m_Skill.CooldownTotal;
                    act = m_Skill.ActiveRemaining; actTotal = m_Skill.ActiveTotal;
                }
                m_Slots[i].Apply(slot, cd, cdTotal, act, actTotal, _dt);
            }
        }

        // 폴링이 아니라 "틱을 켤 필요가 생겼나"만 본다 — 쿨다운 시작은 컴포넌트 내부 값이라 스트림이 없다
        // (ControlsHud와 같은 사정, 규칙 3 예외). 틱이 도는 동안엔 아무것도 안 한다.
        private void Update()
        {
            if (m_Built.Length > 0 && m_Tick.Disposable == null && AnyAlive()) EnsureTicker();
        }
    }
}
