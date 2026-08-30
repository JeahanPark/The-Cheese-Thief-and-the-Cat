using System.Collections.Generic;
using CTC.Runtime.Core;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 대기실의 몸을 세우는 Worker(규칙 6) — 서버 전용. 참가자 한 명당 쥐 하나.
    ///
    /// 인게임의 <see cref="CTC.Runtime.Lobby.PlayerSpawner"/>와 달리 **역할을 안 본다.**
    /// 대기실에서는 아직 아무도 고양이가 아니라서 전원 쥐 몸이고, 자리는 슬롯 번호로 고정한다 —
    /// 인원수로 원을 나누면 누가 들어올 때마다 서 있던 사람이 밀려난다.
    /// </summary>
    public class RoomAvatarSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject m_AvatarPrefab;

        /// <summary>참가자들이 둘러설 원의 반지름(m). 정원 10명이 안 겹치는 최소치보다 넉넉히 잡는다.</summary>
        [SerializeField] private float m_Radius = 5f;

        private RoomSession m_Session;

        // clientId → 세워 둔 몸. 목록이 흔들려도 이미 선 사람은 그대로 둔다.
        private readonly Dictionary<ulong, NetworkObject> m_Bodies = new Dictionary<ulong, NetworkObject>();

        public float Radius => m_Radius;

        private void Awake() => m_Session = GetComponent<RoomSession>();

        /// <summary>서버 전용 — 참가자 목록과 몸을 맞춘다. 목록이 바뀔 때마다 부른다.</summary>
        public void ServerSync()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || m_Session == null) return;
            if (m_AvatarPrefab == null)
            {
                GameDebug.LogError("[Room] 대기실 몸 프리팹이 안 붙어 있다");
                return;
            }

            List<ulong> ids = m_Session.SnapshotClientIds();

            for (int i = 0; i < ids.Count; i++)
            {
                if (m_Bodies.TryGetValue(ids[i], out NetworkObject exist) && exist != null) continue;
                Spawn(ids[i], i);
            }

            Despawn(ids);
        }

        /// <summary>
        /// 서버 전용 — 세워 둔 몸을 전부 걷는다. **인게임 씬으로 넘기기 직전에 부른다.**
        ///
        /// 동적으로 스폰한 NetworkObject는 기본이 `DestroyWithScene = false`라 씬이 바뀌어도
        /// 살아남는다 — 안 걷으면 인게임 한복판에 대기실 쥐와 그 이름표가 그대로 떠 있다
        /// (2026-08-30 실측).
        /// </summary>
        public void ServerDespawnAll()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            foreach (KeyValuePair<ulong, NetworkObject> pair in m_Bodies)
            {
                if (pair.Value != null && pair.Value.IsSpawned) pair.Value.Despawn();
            }
            m_Bodies.Clear();
        }

        private void Spawn(ulong _clientId, int _slot)
        {
            Vector3 pos = RoomLogic.StandPosition(_slot, RoomLogic.MAX_PLAYERS, m_Radius);
            var rot = Quaternion.Euler(0f, RoomLogic.StandYaw(_slot, RoomLogic.MAX_PLAYERS), 0f);

            GameObject go = Instantiate(m_AvatarPrefab, pos, rot);
            var netObj = go.GetComponent<NetworkObject>();
            netObj.SpawnAsPlayerObject(_clientId);

            m_Bodies[_clientId] = netObj;
            GameDebug.Log($"[Room] 몸 세움 client={_clientId} slot={_slot}");
        }

        // 나간 사람의 몸을 걷는다. NGO가 플레이어 오브젝트를 스스로 지우기도 하므로
        // 이미 사라진 것(null·!IsSpawned)은 조용히 목록에서만 뺀다.
        private void Despawn(List<ulong> _alive)
        {
            var stale = new List<ulong>();
            foreach (KeyValuePair<ulong, NetworkObject> pair in m_Bodies)
            {
                if (_alive.Contains(pair.Key)) continue;
                stale.Add(pair.Key);
            }

            for (int i = 0; i < stale.Count; i++)
            {
                NetworkObject body = m_Bodies[stale[i]];
                m_Bodies.Remove(stale[i]);
                if (body != null && body.IsSpawned) body.Despawn();
            }
        }
    }
}
