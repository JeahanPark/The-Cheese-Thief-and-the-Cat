using System;
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
    /// 내 HP 바 + 고양이 HP 바(쥐 전원의 공동 목표 게이지) + 재화 + 상시 배지 한 줄.
    ///
    /// 배지 줄은 "내가 지금 가진 것"의 단어 나열이다(쥐: 무기 티어·대시·직업 / 고양이: 강화
    /// 스택·전직). 문장 설명은 구매 토스트(3초)와 MouseJobHud 팝업(3초)의 몫 — "상시는 얇게".
    ///
    /// 관전자에게는 고양이 바만 남긴다 — 판의 공동 목표는 죽어서도 보여야 관전이 읽힌다.
    /// </summary>
    public class StatusHud : MonoBehaviour
    {
        private const float CAT_BIND_RETRY_SECONDS = 0.5f;
        private const int CAT_BIND_RETRY_COUNT = 20;

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private GameObject m_Root;
        [SerializeField] private GameObject m_MyBar;
        [SerializeField] private Image m_MyHpFill;
        [SerializeField] private TMP_Text m_MyHpText;
        [SerializeField] private GameObject m_CatBar;   // 고양이 화면에선 숨김 — 제 HP 바와 같은 값이다
        [SerializeField] private Image m_CatHpFill;
        [SerializeField] private TMP_Text m_CatHpText;
        [SerializeField] private TMP_Text m_WalletText;
        [SerializeField] private TMP_Text m_BadgeText;

        private readonly SerialDisposable m_HpSub = new SerialDisposable();
        private readonly SerialDisposable m_WalletSub = new SerialDisposable();
        private readonly SerialDisposable m_CatSub = new SerialDisposable();
        private readonly SerialDisposable m_CatRetry = new SerialDisposable();
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
            m_CatSub.AddTo(this);
            m_CatRetry.AddTo(this);
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
            if (_started) RetryBindCat();
            else
            {
                m_CatSub.Disposable = null;
                m_CatRetry.Disposable = null;
            }
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

        private void ApplyActive()
        {
            bool show = m_GameStarted;
            if (m_Root.activeSelf != show) m_Root.SetActive(show);
            if (!show) return;

            m_MyBar.SetActive(m_HasPlayer);
            m_WalletText.gameObject.SetActive(m_HasPlayer);
            m_BadgeText.gameObject.SetActive(m_HasPlayer);
            m_CatBar.SetActive(m_Role != RoleId.Cat); // 쥐·관전자에게만 — 공동 목표 게이지
        }

        private void DrawMyHp(int _hp, int _maxHp)
        {
            m_MyHpText.text = $"HP {_hp}/{_maxHp}";
            SetFill(m_MyHpFill, _maxHp > 0 ? _hp / (float)_maxHp : 0f);
        }

        // 시작 신호가 스폰보다 먼저 오는 경우가 있어(CombatHud 실측) 잡힐 때까지 재시도한다.
        // 폴링으로 되돌아간 게 아니라 **찾을 때까지만** 도는 한시적 스트림이다.
        private void RetryBindCat()
        {
            if (BindCat()) return;

            m_CatRetry.Disposable = Observable
                .Interval(TimeSpan.FromSeconds(CAT_BIND_RETRY_SECONDS), UnityTimeProvider.Update)
                .Take(CAT_BIND_RETRY_COUNT)
                .Subscribe(_ =>
                {
                    if (BindCat()) m_CatRetry.Disposable = null;
                });
        }

        // 고양이는 매치당 한 마리 — 찾으면 붙이고 true를 돌려준다
        private bool BindCat()
        {
            foreach (Health h in FindObjectsByType<Health>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                RoleTag tag = h.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Cat) continue;

                m_CatSub.Disposable = h.HpChangedRx.Subscribe(hp =>
                {
                    m_CatHpText.text = $"고양이 {hp}/{h.MaxHp}";
                    SetFill(m_CatHpFill, h.MaxHp > 0 ? hp / (float)h.MaxHp : 0f);
                });
                return true;
            }
            m_CatHpText.text = "고양이 -";
            SetFill(m_CatHpFill, 0f);
            return false;
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
