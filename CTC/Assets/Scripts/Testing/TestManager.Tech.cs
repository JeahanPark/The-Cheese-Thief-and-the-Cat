// TestManager의 일부 — 테크 v3 훅 (2026-09-07). wesqa가 리플렉션으로 Instance.<method>를 부르므로
// **타입을 쪼개지 않는다**(CLAUDE.md). 개발 빌드에도 실린다 — 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
using Cysharp.Threading.Tasks;
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Map;
using CTC.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>구매가 실제로 반영될 때까지 기다리는 상한(초). 못 기다리면 경고를 남기고 포기한다.</summary>
        private const float TECH_LEVEL_WAIT_SECONDS = 2f;

        /// <summary>
        /// 산 테크가 **눈에 보일 때까지** 기다린다 — "사고 바로 쓴다" 훅의 유일한 안전장치.
        ///
        /// 호스트에서는 서버 RPC가 그 자리에서 돌아 `Buy` 다음 줄이면 이미 레벨이 올라 있다.
        /// 그런데 클론(원격 클라이언트)에서는 RPC가 왕복하고 `TechSlots`(NetworkList)가 복제될
        /// 때까지 몇 프레임이 걸린다 — 그 사이에 `Use`를 부르면 "아직 없다"로 조용히 버려져
        /// **설치가 아예 안 일어난 채 QA가 통과처럼 흘러간다**(2026-09-07 리뷰 C1).
        /// </summary>
        private static async UniTask<bool> WaitForTechLevel(
            TechSlots _slots, TechId _id, int _level, CancellationToken _token)
        {
            if (_slots == null) return false;

            float until = Time.realtimeSinceStartup + TECH_LEVEL_WAIT_SECONDS;
            while (_slots.LevelOf(_id) < _level)
            {
                if (_token.IsCancellationRequested) return false;
                if (Time.realtimeSinceStartup > until)
                {
                    GameDebug.LogWarning(
                        $"[TestManager] {_id} Lv{_level} 반영을 {TECH_LEVEL_WAIT_SECONDS}초 안에 못 봤다 — 구매가 거절됐을 수 있다");
                    return false;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, _token);
            }
            return true;
        }

        /// <summary>QA: 테크 한 레벨의 값(없는 행이면 -1) — 스크립트가 가격을 손으로 적지 않게 한다.</summary>
        public int TestGetTechCost(string _techId, int _level)
        {
            if (!System.Enum.TryParse(_techId, out TechId id)) return -1;
            TechRow row = TechLogic.RowOf(id, _level);
            return row.IsValid ? row.Cost : -1;
        }

        /// <summary>QA: 설치 1회 비용(설치권과 별개). 설치물이 아니면 0, 모르는 이름이면 -1.</summary>
        public int TestGetPlaceCost(string _techId)
        {
            if (!System.Enum.TryParse(_techId, out TechId id)) return -1;
            Info.BalanceInfo b = Info.InfoManager.Instance.Balance;
            switch (id)
            {
                case TechId.Fortress: return b.FortressPlaceCost;
                case TechId.Warehouse: return b.WarehousePlaceCost;
                default: return 0;
            }
        }

        // NetUtil.FindPlayerObject(clientId)는 NetworkObject를 준다 — 이웃 partial(TestManager.Balance)과 같은 방식.
        private static GameObject FindPlayerGo(long _clientId)
        {
            NetworkObject no = NetUtil.FindPlayerObject((ulong)_clientId);
            return no != null ? no.gameObject : null;
        }

        /// <summary>특정 클라이언트의 지갑 — 기존 TestGetCheese()는 로컬 전용이라 따로 둔다.</summary>
        public int TestGetCheeseOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            CheeseWallet w = p != null ? p.GetComponent<CheeseWallet>() : null;
            return w != null ? w.CheeseValue : -1;
        }

        /// <summary>보유 레벨(0 = 미보유, -1 = 못 찾음).</summary>
        public int TestGetTechLevel(long _clientId, string _techId)
        {
            GameObject p = FindPlayerGo(_clientId);
            TechSlots slots = p != null ? p.GetComponent<TechSlots>() : null;
            if (slots == null || !System.Enum.TryParse(_techId, out TechId id)) return -1;
            return slots.LevelOf(id);
        }

        /// <summary>보유 칸 수 — 기획 규칙 1(5칸)이 지켜지는지 본다.</summary>
        public int TestGetTechSlotCount(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            TechSlots slots = p != null ? p.GetComponent<TechSlots>() : null;
            return slots != null ? slots.Count : -1;
        }

        /// <summary>지금 상점 자리 안에 서 있는가 — 쥐의 구매 조건(기획 규칙 3).</summary>
        public bool TestIsInShop(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            return p != null && ShopSpot.AnyContains(p.transform.position);
        }

        /// <summary>실제로 씬에 선 상점 수 — CSV의 ShopSpotCount와 어긋나면 배선 사고다.</summary>
        public int TestGetShopSpotCount()
        {
            return FindObjectsByType<ShopSpot>(FindObjectsSortMode.None).Length;
        }

        /// <summary>
        /// QA: 지금 선 상점 자리 **전부**를 "x,z|x,z|..."로(없으면 빈 문자열).
        ///
        /// 기존 <see cref="TestGetShopSpotPos"/>는 번호 하나씩이고 <see cref="TestGetShopSpotCount"/>는
        /// 개수뿐이라, 둘로는 **「판마다 자리가 바뀌었나」를 못 잰다** — 개수가 같으면 통과해 버린다.
        /// 좌표를 통째로 받아 두 판을 집합으로 비교하라고 만든 훅이다(계획 3 Task 7 ②).
        ///
        /// 순서는 명부(<c>ShopSpot.All</c>) 순이지 정렬이 아니다 — 비교하는 쪽이 정렬한다.
        /// </summary>
        public string TestGetShopSpotPositions()
        {
            System.Collections.Generic.IReadOnlyList<ShopSpot> all = ShopSpot.All;
            var parts = new System.Collections.Generic.List<string>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                Vector3 p = all[i].transform.position;
                parts.Add($"{p.x:F2},{p.z:F2}");
            }
            return string.Join("|", parts);
        }

        /// <summary>
        /// QA: 그 클라이언트가 **판을 시작한 뒤 번 총액**(장난기 누적, -1 = 못 찾음).
        ///
        /// 전직 문턱이 잔액이 아니라 이 값이다(계획 3 Task 5). 기존
        /// <c>TestGetAdvanceMischief</c>는 로컬 전용이라 호스트에서 남의 고양이를 못 본다 —
        /// 여기서는 <see cref="FindPlayerGo"/>로 지목해 읽는다.
        /// </summary>
        public int TestGetEarnedOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            CheeseWallet w = p != null ? p.GetComponent<CheeseWallet>() : null;
            return w != null ? w.EarnedValue : -1;
        }

        /// <summary>
        /// QA: 사람의 숫자키와 같은 입구 — 판정은 서버가 한다(상점 밖 쥐는 거절).
        ///
        /// **로컬 플레이어에만 듣는다.** Buy는 `RequireOwnership` 소유자 RPC라 남의 몸에서 부르면
        /// NGO가 보내기 전에 버린다. 클론을 움직이려면 `MultiplayerQaProbe`의 `TestMp*` 커맨드를 쓴다.
        /// </summary>
        public string TestBuyTech(long _clientId, string _techId)
        {
            GameObject p = FindPlayerGo(_clientId);
            ShopController shop = p != null ? p.GetComponent<ShopController>() : null;
            if (shop == null) return $"no ShopController({_clientId})";
            // 소유자 RPC라 남의 몸에서 부르면 서버가 버린다 — 조용히 버려지는 대신 그렇다고 말한다.
            if (!shop.IsOwner) return $"notOwner({_clientId}) — 소유자 RPC라 무시된다";
            if (!System.Enum.TryParse(_techId, out TechId id)) return $"unknown tech '{_techId}'";

            TechSlots slots = p.GetComponent<TechSlots>();
            int before = slots != null ? slots.LevelOf(id) : -1;
            TechDeny deny = shop.CanBuyNow(id);
            shop.Buy(id);
            return $"before={before}|local={deny}";
        }

        /// <summary>
        /// 로컬 플레이어 구매 — 소유자 RPC 제약이 없는 쪽. clientId를 모르는 스크립트가 쓴다.
        ///
        /// **판정을 글자로 돌려준다**(2026-09-13). 예전엔 <c>Enum.TryParse</c>가 실패하면
        /// 오타 난 테크 이름이 **아무 말 없이 지나갔고**, 서버 거절도 안 남았다.
        /// </summary>
        public string TestBuyTechLocal(string _techId)
        {
            ShopController shop = LocalComponent<ShopController>();
            if (shop == null) return "no ShopController";
            if (!System.Enum.TryParse(_techId, out TechId id)) return $"unknown tech '{_techId}'";

            TechSlots slots = LocalComponent<TechSlots>();
            int before = slots != null ? slots.LevelOf(id) : -1;
            TechDeny deny = shop.CanBuyNow(id);
            shop.Buy(id);
            return $"before={before}|local={deny}";
        }

        // ===== 사람 손 경로 (2026-09-09 감사 G-1·G-2·C-01) =====
        //
        // **여기 있는 것만이 「사람이 누르는 길」이다.** 아래 TestUseTech류는 ShopController를 직접
        // 부르므로 핫바의 상점 모드 분기를 통째로 건너뛴다 — 그래서 「고양이는 산 광분을 켤 수
        // 없다」가 qa_v48을 초록으로 통과했다. 입력 계약을 재는 스위트는 이쪽을 쓴다.

        /// <summary>
        /// QA: 핫바 숫자키 한 번(0-based) — <see cref="ShopHotbar.PressSlot"/>이 곧 사람이 타는 분기다.
        /// 이 프로세스의 **로컬 플레이어**에만 듣는다(핫바는 내 화면에만 있다).
        /// false = 핫바를 못 찾았다(씬 배선 사고) — 「눌렀는데 아무 일도 없었다」와 구분된다.
        /// </summary>
        public bool TestPressSlotKey(int _slot)
        {
            ShopHotbar bar = FindFirstObjectByType<ShopHotbar>();
            if (bar == null)
            {
                GameDebug.LogWarning("[TestManager] TestPressSlotKey — 씬에 ShopHotbar가 없다");
                return false;
            }
            bar.PressSlot(_slot);
            return true;
        }

        /// <summary>
        /// QA: 액티브 전용 키(Space) 한 번 — 상점 면제 역할(고양이)의 맨 왼쪽 액티브 칸을 쓴다.
        /// <c>true</c>는 「쓸 칸이 있어서 실제로 시도했다」이고, 켜졌는지는
        /// <see cref="TestGetCatFrenzyState"/>로 따로 본다(거절 사유는 토스트로 간다).
        /// </summary>
        public bool TestPressActiveKey()
        {
            ShopHotbar bar = FindFirstObjectByType<ShopHotbar>();
            if (bar == null)
            {
                GameDebug.LogWarning("[TestManager] TestPressActiveKey — 씬에 ShopHotbar가 없다");
                return false;
            }
            return bar.PressActiveKey();
        }

        /// <summary>
        /// QA: Enter 한 번 — <see cref="Core.EscapeRouter.PressEnterKey"/>가 곧 사람이 타는 분기다.
        /// 채팅 전송 · 확정 구매 · 채팅 열기의 갈림길을 실제로 지난다.
        /// false = 씬에 EscapeRouter가 없다(배선 사고).
        /// </summary>
        public bool TestPressEnterKey()
        {
            var router = FindFirstObjectByType<Core.EscapeRouter>();
            if (router == null)
            {
                GameDebug.LogWarning("[TestManager] TestPressEnterKey — 씬에 EscapeRouter가 없다");
                return false;
            }
            router.PressEnterKey();
            return true;
        }

        /// <summary>
        /// QA: 핫바 토스트가 **지금 화면에 있는 글자**(꺼져 있으면 빈 문자열).
        /// 거절 사유는 토스트로만 나오므로, 「상점 밖에서 빈 칸을 눌렀다」의 결과를 재는 유일한 길이다.
        /// </summary>
        public string TestGetHotbarToast()
        {
            ShopHotbar bar = FindFirstObjectByType<ShopHotbar>();
            if (bar == null) return "";
            System.Reflection.FieldInfo info = typeof(ShopHotbar).GetField(
                "m_Toast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var text = info != null ? info.GetValue(bar) as TMPro.TMP_Text : null;
            if (text == null || !text.gameObject.activeInHierarchy) return "";
            return text.text;
        }

        /// <summary>
        /// QA: 핫바 모드 줄이 화면에 그린 글자("상점 · 숫자키 = …    보유 n/5").
        /// 모드가 뒤집혔는지·전용 키 안내가 뜨는지를 **Model이 아니라 글자로** 잰다.
        /// </summary>
        public string TestGetHotbarModeText()
            => HudText(FindFirstObjectByType<ShopHotbar>(), "m_ModeLabel");

        /// <summary>
        /// QA: 핫바 칸들이 실제로 그린 글자 — "라벨#값#힌트"를 '|'로 잇는다(꺼진 칸은 건너뛴다).
        /// 쿨다운이 **화면에 보이는지**를 재는 유일한 길이다.
        /// </summary>
        public string TestGetHotbarSlots()
        {
            ShopHotbar bar = FindFirstObjectByType<ShopHotbar>();
            if (bar == null) return "";

            string[] fields = { "m_SlotLabels", "m_SlotCosts", "m_SlotHints" };
            var arrays = new TMPro.TMP_Text[fields.Length][];
            for (int f = 0; f < fields.Length; f++)
            {
                System.Reflection.FieldInfo info = typeof(ShopHotbar).GetField(
                    fields[f], System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                arrays[f] = info != null ? info.GetValue(bar) as TMPro.TMP_Text[] : null;
            }
            if (arrays[0] == null) return "";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < arrays[0].Length; i++)
            {
                TMPro.TMP_Text label = arrays[0][i];
                if (label == null || !label.gameObject.activeInHierarchy) continue;
                if (sb.Length > 0) sb.Append('|');
                sb.Append(label.text).Append('#')
                  .Append(TextAt(arrays[1], i)).Append('#')
                  .Append(TextAt(arrays[2], i));
            }
            return sb.ToString();
        }

        private static string TextAt(TMPro.TMP_Text[] _array, int _index)
        {
            if (_array == null || _index >= _array.Length || _array[_index] == null) return "";
            return _array[_index].text;
        }

        /// <summary>
        /// QA: 「지금 이 테크를 쓸 수 있나」의 **사유**(UseDeny 이름). 못 찾으면 "Missing".
        /// 판정은 <see cref="ShopController.CanUse"/> 단독이므로 여기서 규칙을 베끼지 않는다 —
        /// 2026-09-07에 「치즈 부족을 창고 상한이라 거짓말한」 자리가 정확히 여기다.
        /// </summary>
        public string TestGetUseDeny(long _clientId, string _techId)
        {
            GameObject p = FindPlayerGo(_clientId);
            ShopController shop = p != null ? p.GetComponent<ShopController>() : null;
            if (shop == null || !System.Enum.TryParse(_techId, out TechId id)) return MISSING;
            return shop.CanUse(id).ToString();
        }

        /// <summary>QA: 즉발 액티브 사용(요새·대시). 구매와 같은 소유자 RPC 제약.</summary>
        public void TestUseTech(long _clientId, string _techId)
        {
            GameObject p = FindPlayerGo(_clientId);
            ShopController shop = p != null ? p.GetComponent<ShopController>() : null;
            if (shop == null) { GameDebug.LogWarning($"[TestManager] TestUseTech — {_clientId} ShopController 없음"); return; }
            if (System.Enum.TryParse(_techId, out TechId id)) shop.Use(id);
        }

        /// <summary>로컬 플레이어 사용 — <see cref="TestBuyTechLocal"/>의 짝.</summary>
        public void TestUseTechLocal(string _techId)
        {
            ShopController shop = LocalComponent<ShopController>();
            if (shop == null) { GameDebug.LogWarning("[TestManager] TestUseTechLocal — ShopController 없음"); return; }
            if (System.Enum.TryParse(_techId, out TechId id)) shop.Use(id);
        }

        /// <summary>
        /// QA: 가장 가까운 상점 자리로 옮긴다(시간 결핍을 건너뛰고 구매만 볼 때).
        ///
        /// <see cref="BodyPush.ServerPush"/> 경유라 **호스트 인스턴스에서 불러야 한다** — 클론에서 부르면
        /// 서버가 아니라 아무 일도 안 일어난다.
        /// 그리고 ServerPush는 다시 **소유자 RPC**다: 클론의 몸은 왕복이 끝나야 실제로 움직인다.
        /// 그래서 <c>true</c>는 "보냈다"지 "도착했다"가 아니다 — QA는 잠깐 쉰 뒤
        /// <see cref="TestIsInShop"/>로 도착을 확인해야 한다.
        /// </summary>
        public bool TestWarpToNearestShop(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            if (p == null) return false;

            // 같은 질의는 한 군데서 — 봇 목적지와 같은 명부를 본다(계획 3 Task 6).
            // 부스 몸통이 막혀 있다(2026-09-19) — 가운데가 아니라 창구 앞으로 민다. 철문 상태는 안 가린다.
            if (!ShopSpot.NearestFront(p.transform.position, false, out Vector3 center)) return false;

            BodyPush push = p.GetComponent<BodyPush>();
            if (push == null) return false;

            // 높이는 건드리지 않는다 — 상점 마커는 바닥에 붙어 있어 y를 따라가면 몸이 바닥에 박힌다.
            Vector3 to = center - p.transform.position;
            to.y = 0f;
            push.ServerPush(to);
            return true;
        }

        // ===== 효과 계측 (테크 v3 계획 2a · qa_v47) =====
        // 여기 있는 것들은 전부 **읽기 전용**이다 — Model 값이 아니라 실제로 선 오브젝트·실제 HP를 센다.

        /// <summary>지금 HP(-1 = 못 찾음). NetworkVariable이라 어느 인스턴스에서 물어도 같은 값이다.</summary>
        public int TestGetHpOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            Combat.Health hp = p != null ? p.GetComponent<Combat.Health>() : null;
            return hp != null ? hp.Hp : -1;
        }

        /// <summary>
        /// 최대 HP(-1 = 못 찾음). <c>Health.MaxHp</c>는 스폰 뒤 <c>NetworkVariable</c>이라
        /// **어느 인스턴스에서 물어도 같은 값**이다(2026-09-08 — 두꺼운 가죽이 판 도중 올린다).
        /// </summary>
        public int TestGetMaxHpOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            Combat.Health hp = p != null ? p.GetComponent<Combat.Health>() : null;
            return hp != null ? hp.MaxHp : -1;
        }

        /// <summary>이 쥐가 세워 둔 창고 수 — 창고 Lv2(상한 2)가 실제로 늘었는지 본다.</summary>
        public int TestGetWarehouseCountOf(long _clientId)
        {
            return WarehouseProduction.CountOwnedBy((ulong)_clientId);
        }

        /// <summary>씬에 깔린 끈끈이 장판 수 — 10초 뒤 0으로 돌아가야 한다.</summary>
        public int TestGetStickyPatchCount()
        {
            return FindObjectsByType<StickyPatch>(FindObjectsSortMode.None).Length;
        }

        /// <summary>
        /// 이 플레이어에게 가장 가까운 요새의 최대 HP(-1 = 요새 없음). 요새 레벨(TechInfo P1)이
        /// 스폰 시 실제로 박혔는지 본다. <see cref="TestGetMaxHpOf"/>와 같이 복제되는 값이다.
        /// </summary>
        public int TestGetNearestFortressMaxHp(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            if (p == null) return -1;

            Combat.Health best = null;
            float bestSqr = float.MaxValue;
            foreach (FortressShield fort in FindObjectsByType<FortressShield>(FindObjectsSortMode.None))
            {
                float d = (fort.transform.position - p.transform.position).sqrMagnitude;
                if (d >= bestSqr) continue;
                Combat.Health hp = fort.GetComponent<Combat.Health>();
                if (hp == null) continue;
                bestSqr = d;
                best = hp;
            }
            return best != null ? best.MaxHp : -1;
        }

        /// <summary>
        /// 지금 걸을 수 있는 이속(-1 = 못 찾음) — 빠른 발(가산)·강화 스택·끈끈이 배율이 다 섞인 값.
        /// 계산은 <see cref="PlayerMovement.CurrentSpeed"/>가 단독으로 갖는다 — 여기서 식을 베끼면
        /// 갈라졌을 때 훅이 조용히 거짓말을 한다. 잠행(LShift)·바람 전직 배율은 그쪽에도 없다:
        /// 누르고 있는 동안만인 순간 값이라 QA가 재는 값이 아니다.
        /// </summary>
        public float TestGetMoveSpeedOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            PlayerMovement move = p != null ? p.GetComponent<PlayerMovement>() : null;
            if (move == null) return -1f;

            return move.CurrentSpeed;
        }

        // ===== 고양이 테크 (테크 v3 계획 2b · qa_v48) =====

        /// <summary>
        /// 광분이 켜져 있는가(못 찾으면 false). <c>CatFrenzy.Active</c>는 <c>NetworkVariable</c>이라
        /// **어느 인스턴스에서 물어도 같은 값**이다 — 호스트에서 클론 고양이를 물어도 참이다.
        /// </summary>
        public bool TestGetCatFrenzyActive(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            CatFrenzy frenzy = p != null ? p.GetComponent<CatFrenzy>() : null;
            if (frenzy == null)
            {
                // **bool은 「없음」을 말할 수 없다**(2026-09-09 감사 T9). 컴포넌트가 통째로
                // 빠져도 false가 나가 「꺼져 있다」 단언이 헛통과한다 — 그래서 크게 운다.
                // 세 값이 필요하면 <see cref="TestGetCatFrenzyState"/>를 쓴다.
                GameDebug.LogWarning($"[TestManager] CatFrenzy 없음(client {_clientId}) — false는 「꺼짐」이 아니다");
                return false;
            }
            return frenzy.Active;
        }

        /// <summary>
        /// QA: 광분 상태를 **세 값**으로 — "On"|"Off"|"Missing".
        /// bool 훅이 못 하는 「컴포넌트가 아예 없다」를 말한다(2026-09-09 감사 T9·E-06).
        /// </summary>
        public string TestGetCatFrenzyState(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            CatFrenzy frenzy = p != null ? p.GetComponent<CatFrenzy>() : null;
            if (frenzy == null) return MISSING;
            return frenzy.Active ? "On" : "Off";
        }

        /// <summary>
        /// 광분의 남은 쿨다운 초(-1 = 못 찾음). 서버 시각 기준의 목표 시각만 복제되고 남은 초는
        /// 각자 계산하므로(<see cref="Player.CatSkill"/>과 같은 규칙) 어느 인스턴스에서도 같다.
        /// </summary>
        public float TestGetCatFrenzyCooldown(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            CatFrenzy frenzy = p != null ? p.GetComponent<CatFrenzy>() : null;
            return frenzy != null ? frenzy.CooldownRemaining : -1f;
        }

        /// <summary>
        /// 예민한 귀 — **이 프로세스**의 가청 거리 배율(기본 1).
        ///
        /// 이 값은 복제되지 않는다: 소리는 각 클라가 제 프로세스에서 재생하므로 배율도
        /// 듣는 쪽에만 있다(<see cref="Audio.ListenerRange"/>). 그래서 QA는 **고양이를 쥔
        /// 인스턴스에서** 물어야 한다 — 호스트에서 클론 고양이의 배율을 물으면 늘 1이 나온다.
        /// </summary>
        public float TestGetListenerMultiplier()
        {
            return Audio.ListenerRange.Multiplier;
        }

        // ===== 매치 시드 (테크 v3 계획 3) =====

        /// <summary>
        /// 이 판의 시드(0 = 아직 안 굴렸다 — 로비·봇 실험실). **판을 두 번 열어 값이 바뀌는지**를 재는 훅이다.
        ///
        /// 복제되는 값이라 호스트·클론 어디서 물어도 같은 수가 나와야 한다 — 다르면 클라가
        /// 상점 자리·품절을 서버와 다르게 계산하고 있다는 뜻이다.
        /// </summary>
        public int TestGetMatchSeed()
        {
            Match.MatchRandom random = Match.MatchRandom.Current;
            return random != null ? random.Seed : 0;
        }

        /// <summary>
        /// QA 전용(서버) — **오늘의 품절을 걷어낸다**. 매치 시드를 0으로 되돌려
        /// <c>ShopController.SoldOutToday</c>가 빈 목록이 되게 한다.
        ///
        /// 왜 필요한가: 품절은 판마다 쥐 테크 둘을 막는데, 테크 **효과**를 재는 스위트(qa_v46·v47)는
        /// 열 종류를 전부 사야 한다 — 한 판으로는 구조적으로 못 잰다(2026-09-08 실측: qa_v47이
        /// 그 판에 품절이던 숨은 창고·빠른 발에서만 빨간 줄이 떴다). 주사위를 치워 효과만 재게 한다.
        /// **품절 자체의 검증은 qa_v50**이 따로 한다 — 여기서 끄는 것이 그 검증을 덮지 않는다.
        ///
        /// 상점 자리·직업권은 판 시작에 이미 놓였고 시드를 다시 안 읽으므로 그대로 남는다.
        /// </summary>
        public void TestClearSoldOut()
        {
            Match.MatchRandom random = Match.MatchRandom.Current;
            if (random == null) { GameDebug.LogWarning("[TestManager] MatchRandom 없음"); return; }
            random.DevClearSeed();
        }

        /// <summary>QA: 용도별 하위 시드(shop · soldout · ticket) — 셋이 실제로 갈렸는지 본다.</summary>
        public int TestGetMatchSubSeed(string _use)
        {
            Match.MatchRandom random = Match.MatchRandom.Current;
            if (random == null) return 0;

            int salt;
            switch ((_use ?? string.Empty).ToLowerInvariant())
            {
                case "shop": salt = Match.MatchRandom.SALT_SHOP; break;
                case "soldout": salt = Match.MatchRandom.SALT_SOLD_OUT; break;
                case "ticket": salt = Match.MatchRandom.SALT_TICKET; break;
                default:
                    GameDebug.LogWarning($"[TestManager] 모르는 시드 용도: {_use}");
                    return 0;
            }
            return Match.MatchRandom.SubSeed(random.Seed, salt);
        }

        /// <summary>
        /// QA: 오늘 못 사는 테크(쉼표로 이은 id 문자열, 없으면 빈 문자열).
        ///
        /// **로컬 플레이어의 ShopController가 답한다** — 서버가 뽑아 보낸 목록이 아니라
        /// 이 프로세스가 시드로 다시 계산한 값이라, 호스트와 클론의 답이 다르면 그것 자체가 버그다.
        /// </summary>
        public string TestGetSoldOutTechs()
        {
            NetworkManager nm = NetworkManager.Singleton;
            NetworkObject local = nm != null && nm.SpawnManager != null
                ? nm.SpawnManager.GetLocalPlayerObject() : null;
            ShopController shop = local != null ? local.GetComponent<ShopController>() : null;
            if (shop == null) { GameDebug.LogWarning("[TestManager] ShopController 없음"); return string.Empty; }

            var parts = new System.Collections.Generic.List<string>();
            System.Collections.Generic.IReadOnlyList<TechId> list = shop.SoldOutToday;
            for (int i = 0; i < list.Count; i++) parts.Add(list[i].ToString());
            return string.Join(",", parts);
        }

        // ───────────────────────────── 상점 팝업 (2026-09-19) ─────────────────────────────

        /// <summary>
        /// QA: 상점 팝업을 **E와 같은 길로** 토글한다 → 토글 뒤에 떠 있으면 true.
        /// 컴포넌트가 없으면(씬을 안 다시 구웠다) 경고를 남기고 false.
        /// </summary>
        public bool TestToggleShopPopup()
        {
            var popup = FindFirstObjectByType<UI.ShopPopup>();
            if (popup == null)
            {
                GameDebug.LogWarning("[TestManager] ShopPopup 없음 — Setup Networking Scene을 다시 돌렸는지 본다");
                return false;
            }

            popup.OnPressE();
            return popup.IsOpen;
        }

        /// <summary>QA: 팝업 카드들 — "테크:제목:살수있나"를 '|'로 잇는다. 안 떠 있으면 빈 문자열.</summary>
        public string TestGetShopPopupCards()
        {
            var popup = FindFirstObjectByType<UI.ShopPopup>();
            if (popup == null)
            {
                GameDebug.LogWarning("[TestManager] ShopPopup 없음 — 카드를 못 읽는다");
                return MISSING;
            }
            return popup.IsOpen ? popup.DevCardSummary() : string.Empty;
        }

        /// <summary>QA: 팝업의 그 기술 카드를 **실제로 누른다**(사람의 클릭과 같은 길). 눌렀으면 true.</summary>
        public bool TestClickShopCard(string _tech)
        {
            var popup = FindFirstObjectByType<UI.ShopPopup>();
            if (popup == null || !popup.IsOpen) return false;

            return popup.DevClick(_tech);
        }

        /// <summary>QA: 팝업 목록의 그 기술 줄에 **커서를 올린다**(사람과 같은 길 — 오른쪽 상세가 바뀐다). 올렸으면 true.</summary>
        public bool TestHoverShopRow(string _tech)
        {
            var popup = FindFirstObjectByType<UI.ShopPopup>();
            if (popup == null || !popup.IsOpen) return false;

            return popup.DevHover(_tech);
        }

        /// <summary>QA: 팝업 오른쪽 상세에 **보이는 글** — "이름|Lv 1/3|버튼 글|밑줄 글|사다리 가격들". 안 떠 있으면 빈 문자열.</summary>
        public string TestGetShopDetail()
        {
            var popup = FindFirstObjectByType<UI.ShopPopup>();
            if (popup == null) return MISSING;
            return popup.IsOpen ? popup.DevDetailSummary() : string.Empty;
        }
    }
}
#endif
