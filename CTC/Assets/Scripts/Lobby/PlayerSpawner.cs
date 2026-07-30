using System.Collections.Generic;
using CTC.Runtime.Map;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 스폰만 맡는 Worker — 자리 계산 공식 + 역할별 프리팹 스폰 + 쥐 시드머니 (개편 3단계, 2026-07-31).
    ///
    /// `SpawnPosition`은 **순수 정적 공식**으로 남긴다. 서버가 스폰할 때와 소유 클라이언트가
    /// 자기 자리로 텔레포트할 때 같은 값이 나와야 하고(ClientNetworkTransform이 소유자 위치를
    /// 밀어붙이므로), EditMode 테스트가 리스트를 직접 넣어 검증하기 때문이다.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        public const float FORTRESS_ENTRANCE_OFFSET_METERS = 1.8f;
        private const float SPAWN_HEIGHT = 0.1f;
        private const float WAVE_SIDE_STEP_METERS = 0.7f;

        [SerializeField] private GameObject m_CatPrefab;
        [SerializeField] private GameObject m_MousePrefab;

        /// <summary>
        /// 결정적 스폰 공식(서버 스폰·소유자 초기 텔레포트 공용) —
        /// k번째 고양이는 중앙 라인, m번째 쥐는 자기 요새 입구 앞.
        /// 요새자리는 MapLayout 주입 — 순수 공식이라 테스트는 리스트를 직접 전달.
        /// </summary>
        public static Vector3 SpawnPosition(
            IReadOnlyList<RoleId> _roles, int _index, IReadOnlyList<(Vector3 pos, float yaw)> _fortresses)
        {
            int nth = 0; // 같은 역할 내 순번
            for (int i = 0; i < _index; i++)
            {
                if (_roles[i] == _roles[_index]) nth++;
            }

            if (_roles[_index] == RoleId.Cat) return new Vector3(nth * 2f, SPAWN_HEIGHT, 0f);

            if (_fortresses.Count == 0) return new Vector3(0f, SPAWN_HEIGHT, 0f); // 방어 — layout 미배선

            (Vector3 pos, float yaw) = _fortresses[nth % _fortresses.Count];
            Vector3 entranceDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 spawn = pos + entranceDir * FORTRESS_ENTRANCE_OFFSET_METERS;

            // 요새보다 쥐가 많아 같은 요새를 공유하면 옆으로 비껴 스폰 — 동일 좌표 CC 중첩 방지
            int wave = nth / _fortresses.Count;
            if (wave > 0)
            {
                spawn += Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (WAVE_SIDE_STEP_METERS * wave);
            }
            spawn.y = SPAWN_HEIGHT;
            return spawn;
        }

        /// <summary>활성 MapLayout의 요새자리를 (pos, yaw) 리스트로 — 미배선이면 빈 리스트.</summary>
        public static List<(Vector3 pos, float yaw)> FortressSpots(MapLayout _layout)
        {
            var list = new List<(Vector3, float)>();
            if (_layout == null) return list;

            foreach (FortressPlacement f in _layout.Fortresses)
            {
                list.Add((new Vector3(f.Pos.x, 0f, f.Pos.y), f.YawDeg));
            }
            return list;
        }

        /// <summary>서버 전용 — 참가자 전원을 역할별 프리팹으로 스폰한다.</summary>
        public void ServerSpawnAll(LobbySession _session, MapLayout _layout)
        {
            if (_session == null) return;

            List<RoleId> roles = _session.SnapshotRoles();
            List<(Vector3 pos, float yaw)> forts = FortressSpots(_layout);

            for (int i = 0; i < _session.PlayerCount; i++)
            {
                LobbyPlayerState p = _session.GetPlayer(i);
                GameObject prefab = p.Role == RoleId.Cat ? m_CatPrefab : m_MousePrefab;
                if (prefab == null) continue;

                GameObject go = Instantiate(prefab, SpawnPosition(roles, i, forts), Quaternion.identity);
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(p.ClientId);

                if (p.Role == RoleId.Mouse) GrantStartCheese(go);
            }
        }

        // 스폰 직후 서버 권위로 지급 — 미끼 1개분. 사는 시점·자리는 쥐의 선택.
        // 수치 원본은 CSV(BalanceInfo.csv).
        private static void GrantStartCheese(GameObject _player)
        {
            var wallet = _player.GetComponent<Economy.CheeseWallet>();
            if (wallet != null) wallet.AddCheese(Info.InfoManager.Instance.Balance.MouseStartCheese);
        }
    }
}
