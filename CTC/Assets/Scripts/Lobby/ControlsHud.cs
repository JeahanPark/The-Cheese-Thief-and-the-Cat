using System.Collections.Generic;
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
        // 한글 표시는 Pretendard 동적 폰트(Tools/CTC/Create Korean Font Asset) 전제
        private const string MOUSE_CONTROLS =
            "WASD 이동   마우스 시점   좌클릭 발사   LShift 대시 (구매 후)";
        private const string CAT_CONTROLS =
            "WASD 이동   마우스 시점   좌클릭 덮치기   <b>LCtrl 살금살금</b>(무음·속도 절반)";
        private const string SPECTATOR_CONTROLS =
            "관전 중 — <b>G</b> 보고 있는 쥐에게 따봉 (고양이에겐 보이지 않는다)";

        private static readonly string[] ITEM_LABELS =
        {
            "새총", "기관총", "대시", "미끼", "요새", "창고", "공격 강화", "이속 강화",
        };

        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private RoleId m_CachedRole = RoleId.None;
        private bool m_HasCache; // None도 유효한 상태(관전자)라 값 비교만으로는 초기화를 못 가린다
        private string m_CachedText = "";

        private void Update()
        {
            bool show = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            RoleTag tag = null;
            if (show)
            {
                NetworkManager nm = NetworkManager.Singleton;
                NetworkObject local = nm != null && nm.SpawnManager != null
                    ? nm.SpawnManager.GetLocalPlayerObject()
                    : null;
                tag = local != null ? local.GetComponent<RoleTag>() : null;
            }

            if (m_Text.gameObject.activeSelf != show)
            {
                m_Text.gameObject.SetActive(show);
            }
            if (!show) return;

            // 역할이 바뀔 때만 문자열을 만든다 — 매 프레임 조립할 이유가 없다
            // 탈락하면 RoleId.None으로 돌아가며 관전자 안내로 바뀐다
            RoleId role = tag != null ? tag.Role : RoleId.None;
            if (role != m_CachedRole || !m_HasCache)
            {
                m_CachedRole = role;
                m_HasCache = true;
                m_CachedText = BuildText(role);
            }
            m_Text.text = m_CachedText;
        }

        private static string BuildText(RoleId _role)
        {
            if (_role == RoleId.None) return SPECTATOR_CONTROLS;

            var sb = new StringBuilder();
            sb.AppendLine(_role == RoleId.Cat ? CAT_CONTROLS : MOUSE_CONTROLS);

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
