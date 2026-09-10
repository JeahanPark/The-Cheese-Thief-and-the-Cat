# 전체 검수 — 도메인 B: 플레이어·전투·카메라·연출 (2026-09-09)

**한 줄 결론: 순수 로직(피해·부채꼴·흡수·눈·귀 수학)은 EditMode가 촘촘히 덮지만, 그 로직이 몸에 실제로 붙어 도는지(원샷 애니메이션·눈/귀 배선·죽은 고양이·사망 경합·클라 권위 대시/잠행)는 아무도 안 본다 — 공백은 전부 「MonoBehaviour 층」에 몰려 있다.**

> **한계 명시**: 이번 검수는 **정적 분석만** 했다. 유니티 에디터·플레이모드·MPPM을 일절 쓰지 않았다(다른 감사 에이전트와 동시 실행). 「확인됨」은 코드·CSV·프리팹 YAML 원문으로 검증한 것, 「의심됨」은 코드에서 읽히지만 실행으로 확인하지 못한 것이다.
> 범위: `Assets/Scripts/Player/`(21) · `Combat/`(15) · `Cameras/`(7) · `Fx/`(2) · `Rendering/`(1) · `PlayerCat/PlayerMouse/Dog.prefab`.

---

## 1. 책임 지도

### 1-1. 상태의 주인

| 기능 | 상태 소유 | 복제 | 판정 주체 | 근거 |
|---|---|---|---|---|
| 이동 | **소유 클라** (`CharacterController`) | `ClientNetworkTransform` (소유자 권위) | 클라 | `PlayerMovement.cs:143` · `ClientNetworkTransform.cs:13` |
| 이속 강화 스택 | 서버 `NetworkVariable<int>` | 전원 | 서버 기록 · 소유자 적용 | `PlayerMovement.cs:35` |
| 외부 감속(끈끈이) | 서버 `Dictionary` → 합산 1개만 복제 | 전원 | 서버 | `PlayerMovement.cs:39,45` |
| 조준(카메라) | **로컬 전용** (Cinemachine 축) | 없음 | 클라 | `OrbitCameraController.cs:60` |
| 덮치기 | 서버 (`m_LastUseTime` 서버 필드) | 스택만 복제 | **서버** (`SendTo.Server`, `RequireOwnership`) | `CatPounce.cs:62-64` |
| 딱총 | 서버 (`m_LastUseTime`) | 티어만 복제 | **서버** (`SendTo.Server`, 소유권 검사 **없음**) | `MousePopgun.cs:138` |
| HP / 최대 HP | 서버 `NetworkVariable<int>` ×2 | 전원 | 서버 | `Health.cs:22,28` |
| 사망 | 서버 이벤트 `Died` | HP 복제로 간접 | 서버 | `Health.cs:84` |
| 부활 | **없음** (쥐 = 즉시 despawn, 고양이 = 로그만) | — | 서버 | `PlayerDeath.cs:33,41` |
| 잠행 | **소유 클라** `NetworkVariable`(Write=Owner) | 전원 | **클라** | `Sneak.cs:26` |
| 대시 | **소유 클라** (`Time.timeAsDouble` 로컬 쿨) | 보유 플래그만 | **클라** (서버 검증 없음) | `MouseDash.cs:116` |
| 전직(CatClass) | 서버 | 전원 | 서버 재검사 | `CatClass.cs:57` |
| 전직 스킬 / 광분 | 서버 (`ReadyAt`·`ActiveUntil` 서버 시각) | 전원 | 서버 | `CatSkill.cs:96` · `CatFrenzy.cs:97` |
| 치즈 방패 흡수 | 서버 (지갑·TechSlots) | 지갑만 | 서버 | `CheeseShield.cs:26` |
| 후추 도트 | **서버 전용 컴포넌트**(클라엔 아예 없음) | HP로만 | 서버 | `PepperDot.cs:16` |
| 가시 벽 반사 | 서버 `NetworkVariable<int>` | 전원 | 서버 | `ThornWall.cs:16` |
| 눈(EyeLook) | 그리기만 — 상태는 `GazeAim`이 갖는다 | `GazeAim.m_Packed`(short, Owner 쓰기) | 소유자 | `EyeLook.cs` · `GazeAim.cs` |
| 시선(GazeAim) | **2바이트 · 초당 12회 상한 · 변화 시에만**(2026-09-10) | 위와 같음 | 소유자 | `GazeAim.cs` |
| 귀(EarPerk) | **각 클라 로컬** (네트워크 0, static 명부) | 없음 | 클라 | `EarPerk.cs:37` |
| 피격 연출(HitFeedback) | 로컬 (HP 변화 구독) | — | 클라 | `HitFeedback.cs:41` |
| 차폐 페이드 | 로컬 | — | 클라 | `OccluderFader.cs:29` |
| 어둠 후처리 | **static** `DarknessFeature.Strength` | 없음 | 보는 클라 | `DarknessFeature.cs:26` |

### 1-2. 피해 계산 경로 (고양이 덮치기)

```
base(60, PlayerInfo.csv)
  -> + 스택×10                          ShopLogic.PounceDamage           [가산]
  -> × max(광분배율, 불배율)              CatClassLogic.PounceMultiplier   [max — 곱 아님]
  -> Health.TakeDamage
       -> IDamageAbsorber(치즈 방패)      CheeseShieldLogic.Absorb         [감산, HP 앞에서]
       -> Mathf.Max(0, hp - dmg)         CombatLogic.ApplyDamage
  -> dealt(실피해)로  (1) 장난기 적립  (2) 가시 벽 반사 트리거              [별개 경로]
```

| 항목 | 합성 방식 | 값(현재 CSV) |
|---|---|---|
| 스택 | **가산** +10/스택, 상한 3 | 60 → 90 |
| 광분 × 불 | **max** (곱 아님) | max(2.5, 1.35) = **2.5** |
| 최대 한 방 | 90 × 2.5 = **225** | |
| 치즈 방패 | **감산** (비율만큼 HP→치즈, 1HP=3치즈). 「막아도 죽는 타격」이면 흡수 0 | Lv3 0.6 |
| 가시 벽 | **정액 반사** 25, 실피해 0이면 반사 0 | |
| 후추 도트 | **별개 틱** (스택 × PepperDotDamage 2/s) | Lv3 4중첩 6초 |
| 용사쥐 | **곱** ×1.5 (딱총만) | |

**한 방 경계선 (계산으로 도출, 실행 미확인)**

| 표적 | HP | 90(스택만) | 121(불) | 135(광분 Lv1) | 225(광분 Lv3) |
|---|---|---|---|---|---|
| 쥐(방패 없음) | 100 | 생존 10 | **즉사** | **즉사** | **즉사** |
| 쥐(방패 Lv3 · 치즈 충분) | 100 | 생존 64 | 생존 52 | 생존 46 | 생존 10 |
| 요새 Lv3 | 1200 | — | — | — | 생존(테스트가 지킴) |

---

## 2. 이미 검증되는 것

| 무엇 | 어디서 | 단언 내용 |
|---|---|---|
| 피해 감산·0클램프·음수 무시 | `CombatLogicTests.cs:11-25` | `ApplyDamage(100,60)=40` · `(30,60)=0` · 음수 데미지 무시 |
| 사망 판정 | `CombatLogicTests.cs:29` | `IsDead(0)=true`, `IsDead(1)=false` |
| 쿨다운 | `CombatLogicTests.cs:36-44` | 경계값이 정확히 `>=` |
| 부채꼴 | `CombatLogicTests.cs:48-72` | 사거리 밖 · 뒤 · **높이차 무시** |
| 피아 식별 | `CombatLogicTests.cs:79-124` | 제3세력 양쪽 타격 · 쥐끼리 O · 제 구조물 X · 중립 노획물 O · 고양이끼리 X |
| 치즈 방패 산술 | `CheeseShieldLogicTests.cs:9-57` | 30%/rate3 분할 · 지갑 부족 시 부분 지불 · **막아도 죽으면 0 청구** · 비율 1 초과 클램프 |
| 후추 도트 총량 보존 | `PepperDotLogicTests.cs:9-51` | 스택 캡 · dps×t(±1) · 만료 |
| 광분 사용 조건·배율 | `CatFrenzyLogicTests.cs:9-22` | 미보유/쿨/중복 거절 · 꺼지면 1배 · 표 사고(0) → 1배 |
| **광분×불 = max, 요새 Lv3 원샷 금지** | `TechLogicTests.cs:145-168` | `combined == max(frenzy,fire)` **그리고** `burst(225) < 요새 Lv3 HP(1200)` |
| 전직 문턱 | `CatClassLogicTests.cs:15-83` · `qa_v42.py:35-102` | 누적 획득 300 · 1회만 · 강화만으론 안 열림 · 스킬 켜짐/쿨 복제 |
| 이동 입력 정규화 | `MovementInputTests.cs:10-56` | 대각 1로 클램프 · yaw 회전 |
| 카메라 각도 | `CameraOrbitLogicTests.cs:10-65` | 피치 클램프 · yaw 랩 · 비반전 |
| 차폐 페이드 집합 연산 | `OcclusionFadeLogicTests.cs:11-90` | 캐스트 원점/거리 · 재스왑 없음 · 전부 복구 |
| 봇/사람 조종 구분 | `LocalControlLogicTests.cs:14-45` | 호스트의 봇이 내 입력을 안 먹는다 |
| 애니메이션 속도→상태 | `AnimationStateLogicTests.cs:11-31` | Idle/Walk/Run 경계 |
| **원샷 상태 이름이 컨트롤러에 실존** | `AnimationStateLogicTests.cs:61-95` | `AC_Cat`·`AC_Mouse` 원문에서 6종 이름 줄 단위 대조 |
| 눈 수학 | `EyeLookLogicTests.cs:16-153` | 각도 클램프 · 뒤집힘 방지 · 프레임률 무관 · 깜빡임 0→1→0 |
| 귀 수학 | `EarPerkLogicTests.cs:17-153` | 좌우 부호 · 최대각 클램프 · 유지창 · 가까운 소리 승 · 먼 귀 0.45배 |
| 잠행 복제·발소리 반경 | `qa_v36.py:105-124` | 원격이 관측 · 무음이 아니라 좁아짐(고양이·쥐 양쪽) |
| 쥐 사망 = 즉시 탈락 | `qa_v36.py:245` | HP 0 → despawn → 관전 |
| 카메라 거리 상수 | `qa_v36.py:269` | 벽 근처에서도 spread < 0.35 |
| 가시 벽 반사 실동작 | `qa_v47.py:308` | 요새 친 고양이가 25 되맞음 |
| 치즈 방패 실동작 | `qa_v47.py:261` | HP 덜 잃고 치즈가 빠짐 |
| 두꺼운 가죽·배고픔·광분 실동작 | `qa_v48.py:179-299` | 최대 HP 1450 · 장난기 ×1.6 · 광분 ×2.5 · 10초 해제 · **광분+불이 ×5가 아님** |
| 불 전직 원샷 | `qa_v42.py:86` | 스킬 중 덮치기 한 방에 쥐가 눕는다(**의도된 동작으로 고정됨**) |

---

## 3. 검증 공백 (핵심 산출물)

| 심각도 | 무엇 | 왜 위험한가 | 근거 |
|---|---|---|---|
| **상** | **원샷 애니메이션 6종이 실제로 재생되는지 읽는 훅이 0개** | `CrossFadeInFixedTime`은 없는 상태 이름에 **에러도 경고도 안 낸다**. 이름 대조 테스트는 「컨트롤러에 이름이 있다」까지만 보고, `PlayOneShot`이 실제로 걸렸는지·`_oneShotUntil` 잠금이 도는지는 아무도 안 본다. Animator를 읽는 TestManager 훅이 **한 개도 없다**(grep 0건) | `PlayerAnimationDriver.cs:50-58` · `Assets/Scripts/Testing/*.cs`에 `Animator` 문자열 0건 |
| **상** | **죽은 고양이가 계속 때린다** — `PounceRpc` 입구에 자기 사망 검사가 없다 | 반사 사망은 루프 **안**에서만 `break`한다(리뷰 2에서 고친 자리). 다음 클릭은 그대로 통과 → HP 0 고양이가 무한히 덮치고 장난기까지 번다. 고양이는 despawn되지 않는다 | `CatPounce.cs:64-66`(입구에 `self.IsDead` 없음) vs `CatPounce.cs:125`(루프 안에만) · `PlayerDeath.cs:33` |
| **상** | **한 방 경계선(쥐 100 HP)을 못 박는 테스트가 없다** | 요새 Lv3 불변식만 있고(`burst < 1200`) **쥐 쪽 선은 없다**. 광분 Lv1(×1.5)만 켜도 90→135로 만피 쥐가 즉사한다. 「치즈 방패 없는 쥐는 항상 2방 이상」 같은 규칙이 있는지조차 어디에도 안 적혀 있다 | `TechLogicTests.cs:145`(요새만) · `PlayerInfo.csv` Mouse MaxHp=100 |
| **상** | **대시가 서버 검증 0** — 쿨다운·이동 전부 소유 클라 | `TryDash`는 `IsOwner`에서만 돌고 서버 RPC가 아예 없다(소리 Rpc만 있다). 쿨은 `Time.timeAsDouble`(클라 시계). 변조 클라가 무한 대시해도 서버가 모른다. 덮치기·총은 `SendTo.Server`인데 대시만 다르다 | `MouseDash.cs:110-124` |
| **상** | **`MousePopgun.FireRpc`에 `RequireOwnership`이 없다** | `CatPounce`는 「타인 고양이 쿨다운 강제 소모 차단 — 리뷰 I2」로 `RequireOwnership = true`를 명시했는데 총은 안 붙어 있다. 남의 쥐 쿨다운을 소모시키거나 남의 몸에서 총을 쏘게 할 여지 | `MousePopgun.cs:138` vs `CatPounce.cs:62` |
| 중 | **잠행이 클라 권위** — `NetworkVariableWritePermission.Owner` | 변조 클라가 `Sneaking=false`로 두고 감속 없이 달리면서 발소리 반경만 4m로 유지할 길이 열린다(속도도 클라가 정한다). 서버 재검증 없음 | `Sneak.cs:26` · `PlayerMovement.cs:176` |
| 중 | **`EyeLook`·`EarPerk` 훅은 있는데 이를 부르는 QA 스크립트가 0개** | `TestGetEyeGaze`·`TestGetEarState`·`TestGetEarStateOfRole`·`TestBlinkEyes`가 다 있는데 `tools/wesqa/tests/`에서 아무도 안 부른다(`Eye`/`Ear` 매치는 전부 `CatEar` 테크였다). 씬 재생성이 프리팹을 **매번 새로 굽는데**(`CreatePlayerPrefab`가 `new GameObject`부터 시작) 배선이 빠져도 알 길이 없다 | `TestManager.View.cs:286,320,367,383` · `qa_v*.py` grep 0건 |
| 중 | **「귀는 없던 정보를 만들지 않는다」 불변식을 자동으로 지키는 것이 없다** | 이 기능의 **존재 이유**인데(고양이가 잠행하면 쥐 귀가 한 톨도 안 움직여야 한다) 순수 함수 테스트는 각도 계산만 본다. 실제 판정은 `FootstepRangeOf`(내 몸/남의 몸 분기) + `BotSenseLogic.CanHear`에 있고 그 조합을 재는 것이 없다. 「예민한 귀」를 산 사람의 화면에서 **남의 귀가 부풀어 도는지**도 미검증 | `EarPerk.cs:279-296` · `EarPerkLogicTests.cs`(수학만) |
| 중 | **사망·despawn 경합을 재는 테스트가 0** | 과거에 「반사 사망 후 계속 때림」이 실제로 있었다(핸드오프 `1368999`). 지금 남은 경합 후보: (1) 죽은 고양이 재공격(위) (2) `Health.Died` → `PlayerDeath.Despawn(true)` 동기 실행 중에 `CatPounce` 루프가 이미 잡아 둔 `Health` 배열을 계속 순회 (3) `PepperDot`가 붙은 채 despawn | `CatPounce.cs:99-160` · `PlayerDeath.cs:41` |
| 중 | **`Health.ServerRaiseMaxHp`가 스폰 전 `m_Hp` 쓰기를 막지 않는다** | `m_MaxHpNet`은 `if (IsSpawned)`로 감쌌는데 바로 다음 줄 `m_Hp.Value` 쓰기는 안 감쌌다. `ThornWall`이 같은 함정 때문에 `m_PendingReflect`를 따로 둔 것과 대조된다. 지금 호출부(`CatHide`)는 스폰 후라 안 터지지만, 다른 곳에서 부르면 조용한 NGO 경고 + 값 유실 | `Health.cs:120-128` vs `ThornWall.cs:24-33` |
| 중 | **`OccluderFader`의 레이어 마스크가 코드 기본값(Default=1)** | `m_ObstacleLayers = 1`이 코드 기본값이고 프리팹/씬이 덮는지 미확인. 맵 구조물이 Default가 아닌 레이어에 있으면 **아무것도 안 흐려지고 에러도 없다**. 훅(`TestGetFadedOccluderCount`)은 있으나 wesqa 호출부 없음 | `OccluderFader.cs:26` |
| 중 | **`SpectatorFallback`이 봇/적을 관전 대상으로 고른다** | `FindFirstObjectByType<PlayerMovement>()` — 역할·소속·사람 여부를 안 본다. 탈락한 쥐가 **고양이 시점**으로 붙을 수 있고, 살아 있는 팀원에게 정보가 샌다 | `SpectatorFallback.cs:44` |
| 하 | **`DarknessFeature.Strength`가 static인데 리셋 경로가 없다** | 도메인 리로드를 끄고 플레이하면 이전 판의 값이 남는다. `UiFocus.ResetForNewSession()` 같은 대응이 없다. 화면이 어두운 채로 시작해도 에러가 없다 | `DarknessFeature.cs:26` · `CatSkillView`에 `OnDisable` 없음 |
| 하 | **`Fx/` 두 파일에 테스트·훅이 0** | `FxAttachToBone`은 본 이름 문자열로 찾고 실패 시 경고만 낸다. 어느 프리팹이 이걸 쓰는지 자체가 미확인 | `FxAttachToBone.cs:41` |
| 하 | **`Dog.prefab`의 전투 경로가 도메인 B 테스트에 없다** | `DogBite`·`DogDeath`는 `Health`/`RoleTag`/`HitFeedback`을 공유하는데 `qa_v40`(강아지)만 있고 EditMode는 `DogAiLogicTests`(길찾기 판단)뿐. `RoleId.None`이 `CanAttack`에서 양쪽 표적이 되는 규칙은 덮이지만 실제 피해·사망은 미검증 | `Dog.prefab` 컴포넌트 목록 |

---

## 4. 발견한 결함

| 심각도 | 무엇 | 근거 | 상태 |
|---|---|---|---|
| **상** | **죽은 고양이가 계속 덮치고 장난기를 번다.** `PounceRpc` 입구에 자기 `Health.IsDead` 검사가 없다 | `CatPounce.cs:64` — 쿨다운만 본다. `self.IsDead` 검사는 `:125`의 루프 안에만 있다 | **확인됨**(코드) |
| **상** | `MousePopgun.FireRpc`가 `RequireOwnership`을 안 건다 — 형제 무기(`CatPounce`)는 건다 | `MousePopgun.cs:138` `[Rpc(SendTo.Server)]` vs `CatPounce.cs:62` `[Rpc(SendTo.Server, RequireOwnership = true)]` | **확인됨**(코드) |
| 중 | **총알마다 머티리얼이 샌다.** `line.material = new Material(...)`로 만든 인스턴스는 `Destroy(go, 0.12f)`로 회수되지 않는다 | `MousePopgun.cs:194,203` | **확인됨**(코드) |
| 중 | 같은 줄의 `Shader.Find("Sprites/Default")`가 null이면 **마젠타 머티리얼**이 조용히 생긴다(CLAUDE.md 함정 #6) | `MousePopgun.cs:194` — 널 검사 없음 | **의심됨**(빌드에서만 재현) |
| 중 | **잠행이 채팅/옵션 창에서 켜진 채 고착된다.** `Sneak.Update`가 `BlocksGameplay`에서 early-return이라 `m_Sneaking`이 마지막 값 그대로 남는다 | `Sneak.cs:55` — 다른 입력들은 값을 0으로 두고 계속 도는데 여기만 `return` | **확인됨**(코드) |
| 중 | **눈(EyeLook)이 피격 적색 플래시에서 빠진다.** `HitFeedback.Awake`가 렌더러를 캐시하는데, 프리팹 컴포넌트 순서상 `HitFeedback`이 `EyeLook`보다 **앞**이라 런타임에 얹히는 흰자/눈동자/눈꺼풀 렌더러가 목록에 없다 | `HitFeedback.cs:43` `GetComponentsInChildren` in `Awake` · `PlayerCat.prefab` 컴포넌트 순서(HitFeedback … EyeLook) | **의심됨**(Awake 순서 = 컴포넌트 순서라는 전제) |
| 중 | `Health.ServerRaiseMaxHp`가 `m_Hp` 쓰기를 `IsSpawned`로 안 감쌌다(바로 위 `m_MaxHpNet`은 감쌌다) | `Health.cs:126-127` | **확인됨**(코드) |
| 하 | `SightLine.IsBlocked(Vector3,Vector3)`이 주석과 다르게 동작한다 — 「가장 가까운 것이 표적이면 보인다」 규칙이 없고 **첫 히트면 무조건 차폐**다. 봇의 「저기로 가면 안 보이나」 판단이 Transform 판정보다 보수적 | `SightLine.cs:57` vs `:39` | **확인됨**(코드) |
| 하 | `PlayerAnimationDriver.Update`가 `_animator == null`로 early-return할 때 `_lastPosition`을 갱신하지 않아, 복귀 프레임에 거대한 속도가 한 번 튄다 | `PlayerAnimationDriver.cs:38` | **확인됨**(코드) |
| 하 | `SpectatorFallback`이 봇·적 아무나 붙잡는다(3절과 동일) | `SpectatorFallback.cs:44` | **확인됨**(코드) |
| 하 | (도메인 밖 참고) `CatSkillView.cs:145` `_me.GetComponent<RoleTag>() is { } tag` — 패턴 매칭은 `?.`와 같이 Unity의 `==` 오버로드를 **건너뛴다**(규칙 9 정신 위반). 파괴된 컴포넌트가 통과한다 | `Assets/Scripts/UI/CatSkillView.cs:145` | **확인됨**(코드) |

### 4-1. 위반이 **없던** 것 (확인)

| 점검 | 결과 |
|---|---|
| 규칙 9 (`?.`/`??`를 `UnityEngine.Object`에) | **위반 0.** 도메인 B의 `?.`는 전부 C# 이벤트(`HpChanged`·`Died`)와 `CancellationTokenSource`뿐 |
| 입력 가드 `UiFocus.BlocksGameplay` | 게임플레이 입력 **전부에 걸려 있다** — `PlayerMovement:148` · `MouseDash:97` · `Sneak:55` · `CatSkill:71` · `CatPounce:53` · `MousePopgun:47`. `OrbitCameraController`만 없는데 이는 **의도**다(`CursorPolicyLogic` 주석: 채팅 중 커서를 잠근 채 두는 이유) |
| ESC 단독 라우팅 | 도메인 B에 `escapeKey` 읽기 0건 |
| 레거시 `Input.*` | 0건 (전부 Input System) |
| `CharacterController` 오브젝트에 transform 직접 세팅 | `BodyPush`·`PlayerMovement.OnNetworkSpawn`만 — **둘 다 CC를 끄고 옮긴다**(정당한 텔레포트) |
| `EyeLook`·`EarPerk` 프리팹 배선 | **양쪽 프리팹에 실재.** `PlayerCat.prefab:425/442` · `PlayerMouse.prefab:606/623` |
| 씬 재생성 시 유지 | `PlayerPrefabFactory`가 매번 `root.AddComponent<EyeLook>()`(`:143`) + `EarPerk`(`:147`)를 굽는다 → **유지된다.** 단 손으로 고친 값은 `EyeAnchorBaker`가 덮어쓴다(함정 #2의 의도된 방향) |

---

## 5. CSV ↔ 프리팹 대조표

**어긋난 것: 0건.** (`PlayerInfo.csv` ↔ `PlayerCat.prefab` / `PlayerMouse.prefab` YAML 원문 대조)

| 값 | CSV(Cat) | 프리팹(Cat) | CSV(Mouse) | 프리팹(Mouse) | 일치 |
|---|---|---|---|---|---|
| MaxHp | 1000 | `m_MaxHp: 1000` | 100 | `m_MaxHp: 100` | O |
| MoveSpeed | 7.4 | `m_MoveSpeed: 7.4` | 6.6 | `6.6` | O |
| RunThreshold | 3.2 | `m_RunThreshold: 3.2` | 4.2 | `4.2` | O |
| TurnSpeedDegPerSec | 720 | `720` | 720 | `720` | O |
| Fov | 70 | `m_CameraFov: 70` | 55 | `55` | O |
| CamDistance | 3 | `m_CameraDistance: 3` | 3 | `3` | O |
| CcHeight / CcRadius | 0.9 / 0.35 | `0.9` / `0.35` | 0.5 / 0.22 | `0.5` / `0.22` | O |
| PounceDamage | 60 | `m_Damage: 60` | — | (컴포넌트 없음) | O |
| PounceCooldown | 2 | `m_CooldownSeconds: 2` | — | — | O |
| PounceRange | 2.8 | `m_Range: 2.8` | — | — | O |
| PounceHalfAngleDeg | 60 | `m_HalfAngleDeg: 60` | — | — | O |
| FootstepRangeMeters | 20 | `m_NormalRange: 20` | 22 | `22` | O |
| SneakFootstepRangeMeters | 4 | `m_SneakRange: 4` | 6 | `6` | O |
| **StrideMeters**(파생) | 7.4 ÷ 2.5 = 2.96 | `m_StrideMeters: 2.96` | 6.6 ÷ 3.4 = 1.9411765 | `1.9411764` | O |
| Role | Cat | `m_Role: 2` | Mouse | `m_Role: 1` | O |

> **과거 사고(고양이 이속 8.6→7.4에 `m_StrideMeters`가 안 따라옴)는 현재 재발하지 않았다.** 파생값이 `FootstepLogic.StrideOf(info.MoveSpeed, 종별 걸음/초)`로 굽히므로 자동 추종한다(`PlayerPrefabFactory.cs:160`). 다만 **이 대조를 자동으로 하는 테스트는 없다**(→ B-10).

**대조 불가(주의)**

| 값 | 상황 |
|---|---|
| `EyeLook`의 `m_EyeCenterLocal`·`m_EyeAxisLocal`·`m_ScleraRadiusLocal`·`m_LidRadiusLocal` | **CSV에 없다** — `EyeAnchorBaker`(에디터 상수)가 원본. 고양이 `{0.00106,0.00179,0.00109}` / axis `{0.5,0.476,0.724}` / 0.00045, 쥐 `{0.00132,0.001,0.00034}` / 0.00055. 대조할 CSV 열이 없으므로 **회귀를 잡는 것은 스크린샷뿐**이다 |
| `EarPerk` | 굽는 값이 0(본 이름만). 각도·유지·속도는 런타임에 `BalanceInfo`에서 직접 읽음 → CSV 변경이 씬 재생성 없이 반영된다 |

---

## 6. 제안 테스트 케이스

우선순위: **P0** = 지금 없는 불변식 · **P1** = 회귀가 실제로 났던 자리 · **P2** = 보강

| ID | 무엇을 검증 | 종류 | 필요한 훅 | 우선 |
|---|---|---|---|---|
| **B-01** | **죽은 고양이는 더 못 때린다.** 요새 Lv3(반사 25)을 고양이 HP가 25 이하가 될 때까지 치게 한 뒤, 고양이 HP=0을 확인하고 덮치기를 **다시** 호출 → 요새 HP·고양이 장난기가 **둘 다 안 변해야** 한다 | wesqa (`qa_v47` 확장) | `TestGetLocalHp` · `TestGetNearestFortressMaxHp` · `TestGetHpOf` + **신규 `TestDevPounce`** | **P0** |
| **B-02** | **쥐 한 방 경계선.** `PounceDamage(60, MAX_STACKS) × max(CatFrenzy Lv1..3, Fire)`를 `PlayerInfo(Mouse).MaxHp`와 견주어 「어느 조합이 즉사인가」를 표로 못 박는다(현재 값은 광분 Lv1부터 즉사 = 의도로 고정) | EditMode (`TechLogicTests` 옆) | 없음 — 전부 순수 함수 | **P0** |
| **B-03** | **치즈 방패가 즉사를 막는 하한.** 방패 Lv3 + 치즈 405 이상인 쥐는 225 한 방에 **살아남아야** 한다(`CheeseShieldLogic.Absorb(225, 0.6, 3, 405, 100)` → hp 90). CSV를 만졌을 때 이 선이 깨지면 잡는다 | EditMode | 없음 | **P0** |
| **B-04** | **원샷 애니메이션 6종이 진짜 걸린다.** 각 동작(대시·끈끈이·요새·광분·구매·채굴)을 QA 훅으로 실행한 직후 Animator의 현재/다음 상태가 그 상태와 일치 | wesqa | **신규 `TestGetAnimState()`** — `PlayerAnimationDriver`에 `CurrentState` 노출 + `Animator.GetCurrentAnimatorStateInfo(0)`. **지금은 훅이 0개라 이 테스트를 쓸 수 없다** | **P0** |
| **B-05** | **총 RPC 소유권.** 쥐 A의 인스턴스에서 쥐 B의 `MousePopgun.FireRpc`를 부르면 B의 쿨다운이 **안 닳아야** 한다 | wesqa (3인) | `TestGetWeaponCooldownOf`(신규) 또는 발사 횟수 카운터 | **P0** |
| **B-06** | **잠행 → 귀 불변식.** 고양이가 쥐 8m 앞에서 (1) 뛴다 → 쥐 `perk>0.5`·`stepAudible=1` (2) 잠행한다 → `perk≈0`·`stepAudible=0`. 같은 상황에서 덮치기(일회성 소리)는 잠행 중에도 `kind=shot`으로 들려야 한다 | wesqa (신규 `qa_v51`) | **이미 있다** — `TestGetEarState`(`turn/perk/hearing/kind/stepAudible/turnR/turnL`) | **P0** |
| **B-07** | **「예민한 귀」가 남의 귀를 부풀리지 않는다.** 고양이가 CatEar Lv3(×2)을 산 뒤 **쥐 클라에서 읽은** 쥐 귀의 `stepAudible`이 배율 없이 판정돼야 한다(내 몸 `HeardRange` vs 남의 몸 `CurrentRange` 분기) | wesqa (3인) | `TestGetEarState`(내 몸) + `TestGetEarStateOfRole`(남의 몸) + `TestGetListenerMultiplier` | P1 |
| **B-08** | **눈이 조준을 따라간다 — 남의 화면에서도**(2026-09-10 개정). 내 몸: 카메라를 90도 돌린 뒤 `TestGetEyeGaze`의 `aim=gaze:cam`이고 눈동자 월드 위치가 이동. **남의 몸: 다른 인스턴스에서 `TestGetOtherEyeGaze`의 `packed`가 바뀌고 눈동자가 따라 돈다** — 예전 기대값(`aim=body(notMine)` = 안 샌다)은 **폐기**다. 그건 「남의 눈이 죽었다」를 통과로 채점하던 항목이었다 | wesqa | `TestGetEyeGaze` · `TestGetOtherEyeGaze` · `TestCaptureOtherHeadShot` · `TestGetGazeWrites` | P1 |
| **B-09** | **씬 재생성 후에도 눈·귀가 붙어 있다.** 두 프리팹에 `EyeLook`·`EarPerk`가 있고 `m_EyeCenterLocal`이 `EyeAnchorBaker` 상수와 일치 | EditMode (에셋 로드형, `MapValidatorTests` 패턴) | 없음 — `AssetDatabase` | P1 |
| **B-10** | **CSV↔프리팹 자동 대조.** `PlayerInfo.csv`의 14개 열이 두 프리팹에 구워진 값과 같은지, `m_StrideMeters == StrideOf(MoveSpeed, 걸음/초)`인지 | EditMode | 없음 — `AssetDatabase` + `InfoManager` | **P0**(지금 손으로만 막고 있다) |
| **B-11** | **반사 사망 경합.** 부채꼴 안에 요새 Lv3 두 채 + 쥐 하나를 두고, 첫 요새 반사로 고양이가 죽는 상황에서 **두 번째 요새·쥐가 피해를 안 받아야** 한다 | wesqa | `TestGetHpOf`(다중) + `TestDevPounce`(신규) | P1 |
| **B-12** | **끈끈이 겹침 후 이속 복구.** 장판 2장을 겹쳐 밟았다 하나가 만료 → `TestGetMoveSpeedOf`가 남은 장판의 배율이어야 하고, 둘 다 만료되면 기본값으로 **정확히** 돌아와야 한다 | wesqa (`qa_v47` 8번 확장) | `TestGetMoveSpeedOf`(있음) | P1 |
| **B-13** | **대시 쿨다운 서버 검증**(현재 없음 → 서버 RPC로 옮기는 수정이 선행). 옮긴 뒤 클라가 연타해도 쿨 안에 두 번 안 나가야 한다 | wesqa | `TestUseTechLocal`(있음) + 이동 거리 측정 | P2(수정 선행) |
| **B-14** | **차폐 페이드가 실제로 걸린다.** 벽 뒤로 카메라를 돌린 뒤 `TestGetFadedOccluderCount() > 0`, 벽에서 떨어지면 0으로 복구 | wesqa | `TestSetCameraAngles` · `TestGetFadedOccluderCount`(둘 다 있음) | P2 |
| **B-15** | **관전 대상이 같은 편 사람이다.** 쥐가 탈락하면 `SpectatorFallback.Current`가 **고양이도 봇도 아니어야** 한다 | wesqa | `TestGetSpectateTarget`(신규) | P2 |
| **B-16** | **어둠 후처리 잔류 없음.** 어둠 스킬 지속이 끝나고 2초 뒤 `DarknessFeature.Strength == 0` | wesqa | `TestGetDarkStrength`(신규) | P2 |
| **B-17** | **잠행이 채팅 중 고착되지 않는다.** Shift를 누른 채 채팅을 열고 Shift를 떼면 `TestGetSneaking()`이 false가 돼야 한다 | wesqa | `TestTypeChat` · `TestGetSneaking` · `TestSetSneak`(전부 있음) | P2 |
| **B-18** | **강아지 전투.** 고양이·쥐 양쪽이 강아지를 때릴 수 있고 HP 0이면 죽는다 + 강아지에겐 장난기가 안 붙는다(`BountyExempt`) | wesqa (`qa_v40` 확장) | `TestDamageDog` · `TestGetDogHp`(있음) + `TestGetEarnedOf` | P2 |

### 6-1. 새로 만들어야 하는 훅

| 훅 | 이유 | 막히는 테스트 |
|---|---|---|
| `TestGetAnimState()` | Animator를 읽는 훅이 **0개** — 원샷 6종 전체가 미검증 | B-04 |
| `TestDevPounce()` | `CatPounce.DevPounce()`는 있는데 TestManager 경유가 없다 | B-01 · B-11 |
| `TestGetWeaponCooldownOf(clientId)` | 소유권 위반을 재는 유일한 관측점 | B-05 |
| `TestGetSpectateTarget()` | 관전 대상 검증 | B-15 |
| `TestGetDarkStrength()` | static 잔류 검증 | B-16 |

---

## 부록 — 이번 검수의 사각지대

| 못 본 것 | 왜 |
|---|---|
| 플레이모드 실제 동작 전부 | 에디터 사용 금지 |
| 애니메이터 컨트롤러의 **전이 조건**(이름만 대조했다) | `.controller` 원문 미해석 |
| 씬(`Networking.unity`)에 배선된 `OccluderFader` 레이어 마스크 · `TestBotManager` 프리팹 참조 실값 | 씬 YAML 미확인 |
| `Fx/` 두 컴포넌트를 실제로 쓰는 프리팹 | 참조 추적 미실시 |
| 물리 레이어 매트릭스(플레이어 layer 9 ↔ 캐스트 마스크) | 프로젝트 설정 미확인 |
