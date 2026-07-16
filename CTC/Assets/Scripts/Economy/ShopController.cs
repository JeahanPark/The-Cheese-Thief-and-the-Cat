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

        /// <summary>로컬 UI/QA 진입점.</summary>
        public void Buy(ShopItemId _itemId)
        {
            BuyRpc((int)_itemId);
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

        [Rpc(SendTo.Server)]
        private void BuyRpc(int _itemId)
        {
            ShopItem item = ShopLogic.Find((ShopItemId)_itemId);
            if (item.Id == ShopItemId.None) return;

            RoleTag tag = GetComponent<RoleTag>();
            if (tag == null || tag.Role != item.Role) return;

            if (!CanApply(item.Id)) return;

            CheeseWallet wallet = GetComponent<CheeseWallet>();
            if (wallet == null || !wallet.TrySpend(item.Cost)) return;

            Apply(item.Id);
            Debug.Log($"[ShopController] {OwnerClientId} 구매: {item.Id} (-{item.Cost})");
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
                    SpawnStructure(m_DecoyPrefab);
                    break;
                case ShopItemId.Fortress:
                    // 입구(로컬 +Z)가 구매자를 향하게 — 쫓기며 지어도 바로 들어갈 수 있다
                    SpawnStructure(m_FortressPrefab, Quaternion.LookRotation(-FlatForward()));
                    break;
                case ShopItemId.CatAttack:
                    GetComponent<CatPounce>().ServerAddDamageStack();
                    break;
                case ShopItemId.CatSpeed:
                    GetComponent<Player.PlayerMovement>().ServerAddSpeedStack();
                    break;
            }
        }

        private void SpawnStructure(GameObject _prefab)
        {
            SpawnStructure(_prefab, Quaternion.identity);
        }

        private void SpawnStructure(GameObject _prefab, Quaternion _rotation)
        {
            Vector3 pos = transform.position + FlatForward() * PLACE_FORWARD_METERS;
            GameObject go = Instantiate(_prefab, pos, _rotation);
            go.GetComponent<NetworkObject>().Spawn(true);
        }

        private Vector3 FlatForward()
        {
            return new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        }
    }
}
