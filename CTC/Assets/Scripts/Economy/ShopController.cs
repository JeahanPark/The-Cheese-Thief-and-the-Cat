using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 테크 구매·업그레이드·사용 진입점(플레이어 프리팹) — 테크 v3(2026-09-07).
    /// 소유자 Buy → ServerRpc → 서버가 역할·상점 안(쥐)·TechLogic·TrySpend 검증 → 레벨 적용.
    /// 사용(Use)은 상점 밖 숫자키 — 요새 즉발·창고 조준·대시.
    /// </summary>
    public class ShopController : NetworkBehaviour
    {
        // 밸런스 원본은 BalanceInfo.csv — 재컴파일 없이 다음 플레이에 반영된다.
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        [SerializeField] private GameObject m_FortressPrefab;
        [SerializeField] private GameObject m_WarehousePrefab;
        [SerializeField] private GameObject m_StickyPrefab;

        /// <summary>설치물을 놓을 수 있는 최대 거리(서버 검증) — 고스트 클램프보다 넉넉히.</summary>
        public static float MAX_PLACE_DISTANCE => Balance.PlaceMaxDistanceMeters;

        // 동작이 보이는 시간(테크 v3 계획 2b Task 5) — 팩에 이미 있던 클립을 원샷으로 쓴다.
        private const float BUY_ANIM_SECONDS = 0.35f;      // Clicked — 짧은 끄덕임
        private const float FORTRESS_ANIM_SECONDS = 0.5f;  // Jump — 뛰어올랐다 내려오며 벽이 선다
        private const float STICKY_ANIM_SECONDS = 0.5f;    // Bounce — 발밑에 내려찍는다

        private TechSlots m_Slots;
        private RoleTag m_Tag;
        private CheeseWallet m_Wallet;
        private Player.PlayerAnimationDriver m_AnimationDriver; // 쓸 때마다 찾지 않는다

        public override void OnNetworkSpawn()
        {
            m_Slots = GetComponent<TechSlots>();
            m_Tag = GetComponent<RoleTag>();
            m_Wallet = GetComponent<CheeseWallet>();
            // 전 클라에서 잡는다 — 원샷 Rpc가 각자의 프로세스에서 이 드라이버를 부른다.
            m_AnimationDriver = GetComponentInChildren<Player.PlayerAnimationDriver>();
        }

        // ===== 로컬 진입점 =====

        /// <summary>산다(미보유) 또는 키운다(보유). 로컬 UI·봇·QA 공용.</summary>
        public void Buy(TechId _id) => BuyRpc((int)_id);

        /// <summary>
        /// 즉발 액티브 — 요새·대시.
        ///
        /// **대시만 서버를 돌지 않는다.** 이동이 소유자 권위(ClientNetworkTransform)라
        /// `MouseDash.TryDash`가 `IsOwner`를 보고 끊는다 — 서버가 남의 몸에서 불러 봐야
        /// 아무 일도 안 일어난다. 그래서 대시는 여기(소유자 프로세스)서 바로 쏜다.
        /// 봇은 서버가 소유하므로 같은 경로로 통과한다.
        /// </summary>
        public void Use(TechId _id)
        {
            if (_id == TechId.Dash)
            {
                if (CanUse(_id) != UseDeny.None) { Audio.SfxPlayer.PlayUi(Audio.SfxId.ShopDeny); return; }
                Player.MouseDash dash = GetComponent<Player.MouseDash>();
                if (dash != null) dash.DevDash();
                return;
            }
            UseRpc((int)_id);
        }

        /// <summary>조준 액티브 — 창고. 고스트 확정 위치를 서버가 재검증한다.</summary>
        public void UseAt(TechId _id, Vector3 _position) => UseAtRpc((int)_id, _position);

        /// <summary>로컬 표시용 판정(권위는 서버). 상점 안 여부까지 본다.</summary>
        public TechDeny CanBuyNow(TechId _id)
        {
            if (m_Slots == null || m_Tag == null || m_Wallet == null) return TechDeny.Unknown;
            if (TechLogic.NeedsShopSpot(m_Tag.Role) && !IsBotExempt() && !Map.ShopSpot.AnyContains(transform.position))
                return TechDeny.NotInShop;
            return TechLogic.CanBuy(m_Slots.Owned, _id, m_Tag.Role, m_Wallet.CheeseValue, Balance.TechSlotCap);
        }

        /// <summary>
        /// 쓸 수 있는가 — 보유·상황(요새 겹침·창고 상한·설치비). **이유까지** 돌려준다:
        /// View가 TechId로 사유를 추측하면 치즈가 모자란 걸 "창고는 하나까지"라고 거짓말한다(2026-09-07).
        /// </summary>
        public UseDeny CanUse(TechId _id)
        {
            if (m_Slots == null || m_Wallet == null) return UseDeny.Unavailable;
            if (!TechLogic.IsActive(_id)) return UseDeny.NotActive;
            if (m_Slots.LevelOf(_id) == 0) return UseDeny.NotOwned;

            switch (_id)
            {
                case TechId.Fortress:
                    if (m_FortressPrefab == null) return UseDeny.Unavailable;
                    // 서 있는 요새 안에서는 못 세운다 — 겹치기 금지(FortressShield.AnyContains 주석)
                    if (FortressShield.AnyContains(transform.position)) return UseDeny.InsideFortress;
                    if (m_Wallet.CheeseValue < Balance.FortressPlaceCost) return UseDeny.NotEnough;
                    return UseDeny.None;
                case TechId.Warehouse:
                    if (m_WarehousePrefab == null) return UseDeny.Unavailable;
                    // 동시 설치 상한(창고 Lv1~2: 1개, Lv2부터 2개) — 구석에 도배해 무한 수입을 만드는 것을 막는다.
                    // 클라에서는 스폰 목록이 불완전할 수 있어 표시용 근사, 판정 권위는 서버.
                    if (WarehouseProduction.CountOwnedBy(OwnerClientId)
                        >= WarehouseLogic.LimitOf(m_Slots.LevelOf(TechId.Warehouse)))
                        return UseDeny.LimitReached;
                    // 설치마다 치즈 — 재설치가 공짜면 째기 응징이 안 된다(2026-09-07).
                    if (m_Wallet.CheeseValue < Balance.WarehousePlaceCost) return UseDeny.NotEnough;
                    return UseDeny.None;
                case TechId.Sticky:
                    if (m_StickyPrefab == null) return UseDeny.Unavailable;
                    StickyUser sticky = GetComponent<StickyUser>();
                    if (sticky == null) return UseDeny.Unavailable;
                    // 설치비가 없는 대신 쿨다운이 값이다 — 이유를 따로 말해야
                    // 핫바가 "지금은 못 쓴다"로 뭉뚱그리지 않는다.
                    if (!sticky.Ready) return UseDeny.Cooldown;
                    return UseDeny.None;
                case TechId.Dash:
                    return UseDeny.None;   // 쿨다운은 MouseDash가 본다
                case TechId.CatFrenzy:
                    Player.CatFrenzy frenzy = GetComponent<Player.CatFrenzy>();
                    if (frenzy == null) return UseDeny.Unavailable;
                    // 설치비가 없다 — 값은 쿨다운이다. 이유를 따로 말해야 핫바가 뭉뚱그리지 않는다.
                    if (!frenzy.Ready) return UseDeny.Cooldown;
                    return UseDeny.None;
                default:
                    return UseDeny.Unavailable;
            }
        }

        /// <summary>쓸 때 드는 치즈(설치비) — "얼마 모자람"을 말하려면 View도 알아야 한다.</summary>
        public static int UseCostOf(TechId _id)
        {
            switch (_id)
            {
                case TechId.Fortress: return Balance.FortressPlaceCost;
                case TechId.Warehouse: return Balance.WarehousePlaceCost;
                default: return 0;
            }
        }

        // 계획 1 한정: 봇은 상점까지 가지 않는다(경로는 계획 3). 계측치에 "상점 이동 비용 제외"를 적는다.
        // 봇의 몸만 `SpawnAsPlayerObject`를 안 거친다 — LocalControl이 사람과 봇을 가르는 것과 같은 표식.
        private bool IsBotExempt()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return NetworkObject != null && !NetworkObject.IsPlayerObject;
#else
            return false;
#endif
        }

        // ===== 서버 =====

        [Rpc(SendTo.Server, RequireOwnership = true)] // 타인 오브젝트로 대리 구매 차단 — 리뷰 I2
        private void BuyRpc(int _id)
        {
            var id = (TechId)_id;
            if (CanBuyNow(id) != TechDeny.None) { ResultRpc(false); return; }

            // 효과를 붙일 컴포넌트가 없으면 **차감 전에** 끊는다 — 예전 CanApply가 겸하던 일이다.
            // 안 그러면 치즈만 먹고 레벨만 오른 뒤 NRE로 ResultRpc도 못 보낸다(응답 없는 구매).
            if (!CanApplyLevel(id)) { ResultRpc(false); return; }

            TechRow row = TechLogic.NextRow(m_Slots.Owned, id);
            if (!m_Wallet.TrySpend(row.Cost)) { ResultRpc(false); return; }

            m_Slots.ServerApplyNextLevel(id);
            TryApplyLevel(id, row.Level);
            BuySoundRpc(transform.position);
            Core.GameDebug.Log($"[ShopController] {OwnerClientId} {id} Lv{row.Level} (-{row.Cost})");
            ResultRpc(true);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void UseRpc(int _id)
        {
            var id = (TechId)_id;
            if (CanUse(id) != UseDeny.None) { ResultRpc(false); return; }
            switch (id)
            {
                case TechId.Fortress:
                    // 즉발 방패(2026-08-15): **내 위치 중심**, 입구(로컬 +Z) = 내가 보는 방향.
                    // 나는 이미 안에 있고, 겹친 남의 몸은 FortressShield가 밀어낸다.
                    if (!m_Wallet.TrySpend(Balance.FortressPlaceCost)) { ResultRpc(false); return; }
                    SpawnFortressAround(m_FortressPrefab);
                    FortressFxRpc();
                    break;
                case TechId.Sticky:
                    // 발밑에 깐다 — 조준이 없다. 쫓기는 중에 쓰는 물건이라 조준할 여유가 없다.
                    SpawnStickyPatch();
                    StickyFxRpc();
                    break;
                case TechId.CatFrenzy:
                    // 몸에 켜지는 것뿐이라 스폰이 없다 — 지속·쿨·소리는 컴포넌트가 갖는다.
                    Player.CatFrenzy frenzy = GetComponent<Player.CatFrenzy>();
                    if (frenzy == null) { ResultRpc(false); return; }
                    frenzy.ServerTryUse();
                    break;
                // 대시는 여기 없다 — 소유자 권위라 Use()가 로컬에서 처리한다(위 주석 참조).
                default:
                    ResultRpc(false);
                    return;
            }
            ResultRpc(true);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void UseAtRpc(int _id, Vector3 _position)
        {
            var id = (TechId)_id;
            if (id != TechId.Warehouse || CanUse(id) != UseDeny.None) { ResultRpc(false); return; }

            Vector3 flat = _position - transform.position;
            flat.y = 0f;
            if (flat.magnitude > MAX_PLACE_DISTANCE || !PlacementFits(id, _position)) { ResultRpc(false); return; }

            // 설치마다 치즈 — 재설치가 공짜면 째기 응징이 안 된다. 자리 검증을 통과한 뒤에만 깎는다.
            if (!m_Wallet.TrySpend(Balance.WarehousePlaceCost)) { ResultRpc(false); return; }

            GameObject go = Instantiate(m_WarehousePrefab, _position, Quaternion.identity);

            // 지어질 때의 테크 레벨을 **스폰 전에** 박는다 — HP는 OnNetworkSpawn이 최대치로 채우고,
            // 수입·소리·환급은 이 창고가 평생 쓸 값이다(나중에 테크를 키워도 서 있는 창고는 그대로).
            int warehouseLv = m_Slots != null ? m_Slots.LevelOf(TechId.Warehouse) : 0;
            int hiddenLv = m_Slots != null ? m_Slots.LevelOf(TechId.HiddenWarehouse) : 0;

            Health hp = go.GetComponent<Health>();
            if (hp != null) hp.ServerSetMaxHp(WarehouseLogic.MaxHp(ShopLogic.WAREHOUSE_MAX_HP, hiddenLv));

            var production = go.GetComponent<WarehouseProduction>();
            if (production != null) production.ServerSetTech(warehouseLv, hiddenLv, Balance.WarehousePlaceCost);

            go.GetComponent<NetworkObject>().Spawn(true);
            BindWarehouseOwner(go);
            ResultRpc(true);
        }

        /// <summary>
        /// 이 테크를 실제로 적용할 수 있는가 — 부작용 없는 사전 검사(차감 전에 부른다).
        /// <see cref="TryApplyLevel"/>과 **같은 목록**을 봐야 한다: 여기서 통과한 게 저기서 실패하면
        /// 치즈만 사라진다.
        /// </summary>
        private bool CanApplyLevel(TechId _id)
        {
            switch (_id)
            {
                case TechId.Gun: return GetComponent<MousePopgun>() != null;
                case TechId.Pepper: return GetComponent<PepperRounds>() != null;
                case TechId.Dash: return GetComponent<Player.MouseDash>() != null;
                case TechId.CheeseShield: return GetComponent<CheeseShield>() != null;
                case TechId.Fortress:
                case TechId.Warehouse:
                    return true;                                                // 설치권만 — 실제 설치는 Use
                case TechId.HiddenWarehouse:
                    return true;                                                // 순수 패시브 — 값은 창고를 세울 때 읽는다
                case TechId.BigPouch:
                    return true;                                                // 순수 패시브 — 값은 채굴할 때 CheeseSpot이 읽는다
                case TechId.Sticky: return GetComponent<StickyUser>() != null;  // 쿨다운을 들 컴포넌트
                case TechId.FastFeet:
                    return GetComponent<Player.PlayerMovement>() != null;       // 이속 가산을 더할 몸
                case TechId.CatClaw: return GetComponent<CatPounce>() != null;
                case TechId.CatStomp: return GetComponent<Player.PlayerMovement>() != null;
                case TechId.CatHide:
                    return GetComponent<Player.CatHide>() != null;              // 최대 HP를 올릴 컴포넌트
                case TechId.CatHunger:
                    return true;                                                // 순수 패시브 — 값은 덮칠 때 CatPounce가 읽는다
                case TechId.CatFrenzy:
                    return GetComponent<Player.CatFrenzy>() != null;            // 켜짐·쿨을 들 컴포넌트
                case TechId.CatEar:
                    return true;                                                // 듣는 쪽 배율이라 몸에 적용할 것이 없다
                default:
                    // CSV 행이 코드보다 먼저 들어온 경우(계획 2 테크). 조용히 돈만 받지 않는다.
                    Core.GameDebug.LogWarning($"[ShopController] ApplyLevel 미구현: {_id}");
                    return false;
            }
        }

        /// <summary>
        /// 레벨이 오른 순간 기존 컴포넌트에 반영. 계획 2가 신규 테크를 여기에 붙인다.
        /// 컴포넌트가 없으면 false — 프리팹 배선이 빠진 것이라 조용히 넘기지 않는다.
        /// </summary>
        private bool TryApplyLevel(TechId _id, int _level)
        {
            switch (_id)
            {
                case TechId.Gun:
                    MousePopgun gun = GetComponent<MousePopgun>();
                    if (gun == null) return false;
                    gun.ServerSetTier(_level);                                  // Lv1 새총 · Lv2 기관총
                    return true;
                case TechId.Pepper:
                    PepperRounds pepper = GetComponent<PepperRounds>();
                    if (pepper == null) return false;
                    pepper.ServerSetOwned();
                    return true;
                case TechId.Dash:
                    Player.MouseDash dash = GetComponent<Player.MouseDash>();
                    if (dash == null) return false;
                    dash.ServerSetOwned();
                    return true;
                case TechId.CheeseShield:
                    // 반영할 게 없다 — 레벨은 TechSlots가 들고 CheeseShield가 피격 때 읽는다.
                    return GetComponent<CheeseShield>() != null;
                case TechId.Fortress:
                case TechId.Warehouse:
                    return true;                                                // 설치권만 — 실제 설치는 Use
                case TechId.HiddenWarehouse:
                    // 반영할 게 없다 — 레벨은 TechSlots가 들고, 창고를 세우는 UseAtRpc가 그때 읽는다.
                    return true;
                case TechId.BigPouch:
                    // 반영할 게 없다 — 레벨은 TechSlots가 들고, 채굴하는 CheeseSpot이 그때 읽는다.
                    return true;
                case TechId.Sticky:
                    // 반영할 게 없다 — 레벨은 TechSlots가 들고, 장판을 깔 때 반경·감속·쿨을 읽는다.
                    return GetComponent<StickyUser>() != null;
                case TechId.FastFeet:
                    // 반영할 게 없다 — 레벨은 TechSlots가 들고, PlayerMovement가 매 프레임 읽는다.
                    // (강화 스택과 달리 서버에 쌓을 상태가 없다 — 스택이 아니라 레벨이 곧 값이다.)
                    return GetComponent<Player.PlayerMovement>() != null;
                case TechId.CatClaw:
                    CatPounce pounce = GetComponent<CatPounce>();
                    if (pounce == null) return false;
                    pounce.ServerAddDamageStack();                              // 레벨 = 스택
                    return true;
                case TechId.CatStomp:
                    Player.PlayerMovement move = GetComponent<Player.PlayerMovement>();
                    if (move == null) return false;
                    move.ServerAddSpeedStack();
                    return true;
                case TechId.CatHide:
                    Player.CatHide hide = GetComponent<Player.CatHide>();
                    if (hide == null) return false;
                    hide.ServerApplyLevel(_level);                              // 이미 스폰된 몸의 최대 HP를 올린다
                    return true;
                case TechId.CatHunger:
                    // 반영할 게 없다 — 레벨은 TechSlots가 들고, 덮치는 CatPounce가 적립할 때 읽는다.
                    return true;
                case TechId.CatFrenzy:
                    // 반영할 게 없다 — 레벨은 TechSlots가 들고, 켤 때 CatFrenzy가 지속·쿨·배율을 읽는다.
                    return GetComponent<Player.CatFrenzy>() != null;
                case TechId.CatEar:
                    // 예민한 귀는 듣는 쪽 배율이라 몸에 적용할 것이 없다 —
                    // ListenerRangeBinder가 TechSlots를 보고 세운다.
                    return true;
                default:
                    Core.GameDebug.LogWarning($"[ShopController] ApplyLevel 미구현: {_id}");
                    return false;
            }
        }

        // 구매음은 12m로 퍼진다(정보=소리) — 상점 근처 고양이가 듣는다. 실패음은 구매자만.
        // 모션(Clicked)도 여기서 함께 튼다 — 이미 전 클라가 도는 길이라 새 Rpc가 필요 없다.
        [Rpc(SendTo.Everyone)]
        private void BuySoundRpc(Vector3 _at)
        {
            Audio.SfxPlayer.PlayAt(Audio.SfxId.ShopBuy, _at, 1f, Balance.ShopBuySoundMeters);
            PlayOneShot(Player.AnimationStateLogic.Clicked, BUY_ANIM_SECONDS);
        }

        /// <summary>
        /// 설치 동작(요새·끈끈이)을 전 클라에 보인다 — 테크 v3 계획 2b Task 5.
        ///
        /// **설치음이 나는 파일에는 못 붙인다.** 요새 설치음은 `PlaceSfxOnSpawn`(요새 몸)과
        /// `FortressShield.PushSfxRpc`(밀린 고양이 쪽)에서 나는데, 둘 다 **누가 세웠는지를 모른다** —
        /// 모션이 붙어야 하는 몸은 설치한 쥐다. 그래서 설치를 부른 이 몸에서 직접 쏜다.
        /// `UseRpc`는 서버 전용이라 여기서 한 번 더 전 클라로 펴 준다.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void FortressFxRpc() => PlayOneShot(Player.AnimationStateLogic.Jump, FORTRESS_ANIM_SECONDS);

        [Rpc(SendTo.Everyone)]
        private void StickyFxRpc() => PlayOneShot(Player.AnimationStateLogic.Bounce, STICKY_ANIM_SECONDS);

        private void PlayOneShot(string _state, float _seconds)
        {
            if (m_AnimationDriver != null) m_AnimationDriver.PlayOneShot(_state, _seconds);
        }

        [Rpc(SendTo.Owner)]
        private void ResultRpc(bool _success)
        {
            if (!_success) Audio.SfxPlayer.PlayUi(Audio.SfxId.ShopDeny);
        }

        // ===== 설치 공용 =====

        public const int PLACE_OVERLAP_BUFFER = 16;
        private static readonly Collider[] s_PlaceOverlap = new Collider[PLACE_OVERLAP_BUFFER];

        // 바닥(Ignore Raycast 레이어 2)은 제외 — 벽·구조물만 방해물로 본다
        private const int PLACE_MASK = ~(1 << 2);

        /// <summary>
        /// 설치 지점에 닿는 콜라이더를 모은다 — <see cref="PlacementFits"/>와 진단 훅
        /// (<c>TestManager.TestDebugPlacementBlockers</c>)이 **같은 질의**를 보게 하는 원본이다.
        /// 여기서 갈라지면 "훅은 비었다는데 설치는 거절된다"가 된다.
        /// </summary>
        public static int PlacementOverlap(TechId _id, Vector3 _position, Collider[] _buffer)
        {
            Vector3 half = HalfExtentsOf(_id);
            Vector3 center = _position + Vector3.up * half.y;
            return Physics.OverlapBoxNonAlloc(center, half * 0.95f, _buffer,
                Quaternion.identity, PLACE_MASK, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// 이 콜라이더가 설치를 막는가. **플레이어의 몸은 막지 않는다** — 제 몸이나 옆 쥐에 걸려
        /// 설치가 거절되는 게 "설치가 너무 힘들다"의 절반이었다(2026-07-28).
        /// 겹쳐 놓아도 CC가 서로 밀어낸다.
        /// </summary>
        public static bool IsPlacementBlocker(Collider _collider)
        {
            if (_collider == null) return false;
            return _collider.transform.root.GetComponent<CharacterController>() == null;
        }

        /// <summary>
        /// 설치 지점이 비어 있는가 — 클라 고스트와 동일 규칙(서버 권위 검증).
        /// </summary>
        public static bool PlacementFits(TechId _id, Vector3 _position)
        {
            int count = PlacementOverlap(_id, _position, s_PlaceOverlap);
            for (int i = 0; i < count; i++)
            {
                if (IsPlacementBlocker(s_PlaceOverlap[i])) return false;
            }
            return true;
        }

        /// <summary>설치물 반경(고스트 표시·겹침 판정 공용).</summary>
        public static Vector3 HalfExtentsOf(TechId _id)
        {
            switch (_id)
            {
                case TechId.Fortress:
                    return new Vector3(1.2f, 0.6f, 1.2f);   // 요새 풋프린트 2.4²·벽높이 1.2
                case TechId.Warehouse:
                    return new Vector3(0.5f, 0.4f, 0.5f);   // 창고 1.0²×0.8 — 요새보다 작아 구석에 끼워 넣을 수 있다
                default:
                    // 알 수 없는 테크 — 작은 캡슐 폭, 예전 미끼 크기
                    return new Vector3(0.25f, 0.25f, 0.25f);
            }
        }

        // 창고는 누가 세웠는지 알아야 수입을 줄 수 있다 — 스폰 직후 서버가 소유자를 새긴다.
        private void BindWarehouseOwner(GameObject _spawned)
        {
            var production = _spawned.GetComponent<WarehouseProduction>();
            if (production != null) production.ServerSetOwner(OwnerClientId);
        }

        /// <summary>
        /// 끈끈이 장판을 발밑에 깐다(서버). 반경·감속·쿨은 지금 레벨의 TechInfo P1/P2/P3다 —
        /// 나중에 테크를 키워도 이미 깔린 장판은 그대로다(요새·창고와 같은 규칙).
        /// </summary>
        private void SpawnStickyPatch()
        {
            int level = m_Slots != null ? m_Slots.LevelOf(TechId.Sticky) : 0;
            float radius = TechLogic.ParamOf(TechId.Sticky, level, 1);
            float slowRatio = TechLogic.ParamOf(TechId.Sticky, level, 2);
            float cooldown = TechLogic.ParamOf(TechId.Sticky, level, 3);

            GameObject go = Instantiate(m_StickyPrefab, transform.position, Quaternion.identity);

            // 값은 **스폰 전에** 넣는다 — 클라가 받는 첫 상태에 반경이 들어 있어야
            // 쿼드가 처음부터 제 크기로 보인다(스폰 후에 쓰면 한 틱 작게 그려진다).
            StickyPatch patch = go.GetComponent<StickyPatch>();
            if (patch != null) patch.ServerSetup(radius, slowRatio);

            go.GetComponent<NetworkObject>().Spawn(true);

            StickyUser user = GetComponent<StickyUser>();
            if (user != null) user.ServerStartCooldown(cooldown);
        }

        // 요새는 겹침 검사를 하지 않는다 — 벽에 물려도 방패 구실은 하고, 쫓기는 순간에
        // "여기엔 못 놓는다"는 답은 치즈만 먹은 것보다 나쁘다. 발밑 = 내 위치.
        private void SpawnFortressAround(GameObject _prefab)
        {
            GameObject go = Instantiate(_prefab, transform.position, Quaternion.LookRotation(FlatForward()));

            // 요새 레벨(TechInfo P1 = HP · P2 = 반사) — **스폰 전에** 세운다.
            // OnNetworkSpawn이 현재 HP를 최대치로 채우므로 그전이어야 그 HP로 시작한다.
            int level = m_Slots != null ? m_Slots.LevelOf(TechId.Fortress) : 0;
            Health hp = go.GetComponent<Health>();
            if (hp != null) hp.ServerSetMaxHp(FortressMaxHp(level));

            ThornWall thorn = go.GetComponent<ThornWall>();
            if (thorn != null) thorn.ServerSet(Mathf.RoundToInt(TechLogic.ParamOf(TechId.Fortress, level, 2)));

            go.GetComponent<NetworkObject>().Spawn(true);

            // 누가 세웠는지 — 미니맵이 「내 요새만」 그린다. 창고(BindWarehouseOwner)와 같이
            // **스폰 뒤**에 새긴다: 스폰 전에 NetworkVariable을 쓰면 NGO가 경고를 뱉는다.
            FortressShield shield = go.GetComponent<FortressShield>();
            if (shield != null) shield.ServerSetOwner(OwnerClientId);
        }

        /// <summary>
        /// 요새 최대 HP — TechInfo P1이 살아 있는 원본이다.
        /// 표가 비었거나(레벨 0) 값을 못 읽은 경우에만 프리팹에 구워진 기본값(BalanceInfo.FortressHp)으로 물러난다.
        /// 0을 그대로 넘기면 세우자마자 부서진 요새가 된다 — 여기서 막는다.
        /// </summary>
        private static int FortressMaxHp(int _level)
        {
            int fromTech = Mathf.RoundToInt(TechLogic.ParamOf(TechId.Fortress, _level, 1));
            if (fromTech > 0) return fromTech;

            Core.GameDebug.LogWarning(
                $"[Shop] 요새 HP를 TechInfo에서 못 읽었다(레벨 {_level}) — BalanceInfo.FortressHp로 대체한다.");
            return Balance.FortressHp;
        }

        private Vector3 FlatForward()
        {
            return new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        }
    }
}
