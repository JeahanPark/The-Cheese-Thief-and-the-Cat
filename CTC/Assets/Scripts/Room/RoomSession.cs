using System.Collections.Generic;
using CTC.Runtime.Core;
using R3;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 대기실의 **네트워크 상태만** 소유한다 (MVC의 Model) — 참가자·방 이름·맵·시작 여부.
    /// 스폰은 <see cref="RoomAvatarSpawner"/>, 시작 절차는 <see cref="RoomDirector"/>가 맡는다.
    ///
    /// 인게임의 <see cref="CTC.Runtime.Lobby.LobbySession"/>과 형제지만 **역할이 없다** —
    /// 대기실에서는 아무도 고양이가 아니고, 시작을 누르는 순간 서버가 하나를 뽑는다.
    ///
    /// 같은 in-scene NetworkObject에 Director·Spawner가 동승한다.
    /// </summary>
    public class RoomSession : NetworkBehaviour
    {
        private NetworkList<RoomMemberState> m_Members;

        private readonly NetworkVariable<FixedString64Bytes> m_RoomName =
            new NetworkVariable<FixedString64Bytes>();

        private readonly NetworkVariable<int> m_MapIndex = new NetworkVariable<int>();
        // 봇 자리(2026-09-01, 개발 빌드 전용 UI) — 혼자서도 판을 열어 보려고 둔다.
        // 전원이 읽는다: 참가자 보드가 "봇 3"을 같이 보여 줘야 몇 명으로 시작하는지 안다.
        private readonly NetworkVariable<int> m_BotCount = new NetworkVariable<int>();

        // 새 방의 기본 맵(2026-08-31) — 씬 빌더가 첫 정식 맵(대형) 인덱스를 굽는다.
        // 런타임에 카탈로그를 뒤지지 않는 이유: Model이 Worker를 모르게(MVC), 스폰 순서에도 안 걸리게.
        [SerializeField] private int m_DefaultMapIndex;

        /// <summary>시작을 눌러 추첨·씬 전환이 도는 중. 다시 못 누르게 잠근다.</summary>
        private readonly NetworkVariable<bool> m_Starting = new NetworkVariable<bool>();

        /// <summary>뽑힌 고양이. 추첨 전에는 의미 없다(<see cref="Starting"/>가 false).</summary>
        private readonly NetworkVariable<ulong> m_CatClientId = new NetworkVariable<ulong>();

        // 이 오브젝트는 씬에 미리 있고 서버가 시작할 때 스폰된다 — View는 그 전부터 살아 있으므로
        // "이제 다시 그려라"를 알려 줄 신호가 따로 필요하다(LobbySession과 같은 이유).
        private readonly Subject<Unit> m_SpawnChanged = new Subject<Unit>();

        public int MemberCount => m_Members != null ? m_Members.Count : 0;
        public RoomMemberState GetMember(int _index) => m_Members[_index];

        public string RoomName => m_RoomName.Value.ToString();
        public int MapIndex => m_MapIndex.Value;
        public bool Starting => m_Starting.Value;
        public ulong CatClientId => m_CatClientId.Value;

        /// <summary>내가 방장인가 — NGO에서 방장은 곧 서버다.</summary>
        public bool IsHost => NetworkManager != null && NetworkManager.IsServer;

        public bool CanStart => RoomBotLogic.CanStartWithBots(MemberCount, m_BotCount.Value, IsHost) && !Starting;

        /// <summary>
        /// 화면에 영향을 주는 모든 변화를 하나로 합친 신호 — 스폰·참가자·방 이름·맵·시작.
        /// View는 이것만 구독해 **바뀔 때만** 다시 그린다(규칙 3).
        /// </summary>
        public Observable<Unit> ViewChanged
        {
            get
            {
                // 목록이 아직 없다 = 구독이 Awake보다 빨랐다. 여기서 1회성 스트림을 내주면
                // 그게 곧바로 완료돼 **구독이 죽는다** — 반드시 Awake 뒤에 구독해야 한다.
                if (m_Members == null)
                {
                    GameDebug.LogError("[Room] Awake 전에 ViewChanged를 구독했다 — Start에서 구독해라");
                    return Observable.Return(Unit.Default);
                }

                return Observable.Merge(
                        m_SpawnChanged,
                        m_Members.AsChangedObservable(),
                        m_RoomName.AsObservable().AsUnitObservable(),
                        m_MapIndex.AsObservable().AsUnitObservable(),
                        m_BotCount.AsObservable().AsUnitObservable(),
                        m_Starting.AsObservable().AsUnitObservable())
                    .Prepend(Unit.Default);
            }
        }

        // 추첨 결과는 **RPC로 알린다.** NetworkVariable의 변경 이벤트에 기대면 두 군데서 새어 나간다:
        // (1) 뽑힌 사람이 clientId 0이면 기본값과 같아 **변경 자체가 안 일어난다**,
        // (2) 서버에서는 값을 넣는 그 줄에서 바로 콜백이 도는데 그때 시작 플래그가 아직 안 서 있다.
        // 둘 다 "결과가 나왔는데 연출이 안 도는" 형태로 조용히 깨진다(2026-08-30 실측).
        private readonly Subject<ulong> m_CatDrawn = new Subject<ulong>();

        /// <summary>추첨 결과가 전원에게 도착했다 — 인자는 고양이의 clientId.</summary>
        public Observable<ulong> CatDrawn => m_CatDrawn;

        private void Awake()
        {
            // NetworkList는 필드 초기화 대신 Awake에서 (NGO 권장 — 도메인 리로드 누수 방지)
            m_Members = new NetworkList<RoomMemberState>();
        }

        public override void OnNetworkSpawn()
        {
            m_SpawnChanged.OnNext(Unit.Default);

            if (IsServer)
            {
                m_MapIndex.Value = Mathf.Max(0, m_DefaultMapIndex);
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
                foreach (ulong id in NetworkManager.ConnectedClientsIds) EnsureEntry(id);
            }

            // 이름은 각자만 안다(스팀 이름은 로컬 API다) — 붙자마자 서버에 알린다.
            SubmitNameRpc(new FixedString32Bytes(Truncate(LocalDisplayName(), 28)));
        }

        public override void OnNetworkDespawn()
        {
            m_SpawnChanged.OnNext(Unit.Default);
            if (NetworkManager == null) return;

            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        public override void OnDestroy()
        {
            // 순수 C# 객체 — 규칙 9의 대상이 아니다
            m_SpawnChanged.Dispose();
            m_CatDrawn.Dispose();
            base.OnDestroy();
        }

        // ---- 서버 전용 쓰기 ----

        public void ServerSetRoomName(string _name)
        {
            if (!IsServer) return;
            m_RoomName.Value = new FixedString64Bytes(Truncate(_name, 60));
        }

        public int BotCount => m_BotCount.Value;

        /// <summary>봇 수 요청(방장만). 남은 자리에 맞춰 서버가 깎는다.</summary>
        public void SetBotCount(int _count)
        {
            SetBotCountRpc(_count);
        }

        [Rpc(SendTo.Server)]
        private void SetBotCountRpc(int _count, RpcParams _rpcParams = default)
        {
            if (m_Starting.Value) return;
            if (_rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;

            m_BotCount.Value = RoomBotLogic.ClampBots(_count, MemberCount);
        }

        public void ServerSetMapIndex(int _index)
        {
            if (!IsServer) return;
            m_MapIndex.Value = Mathf.Max(0, _index);
        }

        /// <summary>맵 선택 요청. **방장만** 통과한다 — 판정 권위는 서버다.</summary>
        public void SelectMap(int _index)
        {
            SelectMapRpc(_index);
        }

        [Rpc(SendTo.Server)]
        private void SelectMapRpc(int _index, RpcParams _rpcParams = default)
        {
            if (m_Starting.Value) return;

            // 방장 = 서버. 클라이언트가 눌러도 여기서 걸린다(화면에서도 못 누르지만 믿지 않는다).
            if (_rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;

            m_MapIndex.Value = Mathf.Max(0, _index);
        }

        /// <summary>추첨 결과를 확정하고 시작 잠금을 건다. 이미 도는 중이면 false.</summary>
        public bool ServerBeginStart(ulong _cat)
        {
            if (!IsServer || m_Starting.Value) return false;

            m_Starting.Value = true;
            m_CatClientId.Value = _cat;
            AnnounceCatRpc(_cat);
            return true;
        }

        [Rpc(SendTo.Everyone)]
        private void AnnounceCatRpc(ulong _cat) => m_CatDrawn.OnNext(_cat);

        /// <summary>지금 붙어 있는 사람들 — 추첨과 스폰이 같은 목록을 본다.</summary>
        public List<ulong> SnapshotClientIds()
        {
            var ids = new List<ulong>(MemberCount);
            for (int i = 0; i < MemberCount; i++) ids.Add(m_Members[i].ClientId);
            return ids;
        }

        public int IndexOf(ulong _clientId)
        {
            for (int i = 0; i < MemberCount; i++)
            {
                if (m_Members[i].ClientId == _clientId) return i;
            }
            return -1;
        }

        // ---- 참가자 목록 ----

        private void OnClientConnected(ulong _clientId) => EnsureEntry(_clientId);

        private void OnClientDisconnected(ulong _clientId)
        {
            int idx = IndexOf(_clientId);
            if (idx >= 0) m_Members.RemoveAt(idx);
            GameDebug.Log($"[Room] 나감 client={_clientId} → {MemberCount}명");
        }

        private void EnsureEntry(ulong _clientId)
        {
            if (IndexOf(_clientId) >= 0) return;

            m_Members.Add(new RoomMemberState { ClientId = _clientId, Name = default });
            GameDebug.Log($"[Room] 들어옴 client={_clientId} → {MemberCount}명");
        }

        // 문자열을 그대로 실어 보내지 않는다 — FixedString이 NGO가 확실히 나르는 형태다.
        [Rpc(SendTo.Server)]
        private void SubmitNameRpc(FixedString32Bytes _name, RpcParams _rpcParams = default)
        {
            ulong sender = _rpcParams.Receive.SenderClientId;

            // 이름이 접속 콜백보다 먼저 닿을 수 있다 — 그럴 때 조용히 버리면 이름표가 영영 빈다.
            EnsureEntry(sender);

            int idx = IndexOf(sender);
            if (idx < 0) return;

            RoomMemberState st = m_Members[idx];
            st.Name = _name;
            m_Members[idx] = st;
        }

        /// <summary>스팀 이름이 있으면 그것, 없으면 빈 값 — 화면이 자리번호로 대신 채운다.</summary>
        private static string LocalDisplayName()
        {
            try
            {
                return Networking.SteamBootstrap.Ready ? Steamworks.SteamClient.Name : "";
            }
            catch (System.Exception)
            {
                return "";
            }
        }

        // FixedString은 바이트 상한이라 한글이 섞이면 길이로는 못 잰다 — 넘치면 던지므로 미리 자른다.
        private static string Truncate(string _value, int _maxBytes)
        {
            if (string.IsNullOrEmpty(_value)) return "";

            var text = _value.Trim();
            while (System.Text.Encoding.UTF8.GetByteCount(text) > _maxBytes && text.Length > 0)
            {
                text = text.Substring(0, text.Length - 1);
            }
            return text;
        }
    }
}
