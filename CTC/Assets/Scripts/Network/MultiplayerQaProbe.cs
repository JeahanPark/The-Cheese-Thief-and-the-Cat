#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;

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

        public static string ProbeDir => Path.Combine(Path.GetTempPath(), PROBE_DIR_NAME);

        [SerializeField] private float m_SnapshotIntervalSeconds = 0.5f;

        private float m_Timer;
        private string m_FilePath;
        private bool m_IsMain;

        private void Start()
        {
            m_IsMain = Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor;

            Directory.CreateDirectory(ProbeDir);
            m_FilePath = Path.Combine(ProbeDir, m_IsMain ? HOST_FILE : CLONE_FILE);
            Debug.Log($"[MultiplayerQaProbe] role={(m_IsMain ? "host" : "clone")} probeFile={m_FilePath}");

            WriteSnapshot();
        }

        private void Update()
        {
            m_Timer += Time.unscaledDeltaTime;
            if (m_Timer < m_SnapshotIntervalSeconds) return;

            m_Timer = 0f;
            WriteSnapshot();
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

            if (nm != null && nm.SpawnManager != null)
            {
                foreach (var kv in nm.SpawnManager.SpawnedObjects)
                {
                    NetworkObject no = kv.Value;
                    if (no == null) continue;

                    Vector3 p = no.transform.position;
                    s.objects.Add(new ProbeObject
                    {
                        id = (long)no.NetworkObjectId,
                        ownerClientId = (long)no.OwnerClientId,
                        isLocalOwner = no.IsOwner,
                        posX = p.x,
                        posY = p.y,
                        posZ = p.z
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
        public float timestamp;
        public List<ProbeObject> objects;
    }

    [System.Serializable]
    public class ProbeObject
    {
        public long id;
        public long ownerClientId;
        public bool isLocalOwner;
        public float posX;
        public float posY;
        public float posZ;
    }
}
#endif
