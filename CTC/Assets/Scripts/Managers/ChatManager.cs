// WES에서 이식(2026-07-12) — 변경 3가지:
// 1) UniRx→R3: IObservable<T>→R3.Observable<T>, Subject<T>는 R3 것 사용(Dispose 패턴 동일).
// 2) Managers.Network(미이식 GameNetworkManager) 의존 제거 → NetworkManager.Singleton 직접 사용.
// 3) MonoSingleton→NetworkBehaviour 전환 — NGO의 [Rpc]는 NetworkBehaviour에서만 ILPP 처리되므로
//    씬/프리팹에 NetworkObject와 함께 배치해 스폰되어야 한다(자가 생성 싱글톤 불가). Instance는 정적 캐시.
using System;
using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
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

        private static ChatManager s_Instance;

        public static ChatManager Instance => s_Instance;

        public Observable<ChatMessage> OnMessageReceivedAsObservable => m_OnMessageReceived;

        private readonly Subject<ChatMessage> m_OnMessageReceived = new Subject<ChatMessage>();
        private readonly Dictionary<ulong, double> m_NextAllowed = new Dictionary<ulong, double>();
        private readonly List<ulong> m_Receivers = new List<ulong>();

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
