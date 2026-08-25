// WES에서 이식(2026-07-12) — 변경 3가지:
// 1) UniRx→R3: IObservable<T>→R3.Observable<T>, Subject<T>는 R3 것 사용(Dispose 패턴 동일).
// 2) Managers.Network(미이식 GameNetworkManager) 의존 제거 → NetworkManager.Singleton 직접 사용.
// 3) MonoSingleton→NetworkBehaviour 전환 — NGO의 [Rpc]는 NetworkBehaviour에서만 ILPP 처리되므로
//    씬/프리팹에 NetworkObject와 함께 배치해 스폰되어야 한다(자가 생성 싱글톤 불가). Instance는 정적 캐시.
using System;
using System.Collections.Generic;
using System.IO;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Managers
{
    /// <summary>
    /// 채팅 릴레이. UI는 `UI.ChatHud`가 맡고 여기는 네트워크만 본다.
    ///
    /// **2026-08-01 보강 4가지** (UI를 붙이기 전에 계약부터 고쳤다):
    /// 1. 발신자를 클라가 파라미터로 보내던 것 → `RpcParams.Receive.SenderClientId`.
    ///    전에는 아무 ID나 사칭할 수 있었다(같은 리포의 `CheerDirector`는 처음부터 옳게 했다).
    /// 2. 서버가 길이를 자른다 — `TMP_InputField.characterLimit`는 클라 UI일 뿐 방어가 아니다.
    /// 3. 도배 쿨다운(`CheerDirector`의 `m_NextAllowed` 패턴 그대로).
    /// 4. **관전자 메시지는 고양이에게 전송하지 않는다.** 탈락한 쥐가 "고양이 부엌에 있음"을 치면
    ///    그대로 고양이 화면에 떴다. 따봉과 같은 기준 — 가리는 게 아니라 안 보낸다.
    /// </summary>
    public class ChatManager : NetworkBehaviour
    {
        public const int MAX_MESSAGE_LENGTH = 120;
        private const double COOLDOWN_SECONDS = 0.7;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ---- /send log — 클라 로그를 호스트로 (2026-08-20, 개발 빌드 전용) ----
        public const int LOG_CHUNK_BYTES = 16 * 1024;      // NGO 메시지 한계 아래로 쪼갠다
        public const int LOG_TAIL_CAP_BYTES = 512 * 1024;  // 파일 끝 512KB — 에러는 끝에 있다
        private const int LOG_CHUNKS_PER_FRAME = 4;        // 전송 큐가 막히지 않게 프레임당 4조각
        private const int LOG_MAX_CHUNKS = 64;             // 서버 방어 상한(= 1MB)
        private const float LOG_COOLDOWN_SECONDS = 15f;    // 연사 방지 — 클라가 안내하고 막는다
        private const double LOG_RX_COOLDOWN_SECONDS = 10; // 서버 방어(클라보다 짧게 — 시계 오차로 정상 재전송을 안 막게)
#endif

        private static ChatManager s_Instance;

        public static ChatManager Instance => s_Instance;

        public Observable<ChatMessage> OnMessageReceivedAsObservable => m_OnMessageReceived;

        private readonly Subject<ChatMessage> m_OnMessageReceived = new Subject<ChatMessage>();
        private readonly Dictionary<ulong, double> m_NextAllowed = new Dictionary<ulong, double>();
        private readonly List<ulong> m_Receivers = new List<ulong>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private sealed class LogAssembly
        {
            public byte[][] Chunks;
            public int Received;
        }

        private readonly Dictionary<ulong, LogAssembly> m_LogRx = new Dictionary<ulong, LogAssembly>(); // 서버 전용
        private readonly Dictionary<ulong, double> m_LogRxNextAllowed = new Dictionary<ulong, double>(); // 서버 전용
        private bool m_LogSending;      // 클라 쪽 재진입 가드 — 전송 도중 또 치면 무시
        private float m_LogNextSendAt;  // 클라 쪽 쿨다운 — 연사로 판마다 512KB씩 쏟아붓지 않게
#endif

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                GameDebug.LogWarning("[ChatManager] Duplicate instance destroyed");
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
        }

        public override void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
            m_OnMessageReceived?.Dispose();
            base.OnDestroy();
        }

        /// <summary>로컬에서 한 줄 보낸다(ChatHud가 부른다). 판정은 전부 서버.</summary>
        public void SendChatMessage(string _message)
        {
            if (string.IsNullOrWhiteSpace(_message)) return;
            if (!IsSpawned) return; // 스폰 전 호출은 NGO가 예외를 던진다

            SendChatMessageRpc(Trim(_message));
        }

        private static string Trim(string _message)
        {
            string trimmed = _message.Trim();
            return trimmed.Length <= MAX_MESSAGE_LENGTH ? trimmed : trimmed.Substring(0, MAX_MESSAGE_LENGTH);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SendChatMessageRpc(string _message, RpcParams _rpcParams = default)
        {
            ulong sender = _rpcParams.Receive.SenderClientId;

            string text = Trim(_message);
            if (string.IsNullOrEmpty(text)) return;

            double now = NetworkManager.ServerTime.Time;
            if (m_NextAllowed.TryGetValue(sender, out double next) && now < next) return;
            m_NextAllowed[sender] = now + COOLDOWN_SECONDS;

            // 살아 있으면 전원에게, 관전자면 고양이를 뺀 전원에게
            bool senderAlive = NetworkManager.SpawnManager.GetPlayerNetworkObject(sender) != null;
            if (senderAlive)
            {
                BroadcastChatMessageRpc(text, sender, false);
                return;
            }

            BuildNonCatReceivers();
            if (m_Receivers.Count == 0) return;
            BroadcastChatMessageRpc(text, sender, true, RpcTarget.Group(m_Receivers, RpcTargetUse.Temp));
        }

        // 고양이를 제외한 전원(살아 있는 쥐 + 탈락자). CheerDirector.BuildReceivers와 같은 판단.
        private void BuildNonCatReceivers()
        {
            m_Receivers.Clear();
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
            {
                NetworkObject player = NetworkManager.SpawnManager.GetPlayerNetworkObject(id);
                if (player != null)
                {
                    RoleTag tag = player.GetComponent<RoleTag>();
                    if (tag != null && tag.Role == RoleId.Cat) continue;
                }
                m_Receivers.Add(id);
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void BroadcastChatMessageRpc(
            string _message, ulong _senderId, bool _fromSpectator, RpcParams _rpcParams = default)
        {
            var chatMessage = new ChatMessage
            {
                SenderId = _senderId,
                Message = _message,
                FromSpectator = _fromSpectator,
                Timestamp = DateTime.Now,
            };

            m_OnMessageReceived.OnNext(chatMessage);
            GameDebug.Log($"[ChatManager] {_senderId}{(_fromSpectator ? "(관전)" : "")}: {_message}");
        }

        // SendTo.SpecifiedInParams 오버로드 — 살아 있는 발신자는 전원에게.
        private void BroadcastChatMessageRpc(string _message, ulong _senderId, bool _fromSpectator)
        {
            BroadcastChatMessageRpc(_message, _senderId, _fromSpectator, RpcTarget.Everyone);
        }

        // ==== /send log — 테스터의 Player.log를 채팅 한 줄로 수거한다 (2026-08-20) ====
        //
        // 왜 채팅 명령인가: 클라 에러 로그를 복사해 메신저로 나르는 품이 커서, 실기기 2인
        // 테스트가 "로그 좀 보내줘 → 기다림" 루프가 됐다. 파일 끝 512KB만 16KB 조각으로
        // 서버에 보내고, 호스트가 persistentDataPath/ClientLogs/에 저장한다.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>ChatHud가 부른다 — 내 Player.log 꼬리를 호스트로 보낸다.</summary>
        public void SendMyLogToHost()
        {
            SendMyLogToHostAsync().Forget();
        }

        private async UniTaskVoid SendMyLogToHostAsync()
        {
            if (!IsSpawned) return;

            // 진행 상황은 채팅 줄로 보인다 — 시작·거부는 내 화면에만(로컬), 도착은 호스트가 방송.
            if (m_LogSending) { ShowLocalLine("[로그] 이미 보내는 중이다"); return; }
            float now = Time.realtimeSinceStartup;
            if (now < m_LogNextSendAt)
            {
                ShowLocalLine($"[로그] 잠깐 — {Mathf.CeilToInt(m_LogNextSendAt - now)}초 뒤에 다시");
                return;
            }

            m_LogSending = true;
            try
            {
                // consoleLogPath: 플레이어 빌드 = Player.log, 에디터 = Editor.log
                byte[] data = ReadLogTail(Application.consoleLogPath, LOG_TAIL_CAP_BYTES);
                if (data == null || data.Length == 0)
                {
                    ShowLocalLine("[로그] 보낼 로그가 없다");
                    return;
                }

                m_LogNextSendAt = now + LOG_COOLDOWN_SECONDS;

                int total = ChatCommandLogic.ChunkCount(data.Length, LOG_CHUNK_BYTES);
                ShowLocalLine($"[로그] 보내는 중… {data.Length / 1024}KB ({total}조각)");
                for (int i = 0; i < total; i++)
                {
                    int offset = i * LOG_CHUNK_BYTES;
                    int length = Math.Min(LOG_CHUNK_BYTES, data.Length - offset);
                    byte[] chunk = new byte[length];
                    Buffer.BlockCopy(data, offset, chunk, 0, length);
                    SendLogChunkRpc(i, total, chunk);

                    // 한 프레임에 다 밀면 전송 큐가 넘친다 — 나눠 흘린다
                    if ((i + 1) % LOG_CHUNKS_PER_FRAME == 0)
                    {
                        await UniTask.Yield(this.GetCancellationTokenOnDestroy());
                    }
                }
            }
            finally
            {
                m_LogSending = false;
            }
        }

        // 내 화면에만 찍는 안내 줄 — 네트워크를 타지 않는다(진행 상황은 남에게 정보가 아니다)
        private void ShowLocalLine(string _text)
        {
            m_OnMessageReceived.OnNext(new ChatMessage
            {
                SenderId = NetworkManager != null ? NetworkManager.LocalClientId : 0,
                Message = _text,
                FromSpectator = false,
                Timestamp = DateTime.Now,
            });
        }

        // 게임이 로그 파일을 쥔 채 돌고 있으므로 공유 읽기로 연다
        private static byte[] ReadLogTail(string _path, int _capBytes)
        {
            try
            {
                using (var fs = new FileStream(_path, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = ChatCommandLogic.TailStart(fs.Length, _capBytes);
                    fs.Seek(start, SeekOrigin.Begin);
                    byte[] buffer = new byte[fs.Length - start];
                    int read = 0;
                    while (read < buffer.Length)
                    {
                        int n = fs.Read(buffer, read, buffer.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    if (read < buffer.Length) Array.Resize(ref buffer, read);
                    return buffer;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChatManager] 로그 읽기 실패: {e.Message}");
                return null;
            }
        }
#endif

        // RPC 껍데기는 배포에도 남긴다 — 메서드를 통째로 빼면 개발↔배포가 섞인 판에서
        // RPC 테이블이 어긋난다(MultiplayerQaProbe의 missing script와 같은 교훈). 본문만 비운다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SendLogChunkRpc(int _index, int _total, byte[] _data, RpcParams _rpcParams = default)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 방어부터 — 개수·크기 상한 밖이면 조립을 시작하지 않는다
            if (_total <= 0 || _total > LOG_MAX_CHUNKS || _index < 0 || _index >= _total) return;
            if (_data == null || _data.Length == 0 || _data.Length > LOG_CHUNK_BYTES) return;

            ulong sender = _rpcParams.Receive.SenderClientId;

            // 새 전송은 반드시 0번 조각으로 시작한다(RPC는 발신자별 순서 보장). 그때마다
            // 조립을 새로 판다 — 끊긴 반쪽 위에 새 조각을 덮어 섞인 파일이 나오지 않게.
            if (_index == 0)
            {
                double serverNow = NetworkManager.ServerTime.Time;
                if (m_LogRxNextAllowed.TryGetValue(sender, out double next) && serverNow < next)
                {
                    m_LogRx.Remove(sender); // 쿨다운 무시 클라(조작) — 조용히 버린다
                    return;
                }
                m_LogRxNextAllowed[sender] = serverNow + LOG_RX_COOLDOWN_SECONDS;
                m_LogRx[sender] = new LogAssembly { Chunks = new byte[_total][], Received = 0 };
            }
            if (!m_LogRx.TryGetValue(sender, out LogAssembly asm) || asm.Chunks.Length != _total)
            {
                return; // 0번 없이 날아든 조각 — 시작이 없었으니 받지 않는다
            }
            if (asm.Chunks[_index] == null) asm.Received++;
            asm.Chunks[_index] = _data;
            if (asm.Received < _total) return;

            m_LogRx.Remove(sender);
            SaveClientLog(sender, asm.Chunks);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void SaveClientLog(ulong _sender, byte[][] _chunks)
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "ClientLogs");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"client{_sender}_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                using (var fs = new FileStream(file, FileMode.Create))
                {
                    foreach (byte[] chunk in _chunks) fs.Write(chunk, 0, chunk.Length);
                }

                long kb = Math.Max(1, new FileInfo(file).Length / 1024);
                // 채팅으로 알린다 — 보낸 쪽은 "갔다"를, 호스트는 "어디 저장됐다"를 같은 줄로 본다
                BroadcastChatMessageRpc($"[로그] 도착 {kb}KB → ClientLogs/{Path.GetFileName(file)}", _sender, false);
                GameDebug.Log($"[ChatManager] 클라{_sender} 로그 저장: {file}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ChatManager] 로그 저장 실패: {e}");
            }
        }
#endif
    }

    public struct ChatMessage
    {
        // WES 원본은 public 필드 — CTC 규칙(public 필드 금지)에 따라 auto-property로 전환.
        public ulong SenderId { get; set; }
        public string Message { get; set; }
        /// <summary>관전자(탈락한 쥐)가 보낸 줄 — 고양이에게는 전송되지 않는다.</summary>
        public bool FromSpectator { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
