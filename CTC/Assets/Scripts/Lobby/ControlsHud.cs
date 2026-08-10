using System.Collections.Generic;
using CTC.Runtime.Core;
using R3;
using System.Text;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 좌하단 안내. 게임 중에만 표시하고 로컬 플레이어 역할에 맞춰
    /// 조작키 + **핫바 항목이 무엇인지**를 보여준다.
    ///
    /// 핫바에는 이름과 가격만 나와서 "창고가 뭐 하는 건지" 알 수가 없었다.
    /// 설명 문구는 ShopLogic.DescriptionOf가 수치와 함께 들고 있다 — 밸런스를 바꾸면 안내도 같이 바뀐다.
    /// </summary>
    public class ControlsHud : MonoBehaviour
    {
        // 한글 표시는 Pretendard 동적 폰트(Tools/CTC/Create Korean Font Asset) 전제.
        // **"무음"이라고 쓰면 안 된다**(2026-08-10) — 잠행은 발소리를 없애는 게 아니라
        // 아주 좁게 줄인다. 안내가 거짓이면 플레이어는 안 들킬 줄 알고 서 있다가 잡힌다.
        private const string MOUSE_CONTROLS =
            "WASD 이동   마우스 시점   좌클릭 발사   <b>LCtrl 살금살금</b>(발소리 4m·느림)   LShift 대시 (구매 후)";
        private const string CAT_CONTROLS =
            "WASD 이동   마우스 시점   좌클릭 덮치기   <b>LCtrl 살금살금</b>(발소리 3m·느림)";
        // 깡패쥐에게만 붙는다 — 임명은 깡패만 쓰는 조작이라 전원에게 보이면 소음이다
        private const string GANG_CONTROLS = "   <b>F</b> 다가가서 부하 임명";
        private const string SPECTATOR_CONTROLS =
            "관전 중 — <b>G</b> 보고 있는 쥐에게 따봉 (고양이에겐 보이지 않는다)";

        private static readonly string[] ITEM_LABELS =
        {
            "새총", "기관총", "대시", "미끼", "요새", "창고", "공격 강화", "이속 강화",
        };

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private RoleId m_CachedRole = RoleId.None;
        private bool m_HasCache; // None도 유효한 상태(관전자)라 값 비교만으로는 초기화를 못 가린다
        private string m_CachedText = "";

        // 직업은 판 중간에 바뀐다(상자·임명) — 로컬 플레이어가 바뀔 때만 그리면 F키 안내가 안 뜬다
        private readonly SerialDisposable m_JobSub = new SerialDisposable();
        private Match.MouseJobId m_CachedJob = Match.MouseJobId.None;

        // 개편 3b(2026-07-31): Update 폴링 → 로컬 플레이어 스트림 구독.
        // 역할이 바뀔 때(= 스폰/탈락)만 문자열을 다시 만든다.
        private void Start()
        {
            m_Text.gameObject.SetActive(false);
            m_JobSub.AddTo(this);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnLocalPlayerChanged(Unity.Netcode.NetworkObject _player)
        {
            var job = _player != null ? _player.GetComponent<Match.MouseJob>() : null;
            m_JobSub.Disposable = job != null ? job.JobChanged.Subscribe(OnJobChanged) : null;
            if (job == null) m_CachedJob = Match.MouseJobId.None;

            Redraw(_player);
        }

        private void OnJobChanged(Match.MouseJobId _job)
        {
            if (_job == m_CachedJob) return;
            m_CachedJob = _job;
            m_HasCache = false;   // 문자열을 다시 만든다
            Redraw(LocalPlayerBinder.LocalPlayer.CurrentValue);
        }

        private void Redraw(Unity.Netcode.NetworkObject _player)
        {
            bool started = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            RoleTag tag = _player != null ? _player.GetComponent<RoleTag>() : null;

            // 매치 전에는 아예 숨긴다 — 로비에는 조작 안내가 필요 없다
            if (!started && tag == null)
            {
                m_Text.gameObject.SetActive(false);
                return;
            }

            m_Text.gameObject.SetActive(true);
            RoleId role = tag != null ? tag.Role : RoleId.None; // None = 관전자
            if (role != m_CachedRole || !m_HasCache)
            {
                m_CachedRole = role;
                m_HasCache = true;
                m_CachedText = BuildText(role, m_CachedJob);
            }
            m_Text.text = m_CachedText;
        }

        private static string BuildText(RoleId _role, Match.MouseJobId _job)
        {
            if (_role == RoleId.None) return SPECTATOR_CONTROLS;

            var sb = new StringBuilder();
            string controls = _role == RoleId.Cat ? CAT_CONTROLS : MOUSE_CONTROLS;
            if (_job == Match.MouseJobId.Gang) controls += GANG_CONTROLS;
            sb.AppendLine(controls);

            List<int> indices = ShopLogic.RoleCatalogIndices(_role);
            for (int slot = 0; slot < indices.Count; slot++)
            {
                ShopItem item = ShopLogic.CATALOG[indices[slot]];
                sb.AppendLine($"<b>{slot + 1} {ITEM_LABELS[indices[slot]]}</b> <color=#FFD84D>{item.Cost}</color>  " +
                              $"<color=#B9C2D0>{ShopLogic.DescriptionOf(item.Id)}</color>");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
