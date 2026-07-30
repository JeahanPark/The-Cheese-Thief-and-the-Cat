#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;

using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// MPPM QA 프로브: 자기 인스턴스(메인=host / 클론=clone)의 네트워크 상태를
    /// 주기적으로 시스템 temp 하위 공유 파일에 기록한다. (WES MultiplayerQaProbe 이식·단순화)
    /// 클론 에디터는 McpBridge 파이프를 열지 않으므로 파일 경유가 클론 상태 수집의 유일 경로.
    /// 시스템 temp를 쓰는 이유: 클론은 작업 디렉터리가 달라 상대경로(Temp/..)면 수집이 깨진다.
    /// </summary>
    public class MultiplayerQaProbe : MonoBehaviour
    {
        public const string PROBE_DIR_NAME = "ctc-mppm-qa";
        public const string HOST_FILE = "probe_host.json";
        public const string CLONE_FILE = "probe_clone.json";
        public const string CLONE_CMD_FILE = "cmd_clone.json";

        public static string ProbeDir => Path.Combine(Path.GetTempPath(), PROBE_DIR_NAME);

        [SerializeField] private float m_SnapshotIntervalSeconds = 0.5f;

        private float m_Timer;
        private string m_FilePath;
        private string m_CmdFilePath;
        private bool m_IsMain;
        private int m_LastCmdSeq = -1;

        private void Start()
        {
            m_IsMain = Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor;

            Directory.CreateDirectory(ProbeDir);
            m_FilePath = Path.Combine(ProbeDir, m_IsMain ? HOST_FILE : CLONE_FILE);
            m_CmdFilePath = Path.Combine(ProbeDir, CLONE_CMD_FILE);
            Debug.Log($"[MultiplayerQaProbe] role={(m_IsMain ? "host" : "clone")} probeFile={m_FilePath}");

            WriteSnapshot();
        }

        private void Update()
        {
            // 클론 한정 역방향 커맨드 채널(파이프가 없어 파일 경유가 유일 경로).
            if (!m_IsMain) PollCloneCommand();

            m_Timer += Time.unscaledDeltaTime;
            if (m_Timer < m_SnapshotIntervalSeconds) return;

            m_Timer = 0f;
            WriteSnapshot();
        }

        // cmd_clone.json { seq, method } 폴링. 새 seq만 1회 실행, TestMp* 화이트리스트.
        private void PollCloneCommand()
        {
            try
            {
                if (!File.Exists(m_CmdFilePath)) return;

                string json = File.ReadAllText(m_CmdFilePath);
                if (string.IsNullOrWhiteSpace(json)) return;

                CloneCommand cmd = JsonUtility.FromJson<CloneCommand>(json);
                if (cmd == null || cmd.seq <= m_LastCmdSeq) return;

                m_LastCmdSeq = cmd.seq;
                ExecuteCloneCommand(cmd.method);
            }
            catch (IOException)
            {
                // QA가 쓰는 중일 수 있음. 다음 주기 재시도.
            }
        }

        private void ExecuteCloneCommand(string method)
        {
            if (string.IsNullOrEmpty(method) || !method.StartsWith("TestMp"))
            {
                Debug.LogWarning($"[MultiplayerQaProbe] 클론 커맨드 거부(화이트리스트 위반): '{method}'");
                return;
            }

            switch (method)
            {
                case "TestMpNudgeLocalPlayer":
                    NudgeLocalPlayer();
                    break;
                case "TestMpSelectRoleMouse":
                    SelectRole(RoleId.Mouse);
                    break;
                case "TestMpSelectRoleCat":
                    SelectRole(RoleId.Cat);
                    break;
                case "TestMpShootAtCat":
                    ShootAtCat();
                    break;
                case "TestMpBuySlingshot":
                    BuyLocal(ShopItemId.Slingshot);
                    break;
                case "TestMpBuyDash":
                    BuyLocal(ShopItemId.Dash);
                    break;
                case "TestMpBuyDecoy":
                    BuyLocal(ShopItemId.Decoy);
                    break;
                case "TestMpBuyFortress":
                    BuyLocal(ShopItemId.Fortress);
                    break;
                case "TestMpEnterNearestFortress":
                    EnterNearestFortress();
                    break;
                default:
                    Debug.LogWarning($"[MultiplayerQaProbe] 알 수 없는 클론 커맨드: '{method}'");
                    break;
            }
        }

        // 자기 소유 플레이어를 (+2, 0, +3) 이동 — 클론→호스트 동기화 검증용.
        private void NudgeLocalPlayer()
        {
            NetworkObject local = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject()
                : null;
            if (local == null)
            {
                Debug.LogWarning("[MultiplayerQaProbe] NudgeLocalPlayer — 로컬 플레이어 없음");
                return;
            }

            // transform 직접 세팅은 CharacterController 내부 위치와 충돌해 되돌아간다 — CC.Move 사용.
            var cc = local.GetComponent<CharacterController>();
            if (cc != null) cc.Move(new Vector3(2f, 0f, 3f));
            else local.transform.position += new Vector3(2f, 0f, 3f);
            Debug.Log($"[MultiplayerQaProbe] TestMpNudgeLocalPlayer 실행 → {local.transform.position}");
        }

        // 클론 로컬에서 로비 역할 선택 — MPPM 로비 자동화용.
        private void SelectRole(RoleId role)
        {
            var lobby = FindFirstObjectByType<LobbyController>();
            if (lobby == null || !lobby.IsSpawned)
            {
                Debug.LogWarning("[MultiplayerQaProbe] SelectRole — LobbyController 없음/미스폰");
                return;
            }
            lobby.SelectRole(role);
            Debug.Log($"[MultiplayerQaProbe] SelectRole({role}) 요청");
        }

        // 클론 쥐가 가장 가까운 고양이에게 딱총 발사 — 전투 QA용.
        private void ShootAtCat()
        {
            NetworkObject local = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject()
                : null;
            var gun = local != null ? local.GetComponent<MousePopgun>() : null;
            if (gun == null)
            {
                Debug.LogWarning("[MultiplayerQaProbe] ShootAtCat — MousePopgun 없음");
                return;
            }
            gun.DevShootAtNearestCat();
        }

        // 클론 로컬 플레이어의 상점 구매 — 상점 QA용.
        private void BuyLocal(ShopItemId itemId)
        {
            NetworkObject local = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject()
                : null;
            var shop = local != null ? local.GetComponent<ShopController>() : null;
            if (shop == null)
            {
                Debug.LogWarning("[MultiplayerQaProbe] BuyLocal — ShopController 없음");
                return;
            }
            shop.Buy(itemId);
        }

        // 클론 쥐를 가장 가까운 요새 중심으로 순간이동(CC 토글) — 내부 회복/차폐 QA용.
        private void EnterNearestFortress()
        {
            NetworkObject local = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject()
                : null;
            if (local == null)
            {
                Debug.LogWarning("[MultiplayerQaProbe] EnterNearestFortress — 로컬 플레이어 없음");
                return;
            }

            FortressHealZone nearest = null;
            float best = float.MaxValue;
            foreach (var zone in FindObjectsByType<FortressHealZone>(FindObjectsSortMode.None))
            {
                float d = (zone.transform.position - local.transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = zone; }
            }
            if (nearest == null)
            {
                Debug.LogWarning("[MultiplayerQaProbe] EnterNearestFortress — 요새 없음");
                return;
            }

            var cc = local.GetComponent<CharacterController>();
            Vector3 pos = nearest.transform.position + Vector3.up * 0.1f;
            if (cc != null) cc.enabled = false;
            local.transform.position = pos;
            if (cc != null) cc.enabled = true;
            Debug.Log($"[MultiplayerQaProbe] EnterNearestFortress → {pos}");
        }

        private void OnDestroy()
        {
            try
            {
                if (!string.IsNullOrEmpty(m_FilePath) && File.Exists(m_FilePath))
                    File.Delete(m_FilePath);
            }
            catch (IOException) { }
        }

        private void WriteSnapshot()
        {
            string json = JsonUtility.ToJson(BuildSnapshot());

            // temp→rename 원자 쓰기: 수집 측이 부분 쓰기 파일을 읽지 않도록 보장.
            string tempPath = m_FilePath + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json);
                if (File.Exists(m_FilePath)) File.Delete(m_FilePath);
                File.Move(tempPath, m_FilePath);
            }
            catch (IOException)
            {
                // 다음 주기에 재시도.
            }
        }

        private ProbeSnapshot BuildSnapshot()
        {
            var s = new ProbeSnapshot
            {
                role = m_IsMain ? "host" : "clone",
                isMainEditor = m_IsMain,
                timestamp = Time.realtimeSinceStartup,
                objects = new List<ProbeObject>()
            };

            NetworkManager nm = NetworkManager.Singleton;
            s.connected = nm != null && nm.IsListening && (nm.IsClient || nm.IsServer);
            s.localClientId = nm != null ? (long)nm.LocalClientId : -1;
            s.connectedClientCount = nm != null && nm.IsServer ? nm.ConnectedClients.Count : -1;

            MatchManager match = FindFirstObjectByType<MatchManager>();
            s.matchResult = match != null && match.IsSpawned ? (int)match.Result : -1;
            s.matchRemaining = match != null && match.IsSpawned ? Mathf.CeilToInt(match.RemainingSeconds) : -1;

            // 종료(teardown) 중엔 SpawnManager 내부가 해체되며 NRE 스팸 — 접속 상태에서만 순회
            if (s.connected && nm.SpawnManager != null)
            {
                foreach (var kv in nm.SpawnManager.SpawnedObjects)
                {
                    NetworkObject no = kv.Value;
                    if (no == null) continue;

                    Vector3 p = no.transform.position;
                    CheeseWallet wallet = no.GetComponent<CheeseWallet>();
                    Health health = no.GetComponent<Health>();
                    MousePopgun popgun = no.GetComponent<MousePopgun>();
                    s.objects.Add(new ProbeObject
                    {
                        id = (long)no.NetworkObjectId,
                        objectName = no.gameObject.name,
                        ownerClientId = (long)no.OwnerClientId,
                        isLocalOwner = no.IsOwner,
                        posX = p.x,
                        posY = p.y,
                        posZ = p.z,
                        cheese = wallet != null ? wallet.CheeseValue : -1,
                        hp = health != null ? health.Hp : -1,
                        weaponTier = popgun != null ? popgun.Tier : -1
                    });
                }
            }

            return s;
        }
    }

    [System.Serializable]
    public class ProbeSnapshot
    {
        public string role;
        public bool isMainEditor;
        public long localClientId;
        public bool connected;
        public int connectedClientCount;
        public int matchResult;
        public int matchRemaining;
        public float timestamp;
        public List<ProbeObject> objects;
    }

    // 클론 역방향 커맨드 DTO. QA가 cmd_clone.json에 기록 → 클론 프로브가 폴링.
    [System.Serializable]
    public class CloneCommand
    {
        public int seq;
        public string method;
    }

    [System.Serializable]
    public class ProbeObject
    {
        public long id;
        public string objectName;
        public long ownerClientId;
        public bool isLocalOwner;
        public float posX;
        public float posY;
        public float posZ;
        public int cheese;
        public int hp;
        public int weaponTier;
    }
}
#endif
