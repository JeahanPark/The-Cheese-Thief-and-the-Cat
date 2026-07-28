using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 구매 진입점(플레이어 프리팹). 소유자 Buy → ServerRpc → 서버가
    /// 역할·적용가능 검증 → TrySpend → 효과 적용/설치물 스폰. 검증 실패 시 차감 없음.
    /// </summary>
    public class ShopController : NetworkBehaviour
    {
        private const float PLACE_FORWARD_METERS = 1.5f;

        [SerializeField] private GameObject m_DecoyPrefab;
        [SerializeField] private GameObject m_FortressPrefab;
        [SerializeField] private GameObject m_WarehousePrefab;

        /// <summary>설치물을 놓을 수 있는 최대 거리(서버 검증) — 고스트 클램프(4m)보다 넉넉히.</summary>
        public const float MAX_PLACE_DISTANCE = 6f;

        /// <summary>로컬 UI/QA 진입점.</summary>
        public void Buy(ShopItemId _itemId)
        {
            BuyRpc((int)_itemId);
        }

        /// <summary>설치물 위치 지정 구매(고스트 확정). 서버가 거리·겹침을 재검증한다.</summary>
        public void BuyAt(ShopItemId _itemId, Vector3 _position)
        {
            BuyAtRpc((int)_itemId, _position);
        }

        /// <summary>구매 가능 여부(로컬 UI 표시용 — 판정 권위는 서버 BuyRpc).</summary>
        public bool CanApply(ShopItemId _itemId)
        {
            switch (_itemId)
            {
                case ShopItemId.Slingshot:
                    MousePopgun gun1 = GetComponent<MousePopgun>();
                    return gun1 != null && gun1.Tier < 1;
                case ShopItemId.MachineGun:
                    MousePopgun gun2 = GetComponent<MousePopgun>();
                    return gun2 != null && gun2.Tier < 2;
                case ShopItemId.Dash:
                    Player.MouseDash dash = GetComponent<Player.MouseDash>();
                    return dash != null && !dash.Owned;
                case ShopItemId.Decoy:
                    return m_DecoyPrefab != null;
                case ShopItemId.Fortress:
                    return m_FortressPrefab != null;
                case ShopItemId.Warehouse:
                    // 1인 1창고 — 구석에 도배해 무한 수입을 만드는 것을 막는다.
                    // 클라에서는 스폰 목록이 불완전할 수 있어 표시용 근사, 판정 권위는 서버.
                    return m_WarehousePrefab != null
                           && WarehouseProduction.CountOwnedBy(OwnerClientId) < ShopLogic.WAREHOUSE_LIMIT_PER_MOUSE;
                case ShopItemId.CatAttack:
                    CatPounce pounce = GetComponent<CatPounce>();
                    return pounce != null && ShopLogic.CanAddStack(pounce.DamageStacks);
                case ShopItemId.CatSpeed:
                    Player.PlayerMovement move = GetComponent<Player.PlayerMovement>();
                    return move != null && ShopLogic.CanAddStack(move.SpeedStacks);
                default:
                    return false;
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = true)] // 타인 오브젝트로 대리 구매 차단 — 리뷰 I2
        private void BuyRpc(int _itemId)
        {
            ShopItem item = ShopLogic.Find((ShopItemId)_itemId);
            if (item.Id == ShopItemId.None) { BuyResultRpc(false); return; }

            RoleTag tag = GetComponent<RoleTag>();
            if (tag == null || tag.Role != item.Role) { BuyResultRpc(false); return; }

            if (!CanApply(item.Id)) { BuyResultRpc(false); return; }

            CheeseWallet wallet = GetComponent<CheeseWallet>();
            if (wallet == null || !wallet.TrySpend(item.Cost)) { BuyResultRpc(false); return; }

            Apply(item.Id);
            Debug.Log($"[ShopController] {OwnerClientId} 구매: {item.Id} (-{item.Cost})");
            BuyResultRpc(true);
        }

        // 설치물 전용: 클라가 고스트로 확정한 위치에 스폰. 위치는 서버가 재검증(거리·겹침).
        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void BuyAtRpc(int _itemId, Vector3 _position)
        {
            ShopItem item = ShopLogic.Find((ShopItemId)_itemId);
            if (item.Id == ShopItemId.None || !ShopLogic.IsPlaceable(item.Id)) { BuyResultRpc(false); return; }

            RoleTag tag = GetComponent<RoleTag>();
            if (tag == null || tag.Role != item.Role) { BuyResultRpc(false); return; }
            if (!CanApply(item.Id)) { BuyResultRpc(false); return; }

            Vector3 flat = _position - transform.position;
            flat.y = 0f;
            if (flat.magnitude > MAX_PLACE_DISTANCE) { BuyResultRpc(false); return; }

            GameObject prefab = PrefabOf(item.Id);
            if (prefab == null || !PlacementFits(item.Id, _position)) { BuyResultRpc(false); return; }

            CheeseWallet wallet = GetComponent<CheeseWallet>();
            if (wallet == null || !wallet.TrySpend(item.Cost)) { BuyResultRpc(false); return; }

            // 요새 입구(로컬 +Z)가 구매자를 향하게 — 쫓기며 지어도 바로 들어갈 수 있다
            Quaternion rot = item.Id == ShopItemId.Fortress
                ? Quaternion.LookRotation(-FlatForward())
                : Quaternion.identity;
            GameObject go = Instantiate(prefab, _position, rot);
            go.GetComponent<NetworkObject>().Spawn(true);
            BindWarehouseOwner(go);

            Debug.Log($"[ShopController] {OwnerClientId} 설치: {item.Id} @ {_position} (-{item.Cost})");
            BuyResultRpc(true);
        }

        private const int PLACE_OVERLAP_BUFFER = 16;
        private static readonly Collider[] s_PlaceOverlap = new Collider[PLACE_OVERLAP_BUFFER];

        /// <summary>
        /// 설치 지점이 비어 있는가 — 클라 고스트와 동일 규칙(서버 권위 검증).
        /// 플레이어는 방해물로 치지 않는다: 제 몸이나 옆 쥐에 걸려 설치가 거절되는 게
        /// "설치가 너무 힘들다"의 절반이었다(2026-07-28). 겹쳐 놓아도 CC가 서로 밀어낸다.
        /// </summary>
        public static bool PlacementFits(ShopItemId _itemId, Vector3 _position)
        {
            Vector3 half = HalfExtentsOf(_itemId);
            Vector3 center = _position + Vector3.up * half.y;
            // 바닥(Ignore Raycast 레이어 2)은 제외 — 벽·구조물만 방해물로 본다
            int count = Physics.OverlapBoxNonAlloc(center, half * 0.95f, s_PlaceOverlap,
                Quaternion.identity, ~(1 << 2), QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (s_PlaceOverlap[i].transform.root.GetComponent<CharacterController>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>설치물 반경(고스트 표시·겹침 판정 공용).</summary>
        public static Vector3 HalfExtentsOf(ShopItemId _itemId)
        {
            switch (_itemId)
            {
                case ShopItemId.Fortress:
                    return new Vector3(1.2f, 0.6f, 1.2f);   // 요새 풋프린트 2.4²·벽높이 1.2
                case ShopItemId.Warehouse:
                    return new Vector3(0.5f, 0.4f, 0.5f);   // 창고 1.0²×0.8 — 요새보다 작아 구석에 끼워 넣을 수 있다
                default:
                    return new Vector3(0.25f, 0.25f, 0.25f); // 미끼(캡슐 0.4²)
            }
        }

        private GameObject PrefabOf(ShopItemId _itemId)
        {
            switch (_itemId)
            {
                case ShopItemId.Fortress: return m_FortressPrefab;
                case ShopItemId.Warehouse: return m_WarehousePrefab;
                default: return m_DecoyPrefab;
            }
        }

        // 창고는 누가 세웠는지 알아야 수입을 줄 수 있다 — 스폰 직후 서버가 소유자를 새긴다.
        private void BindWarehouseOwner(GameObject _spawned)
        {
            var production = _spawned.GetComponent<WarehouseProduction>();
            if (production != null) production.ServerSetOwner(OwnerClientId);
        }

        // 구매자에게만 결과음 — 성공 카칭 / 실패 붑
        [Rpc(SendTo.Owner)]
        private void BuyResultRpc(bool _success)
        {
            Audio.SfxPlayer.PlayUi(_success ? Audio.SfxId.ShopBuy : Audio.SfxId.ShopDeny);
        }

        // 서버 전용 — BuyRpc에서 검증·차감 후 호출.
        private void Apply(ShopItemId _itemId)
        {
            switch (_itemId)
            {
                case ShopItemId.Slingshot:
                    GetComponent<MousePopgun>().ServerSetTier(1);
                    break;
                case ShopItemId.MachineGun:
                    GetComponent<MousePopgun>().ServerSetTier(2);
                    break;
                case ShopItemId.Dash:
                    GetComponent<Player.MouseDash>().ServerSetOwned();
                    break;
                case ShopItemId.Decoy:
                    SpawnStructure(_itemId, m_DecoyPrefab, Quaternion.identity);
                    break;
                case ShopItemId.Fortress:
                    // 입구(로컬 +Z)가 구매자를 향하게 — 쫓기며 지어도 바로 들어갈 수 있다
                    SpawnStructure(_itemId, m_FortressPrefab, Quaternion.LookRotation(-FlatForward()));
                    break;
                case ShopItemId.Warehouse:
                    SpawnStructure(_itemId, m_WarehousePrefab, Quaternion.identity);
                    break;
                case ShopItemId.CatAttack:
                    GetComponent<CatPounce>().ServerAddDamageStack();
                    break;
                case ShopItemId.CatSpeed:
                    GetComponent<Player.PlayerMovement>().ServerAddSpeedStack();
                    break;
            }
        }

        private void SpawnStructure(ShopItemId _itemId, GameObject _prefab, Quaternion _rotation)
        {
            // 즉시 설치(미끼)는 조준이 없다 — 앞이 벽이면 제 발밑으로 물러선다.
            // 여기서 그냥 실패시키면 치즈만 먹고 아무것도 안 나오는 꼴이 된다.
            Vector3 pos = transform.position + FlatForward() * PLACE_FORWARD_METERS;
            if (!PlacementFits(_itemId, pos)) pos = transform.position;

            GameObject go = Instantiate(_prefab, pos, _rotation);
            go.GetComponent<NetworkObject>().Spawn(true);
            BindWarehouseOwner(go);
        }

        private Vector3 FlatForward()
        {
            return new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        }
    }
}
