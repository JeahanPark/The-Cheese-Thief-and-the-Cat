using CTC.Runtime.Core;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 우상단 상태 클러스터 (HUD 개편 2026-08-15) — 흩어져 있던 좌상단 `HP/CAT` 텍스트와
    /// 우상단 `치즈:` 텍스트를 한 덩이로 합쳤다. CombatHud·WalletHud를 대체한다:
    /// 내 HP 바 + 재화 + 상시 배지 한 줄.
    ///
    /// 배지 줄은 "내가 지금 가진 것"의 단어 나열이다(쥐: 무기 티어·대시·직업 / 고양이: 강화
    /// 스택·전직). 문장 설명은 구매 토스트(3초)와 MouseJobHud 팝업(3초)의 몫 — "상시는 얇게".
    ///
    /// **고양이 HP는 쥐에게 보여주지 않는다**(2026-08-15 확정). 상대 체력이 보이면 "지금 몇 발
    /// 남았나"가 계산이 되고, 고양이가 얼마나 다쳤는지는 소리·행동으로 읽어야 하는 정보다.
    /// 관전자는 표시할 게 없어 클러스터 자체가 숨는다.
    /// </summary>
    public class StatusHud : MonoBehaviour
    {
        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private GameObject m_Root;
        [SerializeField] private Image m_MyHpFill;
        [SerializeField] private TMP_Text m_MyHpText;
        [SerializeField] private TMP_Text m_WalletText;
        [SerializeField] private TMP_Text m_BadgeText;

        private readonly SerialDisposable m_HpSub = new SerialDisposable();
        private readonly SerialDisposable m_WalletSub = new SerialDisposable();
        private readonly SerialDisposable m_WeaponSub = new SerialDisposable();
        private readonly SerialDisposable m_DashSub = new SerialDisposable();
        private readonly SerialDisposable m_JobSub = new SerialDisposable();
        private readonly SerialDisposable m_PounceSub = new SerialDisposable();
        private readonly SerialDisposable m_SpeedSub = new SerialDisposable();
        private readonly SerialDisposable m_ClassSub = new SerialDisposable();

        private bool m_GameStarted;
        private bool m_HasPlayer;
        private RoleId m_Role = RoleId.None;

        // 배지 재료 — 어느 스트림이 흘러도 RedrawBadge()가 전부 다시 조립한다
        private int m_Tier;
        private bool m_DashOwned;
        private Match.MouseJobId m_Job = Match.MouseJobId.None;
        private int m_DamageStacks;
        private int m_SpeedStacks;
        private Player.CatClassId m_Class = Player.CatClassId.None;

        private void Start()
        {
            m_Root.SetActive(false);
            m_HpSub.AddTo(this);
            m_WalletSub.AddTo(this);
            m_WeaponSub.AddTo(this);
            m_DashSub.AddTo(this);
            m_JobSub.AddTo(this);
            m_PounceSub.AddTo(this);
            m_SpeedSub.AddTo(this);
            m_ClassSub.AddTo(this);

            m_Lobby.GameStartedChanged.Subscribe(OnGameStartedChanged).AddTo(this);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnGameStartedChanged(bool _started)
        {
            m_GameStarted = _started;
            ApplyActive();
        }

        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            RoleTag tag = _player != null ? _player.GetComponent<RoleTag>() : null;
            m_HasPlayer = tag != null;
            m_Role = tag != null ? tag.Role : RoleId.None;

            var health = _player != null ? _player.GetComponent<Health>() : null;
            m_HpSub.Disposable = health != null
                ? health.HpChangedRx.Subscribe(hp => DrawMyHp(hp, health.MaxHp))
                : null;

            var wallet = _player != null ? _player.GetComponent<CheeseWallet>() : null;
            m_WalletSub.Disposable = wallet != null
                ? wallet.Cheese.Subscribe(c =>
                    m_WalletText.text = $"{ShopLogic.CurrencyNameOf(m_Role)} {c}")
                : null;

            BindBadgeStreams(_player);
            ApplyActive();
        }

        // 배지 재료 스트림 6종 — 프리팹에 없는 컴포넌트(역할이 다르면)는 조용히 건너뛴다
        private void BindBadgeStreams(NetworkObject _player)
        {
            m_Tier = 0;
            m_DashOwned = false;
            m_Job = Match.MouseJobId.None;
            m_DamageStacks = 0;
            m_SpeedStacks = 0;
            m_Class = Player.CatClassId.None;

            var gun = _player != null ? _player.GetComponent<MousePopgun>() : null;
            m_WeaponSub.Disposable = gun != null
                ? gun.TierChanged.Subscribe(t => { m_Tier = t; RedrawBadge(); }) : null;

            var dash = _player != null ? _player.GetComponent<Player.MouseDash>() : null;
            m_DashSub.Disposable = dash != null
                ? dash.OwnedChanged.Subscribe(o => { m_DashOwned = o; RedrawBadge(); }) : null;

            var job = _player != null ? _player.GetComponent<Match.MouseJob>() : null;
            m_JobSub.Disposable = job != null
                ? job.JobChanged.Subscribe(j => { m_Job = j; RedrawBadge(); }) : null;

            var pounce = _player != null ? _player.GetComponent<CatPounce>() : null;
            m_PounceSub.Disposable = pounce != null
                ? pounce.DamageStacksChanged.Subscribe(d => { m_DamageStacks = d; RedrawBadge(); }) : null;

            var move = _player != null ? _player.GetComponent<Player.PlayerMovement>() : null;
            m_SpeedSub.Disposable = move != null
                ? move.SpeedStacksChanged.Subscribe(s => { m_SpeedStacks = s; RedrawBadge(); }) : null;

            var catClass = _player != null ? _player.GetComponent<Player.CatClass>() : null;
            m_ClassSub.Disposable = catClass != null
                ? catClass.ClassChanged.Subscribe(c => { m_Class = c; RedrawBadge(); }) : null;

            RedrawBadge();
        }

        // 게임 중 + 내 몸이 있을 때만 — 관전자는 보여줄 상태가 없다
        private void ApplyActive()
        {
            bool show = m_GameStarted && m_HasPlayer;
            if (m_Root.activeSelf != show) m_Root.SetActive(show);
        }

        private void DrawMyHp(int _hp, int _maxHp)
        {
            m_MyHpText.text = $"HP {_hp}/{_maxHp}";
            SetFill(m_MyHpFill, _maxHp > 0 ? _hp / (float)_maxHp : 0f);
        }

        // 채움은 앵커 비율로 그린다 — Filled 타입은 스프라이트 유무에 따라 동작이 갈려서 안 쓴다
        private static void SetFill(Image _fill, float _ratio)
        {
            var rt = _fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(_ratio), 1f);
        }

        private void RedrawBadge()
        {
            m_BadgeText.text = BuildBadge(m_Role, m_Tier, m_DashOwned, m_Job,
                m_DamageStacks, m_SpeedStacks, m_Class);
        }

        /// <summary>상시 배지 한 줄 — 순수 함수(EditMode 테스트 대상). 단어만, 문장 금지.</summary>
        public static string BuildBadge(RoleId _role, int _tier, bool _dashOwned,
            Match.MouseJobId _job, int _damageStacks, int _speedStacks, Player.CatClassId _class)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (_role == RoleId.Mouse)
            {
                parts.Add(_tier >= 2 ? "기관총" : _tier == 1 ? "새총" : "딱총");
                if (_dashOwned) parts.Add("대시");
                string job = JobWordOf(_job);
                if (job != "") parts.Add(job);
            }
            else if (_role == RoleId.Cat)
            {
                if (_damageStacks > 0) parts.Add($"공격 +{_damageStacks}");
                if (_speedStacks > 0) parts.Add($"이속 +{_speedStacks}");
                string cls = ClassWordOf(_class);
                if (cls != "") parts.Add(cls);
            }
            return string.Join(" · ", parts);
        }

        private static string JobWordOf(Match.MouseJobId _job)
        {
            switch (_job)
            {
                case Match.MouseJobId.Hero: return "용사쥐";
                case Match.MouseJobId.Gang: return "깡패 보스";
                case Match.MouseJobId.Minion: return "부하쥐";
                default: return "";
            }
        }

        private static string ClassWordOf(Player.CatClassId _class)
        {
            switch (_class)
            {
                case Player.CatClassId.Fire: return "불";
                case Player.CatClassId.Wind: return "바람";
                case Player.CatClassId.Light: return "빛";
                case Player.CatClassId.Dark: return "어둠";
                default: return "";
            }
        }
    }
}
