using System;
using System.Text;
using CTC.Runtime.Core;
using CTC.Runtime.Combat;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 좌하단 특수키 칩 한 줄 (HUD 개편 2026-08-15).
    ///
    /// 예전엔 조작키+핫바 항목 설명 장문 6줄이었다 — "기능이 늘면 다 담을 수 없다"(사용자).
    /// 역할 분담: 항목 설명은 핫바 슬롯 힌트·구매 토스트, 기본 조작(WASD류)은 첫 스폰 토스트,
    /// 여기는 **지금 이 사람이 쓸 수 있는 특수키만** 칩으로 남긴다(역할·보유·직업 조건부).
    /// 쿨다운 중에는 칩이 어두워지고 남은 초가 붙는다.
    ///
    /// **"무음"이라고 쓰면 안 된다**(2026-08-10) — 잠행은 발소리를 없애는 게 아니라
    /// 좁힐 뿐이다. 안내가 거짓이면 안 들킬 줄 알고 서 있다가 잡힌다.
    /// </summary>
    public class ControlsHud : MonoBehaviour
    {
        // 쿨다운 초읽기는 스트림이 없는 시간 파생값이라 짧은 인터벌로 읽는다
        // (CombatHud의 한시 재시도와 같은 결) — 표시 초가 바뀔 때만 다시 그린다.
        private const float COOLDOWN_TICK_SECONDS = 0.25f;

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private readonly SerialDisposable m_JobSub = new SerialDisposable();
        private readonly SerialDisposable m_DashSub = new SerialDisposable();
        private readonly SerialDisposable m_ClassSub = new SerialDisposable();
        private readonly SerialDisposable m_CooldownTick = new SerialDisposable();

        private RoleId m_Role = RoleId.None;
        private bool m_HasPlayer;
        private Match.MouseJobId m_Job = Match.MouseJobId.None;
        private bool m_DashOwned;
        private bool m_HasSkill;
        private int m_DashCooldownShown;
        private int m_SkillCooldownShown;
        private Player.MouseDash m_Dash;
        private Player.CatSkill m_Skill;

        private void Start()
        {
            m_Text.gameObject.SetActive(false);
            m_JobSub.AddTo(this);
            m_DashSub.AddTo(this);
            m_ClassSub.AddTo(this);
            m_CooldownTick.AddTo(this);

            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            RoleTag tag = _player != null ? _player.GetComponent<RoleTag>() : null;
            m_HasPlayer = tag != null;
            m_Role = tag != null ? tag.Role : RoleId.None;

            // 직업은 판 중간에 바뀐다(상자·임명) — 스트림으로 F 칩을 붙였다 뗀다
            var job = _player != null ? _player.GetComponent<Match.MouseJob>() : null;
            m_Job = Match.MouseJobId.None;
            m_JobSub.Disposable = job != null
                ? job.JobChanged.Subscribe(j => { m_Job = j; Redraw(); })
                : null;

            m_Dash = _player != null ? _player.GetComponent<Player.MouseDash>() : null;
            m_DashOwned = false;
            m_DashSub.Disposable = m_Dash != null
                ? m_Dash.OwnedChanged.Subscribe(o => { m_DashOwned = o; UpdateTicker(); Redraw(); })
                : null;

            var catClass = _player != null ? _player.GetComponent<Player.CatClass>() : null;
            m_Skill = _player != null ? _player.GetComponent<Player.CatSkill>() : null;
            m_HasSkill = false;
            m_ClassSub.Disposable = catClass != null
                ? catClass.ClassChanged.Subscribe(c =>
                {
                    m_HasSkill = c != Player.CatClassId.None;
                    UpdateTicker();
                    Redraw();
                })
                : null;

            UpdateTicker();
            Redraw();
        }

        // 쿨다운을 표시할 이유가 있을 때만 돈다 — 대시 미보유·미전직이면 인터벌 자체가 없다
        private void UpdateTicker()
        {
            bool need = (m_DashOwned && m_Dash != null) || (m_HasSkill && m_Skill != null);
            if (!need)
            {
                m_CooldownTick.Disposable = null;
                return;
            }

            m_CooldownTick.Disposable = Observable
                .Interval(TimeSpan.FromSeconds(COOLDOWN_TICK_SECONDS), UnityTimeProvider.Update)
                .Subscribe(_ => TickCooldowns());
        }

        private void TickCooldowns()
        {
            int dash = SecondsLeft(m_Dash != null ? m_Dash.CooldownRemaining : 0f);
            int skill = SecondsLeft(m_Skill != null ? m_Skill.CooldownRemaining : 0f);
            if (dash == m_DashCooldownShown && skill == m_SkillCooldownShown) return;
            Redraw();
        }

        private void Redraw()
        {
            bool started = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;

            // 매치 전에는 아예 숨긴다 — 로비에는 조작 안내가 필요 없다
            if (!started && !m_HasPlayer)
            {
                m_Text.gameObject.SetActive(false);
                return;
            }

            m_Text.gameObject.SetActive(true);
            RoleId role = m_HasPlayer ? m_Role : RoleId.None; // None = 관전자
            m_DashCooldownShown = SecondsLeft(m_Dash != null ? m_Dash.CooldownRemaining : 0f);
            m_SkillCooldownShown = SecondsLeft(m_Skill != null ? m_Skill.CooldownRemaining : 0f);
            m_Text.text = BuildChips(role, m_Job, m_DashOwned, m_DashCooldownShown,
                m_HasSkill, m_SkillCooldownShown, SneakMetersOf(role));
        }

        // 발소리 거리는 PlayerInfo.csv가 원본 — 문자열에 박으면 밸런스를 바꿀 때 안내가 거짓이 된다
        private static float SneakMetersOf(RoleId _role)
        {
            if (_role != RoleId.Cat && _role != RoleId.Mouse) return 0f;
            return Info.InfoManager.Instance.PlayerOf(_role).SneakFootstepRangeMeters;
        }

        private static int SecondsLeft(float _remaining) => Mathf.CeilToInt(_remaining);

        /// <summary>칩 문자열 조립 — 순수 함수(EditMode 테스트 대상).</summary>
        public static string BuildChips(RoleId _role, Match.MouseJobId _job, bool _dashOwned,
            int _dashCooldownSeconds, bool _hasSkill, int _skillCooldownSeconds, float _sneakMeters)
        {
            if (_role == RoleId.None)
            {
                return Chip("<b>G</b> 보고 있는 쥐에게 따봉 · 고양이에겐 안 보인다");
            }

            var sb = new StringBuilder();
            sb.Append(Chip($"<b>LCtrl</b> 살금살금 · 발소리 {_sneakMeters:0.#}m"));
            if (_role == RoleId.Mouse && _dashOwned)
            {
                sb.Append("  ").Append(CooldownChip("LShift", "대시", _dashCooldownSeconds));
            }
            if (_role == RoleId.Cat && _hasSkill)
            {
                sb.Append("  ").Append(CooldownChip("Q", "스킬", _skillCooldownSeconds));
            }
            if (_job == Match.MouseJobId.Gang)
            {
                sb.Append("  ").Append(Chip("<b>F</b> 부하 임명"));
            }
            return sb.ToString();
        }

        private static string Chip(string _body) => $"<mark=#10151CC8> {_body} </mark>";

        // 쿨다운 중엔 어두운 칩 + 남은 초. CatSkill 쿨다운은 서버 값이라 클라 고양이에게는
        // 0으로 보인다(칩이 항상 밝음) — 호스트 중심 개발 단계 한계, CatSkill 주석 참조.
        private static string CooldownChip(string _key, string _name, int _secondsLeft)
        {
            return _secondsLeft > 0
                ? $"<mark=#10151C78><color=#7C8798> {_key} {_name} {_secondsLeft} </color></mark>"
                : Chip($"<b>{_key}</b> {_name}");
        }
    }
}
