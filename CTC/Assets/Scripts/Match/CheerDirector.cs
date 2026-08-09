using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 따봉 — 탈락한 쥐가 관전 중인 동료에게 G로 응원을 보낸다(2026-07-28 요청).
    /// 쥐는 죽으면 despawn돼 할 게 없었다. 판을 보는 것 말고 판에 관여하는 한 줄이 필요하다.
    ///
    /// 고양이에게는 전달 자체를 하지 않는다 — 화면에서만 가리면 "누가 어디서 응원받는지"가
    /// 패킷에 남아 위치 단서가 된다. 서버가 수신자 그룹에서 고양이를 빼고 보낸다.
    /// 씬의 로비 오브젝트에 동승한다(in-scene NetworkObject).
    /// </summary>
    public class CheerDirector : NetworkBehaviour
    {
        private const float MARKER_SECONDS = 2.5f;
        private const float MARKER_HEIGHT = 1.1f;
        private const double COOLDOWN_SECONDS = 3.0;

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private Cameras.SpectatorFallback m_Spectator;

        private readonly Dictionary<ulong, double> m_NextAllowed = new Dictionary<ulong, double>();
        private readonly List<ulong> m_Receivers = new List<ulong>();

        private void Update()
        {
            if (!IsSpawned || m_Lobby == null || !m_Lobby.IsSpawned || !m_Lobby.GameStarted) return;

            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.gKey.wasPressedThisFrame) return;
            if (Core.UiFocus.BlocksGameplay) return; // 채팅·콘솔에 'g'를 치는 중이면 응원이 아니다

            ulong target = SpectatedClientId();
            if (target == ulong.MaxValue) return;

            CheerRpc(target);
        }

        /// <summary>QA 전용 — 키 입력 없이 따봉을 보낸다.</summary>
        public void DevCheer(ulong _targetClientId) => CheerRpc(_targetClientId);


        // 보낼 자격: 관전자(영구 탈락) 또는 **갇힌 쥐**(v3.8).
        // 감옥이 생기면서 "할 게 없는 사람"이 관전자에서 수감자로 옮겨 갔다 —
        // 따봉이 겨냥하던 그 자리라, 대상도 같이 옮긴다.
        private ulong SpectatedClientId()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return ulong.MaxValue;

            NetworkObject local = nm.SpawnManager.GetLocalPlayerObject();
            if (local != null)
            {
                // 갇힌 쥐는 자기 몸이 살아 있으므로, 응원 대상은 스스로 고르지 못한다 —
                // 가장 가까운 자유로운 쥐(구하러 오는 사람)에게 보낸다.
                var jailed = local.GetComponent<JailedTag>();
                if (jailed == null || !jailed.Jailed) return ulong.MaxValue;
                return NearestFreeMouseClientId(local.transform.position);
            }

            Transform current = m_Spectator != null ? m_Spectator.Current : null;
            if (current == null) return ulong.MaxValue;

            var netObj = current.GetComponent<NetworkObject>();
            return netObj != null ? netObj.OwnerClientId : ulong.MaxValue;
        }

        // 갇힌 쥐가 볼 수 있는 건 감옥 주변뿐이라, 가장 가까운 자유로운 쥐를 응원 대상으로 잡는다.
        private static ulong NearestFreeMouseClientId(Vector3 _from)
        {
            ulong best = ulong.MaxValue;
            float bestSqr = float.MaxValue;
            foreach (RoleTag tag in FindObjectsByType<RoleTag>(FindObjectsSortMode.None))
            {
                if (tag.Role != RoleId.Mouse) continue;
                var no = tag.GetComponent<NetworkObject>();
                if (no == null || !no.IsSpawned || !no.IsPlayerObject) continue;

                var jailed = tag.GetComponent<JailedTag>();
                if (jailed != null && jailed.Jailed) continue;

                float sqr = (tag.transform.position - _from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = no.OwnerClientId;
            }
            return best;
        }

        [Rpc(SendTo.Server)]
        private void CheerRpc(ulong _targetClientId, RpcParams _rpcParams = default)
        {
            ulong sender = _rpcParams.Receive.SenderClientId;
            double now = NetworkManager.ServerTime.Time;
            if (m_NextAllowed.TryGetValue(sender, out double next) && now < next) return;

            // 살아 있고 갇히지도 않았으면 무시 — 자유로운 사람의 행동이 아니다
            NetworkObject senderObj = NetworkManager.SpawnManager.GetPlayerNetworkObject(sender);
            if (senderObj != null)
            {
                var senderJail = senderObj.GetComponent<JailedTag>();
                if (senderJail == null || !senderJail.Jailed) return;
            }

            NetworkObject target = NetworkManager.SpawnManager.GetPlayerNetworkObject(_targetClientId);
            if (target == null) return;
            RoleTag tag = target.GetComponent<RoleTag>();
            if (tag == null || tag.Role != RoleId.Mouse) return;

            m_NextAllowed[sender] = now + COOLDOWN_SECONDS;

            BuildReceivers();
            if (m_Receivers.Count == 0) return;
            ShowCheerRpc(_targetClientId, RpcTarget.Group(m_Receivers, RpcTargetUse.Temp));
        }

        // 고양이를 제외한 전원(살아 있는 쥐 + 탈락자). 역할은 스폰된 플레이어 오브젝트로 판단한다 —
        // 로비 목록보다 이쪽이 현재 상태에 가깝다.
        private void BuildReceivers()
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
        private void ShowCheerRpc(ulong _targetClientId, RpcParams _rpcParams = default)
        {
            // 클라이언트에서는 GetPlayerNetworkObject로 남을 못 찾는다(서버 전용) — 스폰 목록을 훑는다
            NetworkObject target = Core.NetUtil.FindPlayerObject(_targetClientId);
            if (target == null) return;

            SpawnMarker(target.transform);
            Audio.SfxPlayer.PlayUi(Audio.SfxId.UiClick, 0.5f);
        }

        // 대상에 매달아 둔다 — 대상이 despawn되면 마커도 같이 사라진다.
        private static void SpawnMarker(Transform _target)
        {
            var go = new GameObject("CheerMarker");
            go.transform.SetParent(_target, false);
            go.transform.localPosition = new Vector3(0f, MARKER_HEIGHT, 0f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "따봉!";
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.45f, 0.95f, 0.45f);
            go.AddComponent<Cameras.FaceCamera>();

            Destroy(go, MARKER_SECONDS);
        }
    }
}
