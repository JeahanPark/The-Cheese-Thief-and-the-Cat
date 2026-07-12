// WES에서 이식(2026-07-12) — 변경 3가지:
// 1) UniRx→R3: IObservable<T>→R3.Observable<T>, Subject<T>는 R3 것 사용(Dispose 패턴 동일).
// 2) Managers.Network(미이식 GameNetworkManager) 의존 제거 → NetworkManager.Singleton 직접 사용.
// 3) MonoSingleton→NetworkBehaviour 전환 — NGO의 [Rpc]는 NetworkBehaviour에서만 ILPP 처리되므로
//    씬/프리팹에 NetworkObject와 함께 배치해 스폰되어야 한다(자가 생성 싱글톤 불가). Instance는 정적 캐시.
using System;
using R3;
using Unity.Netcode;
using UnityEngine;
using CTC.Runtime.Core;

namespace CTC.Runtime.Managers
{
    public class ChatManager : NetworkBehaviour
    {
        private static ChatManager s_Instance;

        public static ChatManager Instance => s_Instance;

        public Observable<ChatMessage> OnMessageReceivedAsObservable => m_OnMessageReceived;

        private readonly Subject<ChatMessage> m_OnMessageReceived = new Subject<ChatMessage>();

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

        public void SendChatMessage(string _message)
        {
            if (string.IsNullOrWhiteSpace(_message))
            {
                GameDebug.LogWarning("[ChatManager] Cannot send empty message");
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                GameDebug.LogError("[ChatManager] Network is not running");
                return;
            }

            ulong senderId = NetworkManager.Singleton.LocalClientId;
            SendChatMessageServerRpc(_message, senderId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SendChatMessageServerRpc(string _message, ulong _senderId)
        {
            BroadcastChatMessageClientRpc(_message, _senderId);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastChatMessageClientRpc(string _message, ulong _senderId)
        {
            ChatMessage chatMessage = new ChatMessage
            {
                SenderId = _senderId,
                Message = _message,
                Timestamp = DateTime.Now
            };

            m_OnMessageReceived.OnNext(chatMessage);
            GameDebug.Log($"[ChatManager] Message from {_senderId}: {_message}");
        }
    }

    public struct ChatMessage
    {
        // WES 원본은 public 필드 — CTC 규칙(public 필드 금지)에 따라 auto-property로 전환.
        public ulong SenderId { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
