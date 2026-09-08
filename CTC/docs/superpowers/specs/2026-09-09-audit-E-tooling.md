# 전체 검수 — 도메인 E: 개발·테스트 기능 자체 (2026-09-09)

> **한 줄 결론 — 훅 하나하나는 대체로 믿을 수 있는데, 훅을 부르는 다리(`InvokeBridge`)와 스위트의 채점 방식을 못 믿는다.**
> 인자 이름을 틀리면 에러 없이 **0**이 들어가고(= 호스트 clientId · 거리 0m), 이름이 안 맞아도 **개수만 맞으면 순서로** 꽂힌다.
> 2026-08-09에 이것 때문에 「엉뚱한 쥐를 가둔」 사고가 났고, **그 구멍은 지금도 그대로다.**
> 그리고 **QA 스위트 7개는 절반이 실패해도 종료 코드 0**이라 사람이 stdout을 읽지 않으면 실패를 모른다.
>
> 범위: `Assets/Scripts/Testing/`(15) · `Assets/WesQA/` · `tools/wesqa/`(스위트 14 + 러너 3) ·
> `Assets/Scripts/Tests/EditMode/`(66파일 · `[Test]` 628개) · `Assets/Scripts/Editor/`(54) 중 개발 도구 ·
> `Bots/TestBotManager`·`BotLab*`.
>
> **정적 분석만이다** — 지시로 유니티 에디터·플레이모드를 안 썼다. 다른 감사 4개가 동시에 돌아 **아무것도 안 고쳤다**.
> 「의심」은 코드상 그렇게 보이지만 **실행으로 확인 못 했다**는 뜻이다.

---

## 0. 심각도 순 요약

> **ID 두 종류를 구분한다.** `F-n` = **이번에 찾은 결함**(이 보고서 안에서만 쓴다) · `E-nn`/`M-nn` = **앞으로 계속 쓸 테스트 케이스**(§6, **번호 영구**).

| # | 심각도 | 무엇이 | 어디서 |
|---|:--:|---|---|
| **F-1** | **치명** | 인자 이름을 틀리면 **에러 없이 `default(T)`**(int 0 = 호스트 clientId) · 이름이 안 맞아도 **개수가 같으면 순서로** 꽂힌다 | `WesQA/Runtime/InvokeBridge.cs:88-106` |
| **F-2** | **치명** | QA 스위트 7개가 **실패해도 exit 0** — CI에 못 물린다 | `qa_v37/40/41/42/43/44/45` 최하단 |
| **F-3** | **치명** | 「첫 발견」은 **가장 이른 시각이 아니다** — 살아 있는 고양이 목록의 **첫 번째** 값이다. 죽은 고양이의 더 이른 발견은 사라진다 | `Bots/TestBotManager.cs:292-309` |
| **F-4** | **높음** | `or True` — **절대 실패할 수 없는 단언**이 스위트에 박혀 있다 | `qa_v41.py:77` |
| **F-5** | **높음** | 미니맵 「적 마커 0」 검사가 **미니맵이 통째로 없어도 통과**한다(-1 → +∞) | `qa_v49.py:97, 245` ← `TestManager.Minimap.cs:75` |
| **F-6** | **높음** | `NavStats`의 `detour`·`short`는 **마지막 경로 한 건**의 값이고, 길을 **포기하면 1.00(만점)** 이 된다 | `Ai/NavRoute.cs:373-378` · `Bots/TestBotManager.cs:513-539` |
| **F-7** | **높음** | 치즈 총량 훅이 **로비 사람 수**로 계산하는데 게임은 **실제로 선 몸(봇 포함)** 으로 깐다 | `Testing/TestManager.Balance.cs:55-104` vs `Economy/CheeseSpotDirector.cs:153-160` |
| **F-8** | **높음** | `TestGetSetting`·`TestSetSetting`의 `default:`가 **전부 sfx**다. 오타 키가 **읽기·쓰기 양쪽에서 일관되게** sfx로 가서 왕복 단언이 통과한다 | `Testing/TestManager.View.cs:217-226, 250-258` |
| **F-9** | 높음 | `MouseCheese`·`MouseTechStats`는 **산 쥐만** 센다(봇 감사 A4와 같은 부류). 같은 질문에 답하는 `MouseReport`와 값이 다르다 | `Bots/TestBotManager.cs:312-347` |
| **F-10** | 높음 | 「없음」과 「정상값」이 같은 값으로 나오는 훅 다수 — `TestGetPhase`는 **PhaseDirector가 없으면 "Hunt"** | §3 |
| **F-11** | 높음 | **조용한 조건부 건너뛰기** — SKIP도 NOTE도 안 남아 분모가 실행마다 달라진다. "16/16 통과"가 24항목 중 8개를 안 돈 것일 수 있다 | `qa_v36.py:162,247` 외 |
| F-12 | 보통 | 고양이 봇의 **광분·예민한 귀를 「영원히 안 산다」** 로 못박은 EditMode 테스트가 남아 있다(쥐 쪽은 고쳤다) | `Tests/EditMode/CatBotLogicTests.cs:72-79` |
| F-13 | 보통 | 어서트가 **0회 실행**되는 테스트 · `Assert.Ignore`로 영구 초록이 되는 테스트 | `AlcoveSpotLogicTests.cs:113-136` · `AnimationStateLogicTests.cs:75-79` |
| F-14 | 보통 | `invoke()`에 **타임아웃이 없다** — 게임이 멈추면 QA가 영원히 대기 | `wesqa/__init__.py:46` |
| F-15 | 보통 | `WesQA.dll`이 **배포 빌드 바이너리에 그대로 실린다**(동작은 안 함) | `Assets/WesQA/WesQA.asmdef` |
| F-16 | 보통 | 도구 자신을 검증하는 장치가 **0개** — `Testing/`(15) · `Editor/`(54) · `TestBotManager` 전부 무테스트 | §4·§6 |
| F-17 | 낮음 | 조건당 10판으로 승률을 말할 수 없다(진단서 자신은 이미 그렇게 적고 있으나 **러너는 그 경고를 안 찍는다**) | `run_matches.py:31, 184` |

### 좋은 소식 넷 — 확인했다

| | 근거 |
|---|---|
| 봇 감사 **A4·A5·A6은 이미 고쳐졌다** | `Retire`가 길찾기 통계를 이월(`TestBotManager.cs:373-383`) · 시야는 **고양이만**(:363-368) · 사인은 `죽을때>직전` 두 칸(:402) |
| 계측 오염 가드가 **실제로 들어갔다** | `run_matches.py:109-111`이 판마다 `TestGetMouseCount`를 읽어 CSV `warning` 칸에 「사람쥐N」을 남기고, 요약이 `!! 이 표는 쓰지 마라`를 찍는다(:244-247) |
| **배포 빌드는 QA 서버를 안 열고 치트 훅도 없다** | §5 — `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`가 15파일 전부에 · 배포 메뉴가 `BuildOptions.None`을 명시 전달 |
| EditMode 628개 중 **assert 없는 테스트 0 · try/catch 삼킴 0** | 품질 문제가 「동어반복」에 집중돼 있고 구조적 부실은 아니다 |

---

## 0-B. 1단계 조치 기록 — C# 훅의 정직성 (2026-09-09 실행)

> 범위는 **계측 코드만**이다. 게임 로직(P0-1~9)과 `tools/wesqa/`의 파이썬은 손대지 않았다.
> EditMode **650/650 통과**(기준선 629 + 신규 21). 컴파일 에러 0.

### 고친 것

| # | 무엇이 거짓이었나 | 어떻게 고쳤나 | 어디 |
|---|---|---|---|
| **T4 / F-3** | 「첫 발견」이 최솟값이 아니라 **살아 있는 고양이 목록의 첫 값** — 고양이가 죽을수록 늦어졌다 | `EarliestSight()`가 **퇴장분까지 접어 최솟값**을 낸다. 시각·거리를 **한 사건으로 묶어** 접는다(따로 접으면 없던 사건이 나온다) | `Bots/TestBotManager.cs` · `Bots/BotStatsLogic.cs`(신규 순수) |
| **T6 / F-6** | `detour`가 **마지막 경로 한 건**이고 **포기하면 1.00(만점)** — 포기가 지표를 좋게 만들었다 | 포기·제자리 경로는 **표본이 아니다**. `detour`는 **판 전체 누적 평균**, 표본 0이면 `-1.00`. `short`는 포기 시 **목표까지 통째로** 적는다(포기가 어딘가에서는 반드시 나빠진다). 출력에 `paths=N`(표본 수) 추가 | `Ai/NavRoute.cs` · `Ai/NavStatsLogic.cs`(신규 순수) · `Bots/BotNavigator.cs` |
| **T7 / F-7** | 총량 훅이 **로비 사람 수**, 게임은 **실제로 선 몸(봇 포함)** — 봇 판에서 최대 8배 | 분모를 하나로. `CheeseSpotDirector.CountSpawnedMice()`를 공개하고, 게임이 매장량을 깔 때 쓴 수를 `DesignedMiceCount`로 남겨 훅이 **그 수를 그대로 읽는다** | `Economy/CheeseSpotDirector.cs` · `Testing/TestManager.Balance.cs` |
| **T8 / F-8** | `TestGetSetting`/`TestSetSetting`의 `default:`가 **양쪽 다 sfx** — 오타 키가 자기 일관되게 왕복 통과 | `case "sfx"`를 명시하고 **모르는 키는 읽기 NaN · 쓰기 무동작 + 경고** | `Testing/TestManager.View.cs` |
| **T9 / F-10** | 「없음」을 정상값으로 — `TestGetPhase` → `"Hunt"` 등 | 문자열 센티널 **`TestManager.MISSING = "Missing"`** 도입. `TestGetPhase`·`TestGetMouseJob(Of)`·`TestGetCatClass(Of)`·`TestGetMatchResult`가 미배선을 `"Missing"`으로 말한다 | `Testing/TestManager.cs` 외 4파일 |

### 같이 찾아 고친 같은 부류

| 부류 | 무엇 | 조치 |
|---|---|---|
| ⑴ 죽은 몸을 빠뜨리는 집계 (**F-9**) | `MouseCheese`·`MouseTechStats`가 **산 쥐만** — 전멸 판이 `mice=0 techs=0`, `MouseReport`와 값이 갈렸다 | `BotAgent.LastCheese` 추가 + `Retire`가 지갑·테크·마릿수를 이월. 두 훅이 `MouseReport`와 같은 분모를 본다 |
| ⑵ 「일어난 일」을 안 보는 훅 | `TestGetCheerMarkerCount`가 **`gameObject.name == "CheerMarker"`** — 개명하면 영원히 0이라 「고양이 화면엔 0」이 항상 통과 | 표식 컴포넌트 `Match/CheerMarker`(신규)를 세고, `CheerDirector`가 마커를 띄울 때 붙인다 |
| ⑵ | `TestGetWarehouseTicks` 주석이 *"소리·이펙트가 발화한 횟수와 같다"* 는 **확인하지 않는 등식**을 단언 | 주석을 사실로 고쳤다(생산 횟수일 뿐). 실제 재생 횟수는 `TestGetSfxPlayCount` 부채로 남는다 |
| ⑶ 없음 = 정상값 | `TestGetBotCount`·`TestGetBotMouseCount`·`TestGetBotMouseCheese`가 매니저가 없어도 **0** | **-1 + 경고**. 「봇이 0마리」와 「계측기가 씬에 없다」를 가른다 |
| ⑶ | `TestGetCatFrenzyActive`·`TestGetCatSkillActive`는 **bool이라 「없음」을 못 말한다** | 파이썬 계약을 안 깨려고 반환형은 두고 **컴포넌트가 없으면 경고**. 세 값이 필요한 곳을 위해 `TestGetCatFrenzyState`·`TestGetCatSkillState`("On"\|"Off"\|"Missing") 신설 |
| 조용한 무동작 | `TestGetJobTicketPositions`가 「서버 전용」 주석뿐 — 클라에서 늘 `Gang:\|Hero:` → **「자리가 안 바뀐다」로 읽혔다** | `RequireServer()` 실가드 + 못 찾은 칸은 `Missing`. 같은 가드를 `TestGrantAdvanceMischief`·`TestMaxOneUpgrade`·`TestMaxAllUpgrades`에도 걸었다 |
| 조용한 무동작 | `TestBurnPhaseClock`이 `m_Remaining` **필드명 문자열**에 기대 — 이름이 바뀌면 로그 한 줄 없이 무동작 | 필드·타입을 못 찾으면 **`LogError`** |
| 조용한 무동작 | `TestSelectRoleCat/Mouse`·`TestStartGame`이 로비를 못 찾으면 **말없이 return** | 경고를 남긴다 |
| 이름이 거짓 | `TestKillCat`이 **첫 마리에서 `return`** — 2고양이 판에서 「쥐 승」이 안 났다(E-19) | 고양이 **전원**을 죽이고 **죽인 마릿수를 반환**(서버 아님 = -1) |

### 계측기 자신에 처음 붙은 테스트 (E §6-2의 선행 작업)

집계를 **순수 static으로 뽑아** EditMode가 손댈 수 있게 만들었다 — 이번 사태의 뿌리가 「계측기가 무테스트」였다.

| 파일 | 무엇을 잰다 | 케이스 |
|---|---|:--:|
| `Tests/EditMode/BotStatsLogicTests.cs` (12) | 첫 목격이 **최솟값**인가 · 시각·거리가 **같은 사건**인가 · 죽은 쪽 값이 살아남는가 · 표본 0이 만점이 아닌가 · 누적 평균인가 | E-13 · E-14 · E-11 |
| `Tests/EditMode/NavStatsLogicTests.cs` (9) | 포기가 우회율 표본이 아닌가 · 포기의 `short`가 **목표까지 통째**인가 · 「이미 그 자리」가 표본이 아닌가 · 포기를 섞어도 평균이 안 좋아지는가 | E-14 |

### 이제 **빨개질 수 있는** 것 — 예상, 미실행

> 파이썬 스위트를 **돌리지 않았다**(다른 에이전트가 동시에 고치는 중). 아래는 코드상 예측이다.
> 빨개진다면 그건 **도구가 드디어 진실을 말하는 것**이고, 그 항목 자체를 고치는 건 2단계 일이다.

| 스위트·항목 | 왜 이제 빨개질 수 있나 |
|---|---|
| `qa_v43` 「단계 — 처음에는 1단계다」(`TestGetPhase == "Hunt"`) | **`PhaseDirector`가 씬에 없으면 이제 `"Missing"`이 나온다.** 예전에는 없어도 `"Hunt"`라 통과했다 — 이 항목이 지금까지 무엇도 검증하지 않았을 가능성이 여기서 드러난다 |
| `qa_v42:37` `TestGetCatClass == "None"` · `qa_v43:37` `TestGetMouseJob == "None"` · `qa_v43:81` `TestGetMatchResult == "None"` | 컴포넌트·매니저가 없으면 `"Missing"`. 배선이 멀쩡하면 그대로 통과한다 |
| `qa_v50` 「품절 조합이 안 바뀜」·직업권 자리 비교 | `TestGetJobTicketPositions`가 클라에서는 `"Missing"`을 준다. 호스트에서 부르는 지금 경로는 그대로지만, **못 찾은 칸이 `Missing`으로 보인다** |
| `run_matches.py`가 읽는 `TestGetNavStats` | `detour` 값이 **바뀐다**(누적 평균 · 표본 0이면 `-1.00`). 진단서의 「detour 1.00 = 곧장 간다」는 더 이상 나오지 않는다. `short`도 커진다(포기가 이제 계산에 들어간다) |
| `run_matches.py`의 「첫 발견」 칸 | 값이 **빨라진다**(더 이른 목격이 살아난다). 진단서의 옛 표와 **직접 비교하면 안 된다** |
| `health_check.py`의 `TestGetBotMouseCheese` | 매니저가 없을 때 `0`이 아니라 `-1`. `max()`로 감싸고 있어 통과는 하지만 **값이 음수면 계측기가 안 선 것**이다 |
| `qa_v38`·`v41`·`v44`의 총량·매장량 | 사람만 있는 판에서는 값이 같아야 한다. **다르면 그게 T7이 잠복해 있던 자리**다 |

### 남긴 것 (이번 범위 밖)

| # | 왜 안 했나 |
|---|---|
| F-1·F-2·F-4·F-5·F-11·F-14 | `InvokeBridge`·`qa_v*.py`·러너 — **파이썬 쪽 에이전트 담당** |
| E-06 전수 계약 테스트 | `bool` 반환 훅을 3값으로 바꾸면 파이썬 계약이 깨진다. 우선 `~State` 훅을 짝으로 세워 두고, 이관은 파이썬 수정과 함께 해야 한다 |
| E-21 `TestGetSfxPlayCount` | 새 런타임 계측(재생 훅)이 필요하다 — 훅의 정직성이 아니라 **새 기능**이다 |
| F-12·F-13 EditMode 정비 | 계측기 정직성과 별개 축(E-28~E-31) |

---

## 1. 훅 대장 — `TestManager` 전 15파일 (216개)

**표기.** 종류 = **실물**(씬 오브젝트·렌더된 글자·`AudioSource.volume`·Transform — 배선 누락이 잡힌다) ·
**Model**(상태 변수·`NetworkVariable`·로직 클래스 — **배선 누락을 못 잡는다**) · **변경**(상태를 바꾸는 훅, 계측 아님) · **혼합**.

### 1-1. `TestManager.cs` (핵심 21)

| 훅 | 무엇을 읽나/하나 | 종류 | 신뢰도 | 함정 |
|---|---|:--:|:--:|---|
| `SimulateKeyPress` | Input System StateEvent 주입 | 변경 | 높음 | 게임 뷰 포커스 없으면 죽는다(메모리 기록) |
| `TestLoadScene` | `SceneManager.LoadScene` | 변경 | 보통 | in-scene NGO 스폰 흐름엔 못 쓴다(주석 명시) |
| `TestIntroStart` · `TestCreateRoom` · `TestRoomForceStart` · `TestSetRoomBots` · `TestGetRoomBots` | 로비·방 진행 | 변경/Model | 보통 | — |
| `TestGetFrameStats` | `Time.smoothDeltaTime` | 실물 | 높음 | 에디터 3인 환경은 EditorLoop가 지배 |
| `TestGetNetDiag` | 씬·NM 싱글턴·`IsListening` | 실물 | 높음 | **접속 클라 수를 안 준다** → 클론 오염을 직접 못 잰다(§3) |
| `TestStartClient` · `TestStartHost` | `HostPortFallback` 경유 | 변경 | 높음 | 직접 `StartHost` 금지 규칙 준수 ✔ |
| `TestSelectRoleCat/Mouse` · `TestStartGame` | `LobbyController.Dev*` | 변경 | 보통 | 로비 못 찾으면 **조용히 아무 일도 안 한다**(로그도 없다) |
| `TestPounce` · `TestShootAtCat` | 로컬 몸의 Dev 래퍼 | 변경 | 높음 | — |
| **`TestKillCat`** | 고양이 **하나**를 즉사 | 변경 | **낮음** | 이름은 복수로 읽히는데 **첫 마리에서 `return`**(`:264`). 2고양이 판에서 「쥐 승」이 안 난다 |
| `TestRestartMatch` · `TestExpireTimer` | `MatchManager` | 변경 | 보통 | 주석은 「서버 전용」인데 **`IsServer` 검사 없음**(피호출부가 방어) |
| `TestGetLocalClientId` | `nm.LocalClientId` | 실물 | 높음 | MPPM 번호와 다름을 주석이 경고 ✔ |

### 1-2. `TestManager.Bots.cs` + `NavGrid` 통계 (16) — **계측의 심장**

| 훅 | 무엇을 읽나 | 종류 | 신뢰도 | 함정 |
|---|---|:--:|:--:|---|
| `TestSpawnBots` · `TestClearBots` | 봇 생성/정리 | 변경 | 높음 | `IsServer` ✔ |
| `TestGetBotCount` · `TestGetBotMouseCount` | 살아 있는 봇 수 | 실물 | 보통 | 매니저 없으면 **0** — 「봇이 없다」와 구분 불가 |
| **`TestGetBotFirstSight`** | 고양이 봇 첫 목격 시각 | Model | **낮음** | **F-3 — 최솟값이 아니라 목록 첫 값. 죽은 고양이 값 소실** |
| **`TestGetBotFirstSightDistance`** | 같음(거리) | Model | **낮음** | 같은 이유 |
| **`TestGetBotMouseCheese`** | 쥐 봇 지갑 합 | 실물 | **낮음** | **F-9 — 산 쥐만.** 러너가 `max()`로 완화(`health_check.py:97`) |
| **`TestGetBotTechStats`** | `mice/techs/buyers` | 실물 | **낮음** | **F-9 — 산 쥐만.** `TestGetBotMouseReport`의 `techs=`와 **값이 다르다** |
| `TestGetBotShopStats` | `seekers/reached/unreached/seek/inside` | Model | 높음 | 퇴장분 이월 ✔ |
| `TestGetBotMouseReport` | `elapsed/mice/alive/dead/earned/techs/deaths` | Model | 높음 | 퇴장분 이월 ✔ · 사인 두 칸(A6 수정) ✔ |
| `TestGetBotSightStats` | `sights/avg/max/far` | Model | 높음 | **고양이만**(A5 수정) ✔ |
| **`TestGetNavStats`** | `plans/giveups/ms/detour/short/stuck/moved` | Model | **낮음** | **F-6** — `plans`·`giveups`·`stuck`·`moved`는 이월돼 정직하나, **`detour`·`short`가 마지막 한 건 · 포기 시 만점** |
| `TestGetBotCombatStats` | `shots/catdeaths/catdmg/cats` | Model | 높음 | 퇴장분 이월 ✔ |
| `TestSetBotCounterAttack` | `BotAgent.DevCounterAttack` | 변경 | 높음 | 손잡이 하나씩 돌리기용 ✔ |
| `TestGetBotOrderCount` | 지휘관 명령 수 | Model | 높음 | — |
| `TestGetBotDiagnostics` | 봇별 `역할/활동/이동거리/길` | 실물 | 보통 | **산 봇만** — 끼어서 죽은 봇은 러너의 「제자리봇」 검사에 안 잡힌다 |

### 1-3. `TestManager.Balance.cs` (9)

| 훅 | 무엇을 읽나 | 종류 | 신뢰도 | 함정 |
|---|---|:--:|:--:|---|
| `TestGetBalanceValue` | `BalanceInfo` 필드를 이름으로 | Model | 높음 | 없는 이름 → NaN ✔ (문자열·enum 필드도 NaN — 구분 불가) |
| `TestGetFootstepRange` | **`AudioSource.maxDistance` 실측** | 실물 | 높음 | 모범 — 「재생 중인가」가 아니라 실제 반경 ✔ |
| `TestGetMouseCount` | `LobbyController.MouseCount`(**사람만**) | Model | 보통 | 로비 없으면 0. **봇을 안 센다** |
| **`TestGetMapTotalCheese`** | 위 수로 총량 재계산 | Model | **낮음** | **F-7 — 게임은 `CountSpawnedMice()`(봇 포함)로 깐다** |
| **`TestGetDesignedSpotReserve`** | 위 수 + `ReservePerSpot` 재계산 | Model | **낮음** | **F-7 + 구현 미러**(같은 식을 다시 부른다 → 식이 틀려도 양변이 같이 틀린다) |
| `TestGetCatCapacity` | 상수 | Model | 높음 | — |
| `TestGetMapShape` | `MapLayout` 필드 | Model | 높음 | 못 찾으면 빈 문자열 ✔ |
| `TestGetCheeseSpotObjectCount` | **씬의 `CheeseSpot` 수** | 실물 | 높음 | 레이아웃 수와 대조하라는 설계 ✔ |
| `TestGetMoveSpeed` | 프리팹에 구운 `BaseSpeed` | 실물 | 높음 | — |

### 1-4. `TestManager.BotMatch.cs` (5)

| 훅 | 무엇을 | 종류 | 신뢰도 | 함정 |
|---|---|:--:|:--:|---|
| `TestSetTimeScale` · `TestResetTimeScale` | `Time.timeScale` + `fixedDeltaTime` | 변경 | 보통 | 「사람 낀 판 금지」가 **주석뿐** — 코드 가드 없음. Reset이 `0.02f` 하드코딩 |
| `TestForceStartGame` | `DevForceStartGame` | 변경 | 높음 | — |
| `TestGetMatchRemaining` | `RemainingSeconds` | Model | 높음 | 없으면 -1 ✔ |
| **`TestGetMatchSummary`** | `result,remaining,mice,cats,firstSight,phase` | Model | **보통** | `mice`·`cats`는 **살아 있는 봇 수**(러너 칸 이름 `mice_left`는 정직 ✔) · `firstSight`가 **F-3** |

### 1-5. `TestManager.Minimap.cs` (5) — **이 파일이 모범이다**

| 훅 | 무엇을 읽나 | 종류 | 신뢰도 | 함정 |
|---|---|:--:|:--:|---|
| `TestIsMinimapVisible` | 루트 `activeInHierarchy` | **실물** | 높음 | Model의 `Active`가 아니다 ✔ |
| `TestGetMinimapMarkerCount` | **켜진 `Image` 아이콘 수**(`MinimapHud.cs:57-67`) | **실물** | 높음 | 모르는 종류 → **-1 + 경고** ✔ ← 다른 훅들이 따라야 할 형태 |
| `TestGetMinimapDiscoveredCheese` | 클라 전용 기억 | Model | 높음 | Model인 이유를 주석이 해명 ✔ |
| `TestGetMinimapNearestMarkerMeters` | **아이콘 픽셀을 월드로 역산** | **실물** | 높음 | 종류만 세면 못 잡는 「엉뚱한 자리」를 잡는다 ✔ · **단 -1을 호출부가 오해한다 → F-5** |
| `TestGetPlayerPos` | `NetworkTransform` 좌표 | 실물 | 높음 | — |

### 1-6. `TestManager.Tech.cs` (25)

| 훅 | 무엇을 | 종류 | 신뢰도 | 함정 |
|---|---|:--:|:--:|---|
| `TestGetTechCost` · `TestGetPlaceCost` | CSV 행 | Model | 높음 | 모르는 이름 → -1 ✔ |
| `TestGetCheeseOf` · `TestGetEarnedOf` · `TestGetTechLevel` · `TestGetTechSlotCount` · `TestGetHpOf` · `TestGetMaxHpOf` | 지목 클라의 컴포넌트 | 실물 | 높음 | 전부 **-1 센티널** ✔ · 복제 의미를 주석이 명시 ✔ |
| `TestIsInShop` · `TestGetShopSpotCount` | `ShopSpot` 실물 | 실물 | 높음 | — |
| `TestGetShopSpotPositions` | 명부(`ShopSpot.All`) | 혼합 | 높음 | 개수 훅과 출처가 달라 **드리프트가 잡힌다** ✔ |
| `TestBuyTech` · `TestUseTech` | 소유자 RPC | 변경 | **보통** | 남의 몸이면 **경고만 찍고 무시**. 반환값 없음 → 거절을 호출자가 모른다 |
| `TestBuyTechLocal` · `TestUseTechLocal` | 로컬 | 변경 | 높음 | — |
| `TestWarpToNearestShop` | `BodyPush.ServerPush` | 변경 | 높음 | `true`가 「도착」이 아님을 주석이 명시 ✔ |
| `TestGetWarehouseCountOf` · `TestGetStickyPatchCount` · `TestGetNearestFortressMaxHp` | 씬 오브젝트 수/HP | 실물 | 높음 | `NearestFortress`는 **주인을 안 가린다** — 「내 요새」를 묻는 QA엔 부적합 |
| `TestGetMoveSpeedOf` | `PlayerMovement.CurrentSpeed` | 실물 | 높음 | 「식을 안 베낀다」를 주석이 못박음 ✔ |
| `TestGetCatFrenzyActive` · `TestGetCatFrenzyCooldown` | `CatFrenzy` | Model | 보통 | Active는 **컴포넌트가 없어도 false** → 「꺼져 있다」 단언이 헛통과 |
| `TestGetListenerMultiplier` | 이 프로세스 배율 | 실물 | 높음 | 물어볼 인스턴스를 주석이 지정 ✔ |
| `TestGetMatchSeed` · `TestGetMatchSubSeed` | `MatchRandom` | Model | 높음 | 없으면 0 · 모르는 용도 → 0 + 경고 |
| **`TestClearSoldOut`** | 시드를 0으로 | 변경 | 보통 | **무작위를 지우는 훅.** 안 부르면 v46·v47이 그날 품절에 깨지고, 부르면 품절 자체는 안 재진다(v50이 분담) |
| `TestGetSoldOutTechs` | 로컬이 **다시 계산한** 목록 | 실물 | 높음 | 호스트·클론 불일치가 곧 버그라는 설계 ✔ |

### 1-7. `TestManager.Economy.cs` (30)

| 훅 | 무엇을 | 종류 | 신뢰도 | 함정 |
|---|---|:--:|:--:|---|
| `TestGrantCheeseAll` | 전 지갑 +1000 | 변경 | 높음 | `IsServer` ✔ |
| `TestBuySlingshot/MachineGun/Dash/PepperShot/CatAttack/CatSpeed` | 로컬 구매 | 변경 | 보통 | 옛 이름 유지(파이썬이 문자열로 부름). `MachineGun`은 Lv1 없으면 **경고 후 Lv1을 산다** |
| `TestBuyFortress` · `TestPlaceWarehouseAt` | 사고 → **복제를 기다렸다** 설치 | 변경 | 높음 | `WaitForTechLevel`(`Tech.cs:28-45`) — 이 하네스의 **유일한 비동기 안전장치** ✔ |
| `TestGetCheeseEarnerNear` · `TestGetCheeseLabelNear` | 최근접 지점 | 실물 | 높음 | 라벨은 **렌더 문자열**이라 「호스트에서 표시가 얼어붙던」 회귀를 잡는다 ✔ |
| 치즈·벽감·상자·드롭 계수 9종 | **씬 오브젝트 전수** | 실물 | 높음 | — |
| `TestGetLowestStructureHp` · `TestGetFortressPos` · `TestGetFortressDistance` | 씬 구조물 | 실물 | 높음 | — |
| `TestBreakFirstBox` · `TestBreakFirstAlcove` · `TestHitFirstAlcove` · `TestDrainAllCheeseSpots` | 파괴/소진 | 변경 | 높음 | — |
| `TestDebugPounceTargets` | 덮치기 후보 진단 | 실물 | 높음 | 「왜 안 맞나」 전용 ✔ |
| `TestGetUnwiredSfxIds` | **전 `SfxId`의 클립 배선 전수** | 실물 | 높음 | 「조용히 null이 들어가는」 사고를 전수로 잡는 모범 ✔ |
| `TestGetWarehouseSfxClip` | 클립 **이름** | 실물 | 보통 | 「붙어 있다」지 「울렸다」가 아니다 |
| **`TestGetWarehouseTicks`** | 생산 횟수 | Model | **낮음** | 주석이 *"소리·이펙트가 발화한 횟수와 같다"* 고 단언하는데 **훅은 그걸 확인하지 않는다.** 소리 호출을 떼도 이 수는 그대로다 (핸드오프의 `TestGetSfxPlayCount` 부채와 같은 자리) |
| `TestGetCheese` · `TestGetCheeseRevived` · `TestGetBestCheeseSpotPos` | 지갑·디렉터 | 혼합 | 높음/보통 | — |

### 1-8. `TestManager.View.cs` (35)

| 훅 | 무엇을 | 종류 | 신뢰도 | 함정 |
|---|---|:--:|:--:|---|
| `TestSetCameraAngles` · `TestGetFadedOccluderCount` | 카메라·페이드 수 | 실물 | 높음 | — |
| `TestSetSneak` · `TestGetSneaking` · `TestGetFootstepAudible` | 잠행·발소리 | 혼합 | 높음 | 못 찾으면 false(경미) |
| `TestCheer` | 따봉 | 변경 | 높음 | — |
| **`TestGetCheerMarkerCount`** | `gameObject.name == "CheerMarker"` | 실물 | **낮음** | **이름 문자열 매칭.** 마커 이름을 바꾸면 영원히 0 → 「고양이 화면에서는 0이어야 한다」가 **항상 통과** |
| `TestSendChat` · `TestTypeChatAndEnter` · `TestTypeChat` · `TestCloseChat` | 채팅 | 변경 | 높음 | `TypeChatAndEnter`가 **입력창을 실제로 태운다** — 한글 절삭 버그를 잡은 이유 ✔ |
| `TestGetChatLog` · `TestGetChatHint` · `TestGetChatOpen` | HUD 상태 | 실물 | 높음 | — |
| `TestGetConsoleState` · `TestShowDebugConsole` | 서드파티 콘솔 리플렉션 | 실물 | 보통 | 필드명 문자열 의존(외부 패키지라 불가피) |
| `TestOpenOptions` · `TestCloseOptions` · `TestGetOptionsOpen` | 팝업 | 실물 | 높음 | — |
| **`TestGetSetting`** | `sensitivity`/`bgm`/**나머지 전부 sfx** | Model | **낮음** | **F-8** |
| **`TestSetSetting`** | 같은 `default:` | 변경 | **낮음** | **F-8 — 읽기·쓰기가 같은 곳으로 새어 왕복 단언이 통과한다** |
| `TestResetSettings` · `TestSaveSettings` · `TestRevertSettings` | 2층(스트림/디스크) 경로 | 변경 | 높음 | — |
| `TestGetAudioVolume` | **`AudioSource.volume` 실측** | **실물** | 높음 | 2026-08-09 사고를 잡으려 만든 훅 ✔ · `GameObject.Find` 이름 의존(없으면 -1이라 크게 울린다) |
| `TestGetLocalHp` · `TestGetRole` | 로컬 몸 | 실물 | 높음 | 없으면 -1/"None" |
| `TestGetEyeGaze` · `TestHideEyes` · `TestShowEyes` · `TestBlinkEyes` | **눈동자 Transform 실측** | **실물** | 높음 | 연출을 Model로 재지 않는 모범 ✔ |
| `TestGetInputBlocked` · `TestGetCursorLocked` · `TestGetControlDiag` | 입력 게이트 | 실물 | 높음 | 커서는 **게임 뷰 포커스가 없으면 무의미**(메모리 기록) |
| `TestGetEarState` · `TestGetEarStateOfRole` | **적용 중인 귀 각도 + 판단 재료** | **실물** | 높음 | 모범 — 값과 근거를 같이 준다 ✔ |
| `TestSetBotSneak` | 봇 잠행 잠금 | 변경 | 높음 | 「두뇌가 매 틱 덮어쓴다」를 알고 판단 자체를 잠근다 ✔ · `IsServer` ✔ |
| `TestTeleportAtRangeFrom` · `TestAimCameraAtRole` | 거리·시선 고정 | 변경 | 높음 | 실제 거리 반환 ✔ |

### 1-9. `TestManager.Map.cs` (23)

| 훅 | 무엇을 | 종류 | 신뢰도 | 함정 |
|---|---|:--:|:--:|---|
| `TestTeleportToOtherPlayer` | 지정/최초 타인 앞으로 | 변경 | 보통 | **인자 누락 시 0(=호스트)** 으로 물린 전례가 주석(`:26-29`)에 남아 있다 → **F-1** |
| `TestTeleportLocalPlayer` · `TestTeleportToDog` | 좌표 이동 | 변경 | 높음 | CC 토글 규칙 준수 ✔ |
| `TestWalkTowards` · `TestWalkDirection` | **실제로 걷는다** | 변경 | 높음 | 텔레포트로는 못 재는 「길이 뚫렸나」 ✔ · 완료 통보 없음(fire-and-forget) |
| 강아지 8종 | 벽감 HP·마릿수·HP·좌표·거리·진단·파괴·피해 | 실물 | 높음 | — |
| `TestGetNavMeshPolygons` · `TestNavMeshIslands` · `TestNavSample` · `TestNavPath` | **NavMesh** | 실물 | 보통 | **길 계산의 주인은 격자다**(CLAUDE.md) — 이 넷은 참고용. 격자와 답이 갈릴 수 있다 |
| 거리·높이 5종 | 로컬 몸 좌표 | 실물 | 높음 | — |
| `TestGetSelectedMap` · `TestSelectMap` | 맵 선택 | 혼합 | 높음 | — |
| **`TestGetJobTicketPositions`** | 벽 뒤 내용물 좌표 | 실물 | **보통** | 주석은 「서버 전용」인데 **`IsServer` 검사가 없다.** 클라에서 부르면 `Gang:\|Hero:` — **두 판을 비교하면 「자리가 안 바뀐다」로 읽힌다** |
| `TestGetMapLootCheese` | 상자·벽감 치즈 총합 | 실물 | 높음 | 셔플 불변식 검사용 ✔ |

### 1-10. 나머지 5파일 (53)

| 파일 | 요점 |
|---|---|
| `Hud.cs` (6) | **전부 실물** — 컴포넌트의 TMP 참조를 리플렉션으로 타고 들어가 **화면에 올라간 글자**를 읽는다. 참조가 빠지면 빈 문자열로 실패한다 ✔ 신뢰도 높음 |
| `CatClass.cs` (13) | 조회 7은 복제 의미를 주석이 명시 ✔. **`TestGrantAdvanceMischief`·`TestMaxOneUpgrade`·`TestMaxAllUpgrades`는 주석이 「서버 전용」인데 `IsServer` 검사가 없다** — 피호출부(`PlayerMovement.cs:90`)가 막으므로 클라에서 부르면 **조용히 아무 일도 안 일어난다** |
| `MouseJob.cs` (8) | **`TestGetPhase`가 `PhaseDirector`를 못 찾으면 `"Hunt"`** → `qa_v43.py:38`이 미배선 씬에서도 통과(**F-10**). `TestBurnPhaseClock`은 `m_Remaining` **필드명 문자열** 의존 — 이름이 바뀌면 **경고조차 없이 무동작** |
| `NavGrid.cs` (5) | `TestGetNavStats`(F-6) 외 **`TestGetNavGridInfo`·`TestTracePath`·`TestDumpGrid`는 이 하네스 최고 수준** — 격자 상태·두 점 실제 길찾기·글자 그림. 「추측 말고 캔다」가 실제로 가능하다 ✔ |
| `Placement.cs` (3) | `TestDebugPlacementBlockers`가 **서버와 같은 `PlacementOverlap`을 공유**한다 — "훅은 비었다는데 서버는 거절하는" 최악을 구조적으로 막았다 ✔ |
| `Capture.cs` (4) | **임시 카메라로 직접 렌더** — 씬 뷰 스크린샷이 갱신 안 되는 함정(CLAUDE.md)을 정면으로 피했다 ✔ |

---

## 2. wesqa 스위트 대장

### 2-0. 실행·채점 구조 (확인)

| 사실 | 근거 |
|---|---|
| **`qa_v*.py`는 pytest가 수집하지 않는다** — 전부 손으로 `python qa_vNN.py` | `pytest.ini:1-3` (`python_files = test_*.py`) |
| pytest가 수집하는 6개(`test_connect` 등)는 **게임을 한 줄도 안 건드린다**(모의 서버·이미지 비교) | `tests/fake_server.py` |
| `conftest.py`는 4줄 — `sys.path` 삽입뿐. **픽스처·가드 0** | `conftest.py:4` |
| **7개 스위트가 실패해도 exit 0** (`main()`을 부르고 반환값을 버린다) | `qa_v37:230` `v40:150` `v41:155` `v42:111` `v43:109` `v44:171` `v45:117` |
| v44·v45는 `summary()`에 **`return`문 자체가 없다** — `sys.exit()`로 감싸도 0 | `qa_v44.py:163-168` · `qa_v45.py:109-114` |
| `CLAUDE.md:183`의 「qa_v36 14항목」은 **낡았다** — 실측 24 | — |

### 2-1. 스위트 표

항목 수 = `check(`/`skip(`/`note(` 호출 수(정의 줄 제외).

| 스위트 | 목적 | 호스트 | 항목 | 하드 단언 | soft만 | SKIP/NOTE | 무작위 위험 | 종료코드 |
|---|---|:--:|:--:|:--:|:--:|:--:|---|:--:|
| v36 | v3.6 종합(라벨·잠행·요새·창고·따봉·카메라) | 고양이 | 24 | 24 | 0 | 0 · **조건부 8** | **높음** — `sleep` 31회 · 21초 생산 대기 · 좌표 `sx±2/±6/±8/−12` 하드코딩 | ✔ |
| v37 | 카메라 프레이밍·옵션창·채팅 | **쥐** | 24 | 24 | 0 | 0 · 조건부 2 | **높음** — 외부 PowerShell 포커스(`:23-26`) · sleep 20회 · 덮치기 14회 반복 | ✘ |
| v38 | 벽감 + 즉시획득 치즈 | 고양이 | 15 | 15 | 0 | 0 | 중 — 벽감 수 **4~8 허용**(폭 5배) · 좌표 `dx+1.5` | ✔ |
| v40 | 제3세력 강아지 | 고양이 | 16 | 16 | 0 | 0 | **높음** — 절대좌표 `(−13,−13)` 등 **30×30 맵 전제** · 12초 이동거리 · 물기 10회 재시도 | ✘ |
| v41 | 상자 + 쥐끼리 공격 | 고양이 | 14 | **13** | **1 (`or True`)** | 0 | 중 | ✘ |
| v42 | 고양이 전직 | 고양이 | 19 | 19 | 0 | 0 | 중 — `_clientId=0` 하드코딩 · 20초 폴링 | ✘ |
| v43 | 쥐 직업 + 2단계 | 고양이 | 15 | 15 | 0 | 0 | **높음** — 절대좌표 `(±12,±12)` · `230<remaining<=240` 타이밍 | ✘ |
| v44 | 2:8 밸런스 120×120 | 고양이×2 | 20 | 20 | 0 | 0 | **매우 높음** — 맵 인덱스 2 · rooms 49/cheese 14/fort 8/half 60/alcoves 6/1400 **전부 상수** · 발소리 14m·4m·이속 7.8 실측값 상수 | ✘ |
| v45 | 직업·2단계 **UI 글자** | 고양이 | 19 | 19 | 0 | 0 | 중 — 3초 팝업 만료 · 타이머 문자열 `"4:" or "3:5"` | ✘ |
| v46 | 테크 뼈대 + 상점 자리 | **쥐** | 11 | 11 | 0 | 0 | 중 — **품절을 `TestClearSoldOut`으로 걷는다**(`:62`), 실패해도 `print`만(`:66`) | ✔ |
| v47 | 쥐 테크 효과 9항목 | **쥐** | 14 | 13 | 0 | **SKIP 1** | **매우 높음** — sleep 29회 · 11초 장판 만료 · 8초 수입 표본 2회 · 창고 자리 12개 시행착오 | ✔ |
| v48 | 고양이 테크 효과 7항목 | 고양이 | 10 | 9 | 0 (`note()` **정의만 하고 미사용** `:40-44`) | **SKIP 1** | **매우 높음** — sleep 25회 · 광분 10초 만료 루프 · 쿨다운 25초 대기 | ✔ |
| v49 | 미니맵 6항목 | 고양이 | 9 | 7 | 0 | **NOTE 2**(둘 다 조건부) | 낮음~중 — `poll()`로 sleep 대체 ✔ · `_index=0` 하드 인덱스 · 30×30 전제 | ✔ |
| v50 | 무작위 3 + 전직 300 + 봇 상점 | 고양이·창고맵 | 17 | 13 | 0 | **NOTE 4** | **설계상 무작위 의존** — `ROUNDS=3`으로 「서로 다른 값 ≥2」를 잰다. docstring이 **우연 일치 1.4%/3%** 를 자인(`:20-24`) | ✔ |

### 2-2. SKIP·NOTE 전량

| 종류 | 위치 | 내용 |
|---|---|---|
| SKIP | `qa_v47.py:212` | ⑦-b 숨은 창고 Lv3 무음 — **「재는 훅이 없다 — `TestGetSfxPlayCount`가 필요」** |
| SKIP | `qa_v48.py:307` | ⑦ 원샷 애니메이션 — **「재는 훅이 없다 … 눈으로 볼 것」** |
| NOTE(조건부) | `qa_v49.py:164`, `:214` | 시작 치즈 마커 ≠ 0 · 상점 안 거절 자리 |
| NOTE(조건부) | `qa_v50.py:189`, `:258`, `:347` | 판 재시작 · **품절 조합이 안 바뀜(무작위 실패를 FAIL 대신 NOTE로 강등)** · 상점 미도달 봇 |
| NOTE(무조건) | `qa_v50.py:356` | 「미니맵 상점 마커 = 씬 상점 수」 — **report-only, 값이 틀려도 통과** |

**핸드오프의 QA 부채 3건 대조**: ① 원샷 애니 재생 훅 없음 → `qa_v48.py:307` SKIP으로 **정직하게 남아 있다** ✔ ② 무음 창고 SFX 카운트 훅 없음 → `qa_v47.py:212` SKIP ✔ ③ 끈끈이 겹침 EditMode 전용 → `StickyLogicTests.cs` 확인. **부채 셋 다 문서와 코드가 일치한다** — 이 부분은 정직하다.

### 2-3. 실패해도 계속 가는 `print` 전용 경고 (기록도 안 된다)

| 위치 | 내용 |
|---|---|
| `qa_v46.py:66` · `qa_v47.py:144` | `!! 품절이 안 걷혔다 — 아래 구매가 그 테크에서 거절된다` → 그대로 진행 → **아래 전부 거짓 FAIL** |
| `qa_v48.py:215` | `!! 계측대가 안 섰다 — ③⑥의 피해 배율을 잴 수 없다` → **그래도 ③⑥을 잰다** |
| `qa_v50.py:187`, `:198` | 판 재개 실패 → `break` / `return 1` |

### 2-4. **조용한 조건부 건너뛰기** — SKIP도 NOTE도 안 남는다 (F-11)

| 위치 | 사라지는 항목 |
|---|---|
| `qa_v36.py:162` `if fpos:` | **요새 6항목이 통째로** |
| `qa_v36.py:247` `if dead:` | 따봉 2항목 |
| `qa_v37.py:214` `if dead:` | 관전자 채팅 2항목 |
| `qa_v38.py:128` `if remaining > 0:` | 「고양이는 못 먹는다」 |
| `qa_v41.py:135/144` if/else | 둘 중 하나만 |
| 조기 반환 13곳 | `v38:32` `v40:32,50` `v41:43` `v42:32` `v43:32` `v44:81` `v45:35` `v46:51` `v47:124` `v48:142` `v49:113` `v50:169` — **나머지 전부 미실행, RESULTS에 흔적 없음** |

→ **분모 `len(RESULTS)`가 실행마다 달라진다.** `qa_v36.py:276`의 `"%d/%d"`가 `16/16`이어도 24항목 중 8개를 안 돈 것일 수 있고, **출력만 봐서는 구분이 안 된다.**

---

## 3. 거짓말하는 도구 — 최우선 산출물

| 심각도 | 무엇 | 어떻게 거짓말하는가 | 근거 | 판정 |
|:--:|---|---|---|:--:|
| **치명** | `InvokeBridge` **인자 바인딩** | ① 루프가 **파라미터만** 돈다 — `data`의 안 쓰인 키를 확인하는 코드가 **없다** → 오타 kwargs는 **에러 없이 무시**. ② 대응 파라미터는 `Activator.CreateInstance` = **`0`/`0f`/`false`/`null`**. `_clientId` 오타 → **0 = 호스트**. ③ 이름이 안 맞아도 **`data.Count == ps.Length`면 JObject 순서대로** 꽂힌다. ④ 메서드 이름은 `IgnoreCase` | `InvokeBridge.cs:93` `data[p.Name]` · `:94-102` 위치 폴백 · `:105` `default(T)` · `:84` IgnoreCase | 확인 |
| **치명** | 위 사고의 **전례가 코드 주석에 남아 있다** | *"qa_v37이 `_clientId`를 넘겨도 시그니처에 없어 통째로 버려졌고, 고양이가 늘 호스트(먼저 스폰된 쥐)를 잡아 **엉뚱한 쥐를 가뒀다**(2026-08-09 규명)"* — **다리는 그대로다** | `TestManager.Map.cs:26-29` | 확인 |
| **치명** | 스위트 **종료 코드** | 7개가 절반 실패해도 **exit 0**. 사람이 stdout을 안 읽으면 통과로 보인다 | `qa_v37:230` 외 6곳 | 확인 |
| **치명** | `TestGetBotFirstSight(Distance)` | 「첫 발견」이 **최솟값이 아니라 `m_Agents` 목록 첫 고양이의 값**. 죽은 고양이는 목록에서 제거되므로 **고양이가 죽을수록 「첫 발견」이 늦어진다**. 진단서 전 표의 「첫 발견」 칸이 이 값이다 | `TestBotManager.cs:292-309` (`return` 즉시 · `Math.Min` 아님) · `run_matches.py:189` | 확인 |
| **높음** | `qa_v41.py:77` | `check("상자 — 부수면 안의 것이 나온다", after > before **or True**)` — **절대 실패 불가** | `qa_v41.py:77` | 확인 |
| **높음** | `qa_v49.py` 미니맵 ⑥ | `TestGetMinimapNearestMarkerMeters`가 **미니맵이 없으면 -1**을 주는데, 파이썬이 `-1 → float("inf")`로 바꿔 `all(d >= 임계)` 를 통과시킨다. **미니맵이 통째로 없어도 「적 마커 0」이 통과** | `qa_v49.py:97, 245` ← `TestManager.Minimap.cs:75` | 확인 |
| **높음** | `NavStats().detour` / `short` | `LastDetour`는 **마지막 경로 한 건**이고, 코너가 없으면(=길을 못 찾아 포기했으면) **`1f`(만점)**. **포기가 지표를 좋게 만든다.** 봇마다 그 한 건을 평균 내 판 통계로 제시한다. 진단서의 「detour 1.00 = 봇이 곧장 간다」가 이 값이다 | `Ai/NavRoute.cs:373-378` · `TestBotManager.cs:513-539` | 확인 |
| **높음** | `TestGetMapTotalCheese` · `TestGetDesignedSpotReserve` | 훅은 **로비 사람 쥐 수**로 계산하고, 게임은 **`CountSpawnedMice()`(봇 포함 실제 몸)** 로 깐다. 디렉터 주석이 *"로비 목록이 아니라 실제로 선 몸을 센다 … 측정용 봇도 캔다"* 라고 명시했는데 **훅이 안 따라갔다.** 봇이 낀 판에서 최대 8배 어긋난다. 덧붙여 `ReservePerSpot`을 **같은 식으로 다시 부른다**(구현 미러) | `TestManager.Balance.cs:55-63, 92-104` vs `CheeseSpotDirector.cs:148-160` | 확인 (**현재 부르는 스위트는 사람만 있는 v38·v41이라 아직 안 물렸다** — 잠복) |
| **높음** | `TestGetSetting` / `TestSetSetting` | `default:`가 **둘 다 sfx**다. 오타 키(`"volume"`·`"master"`)가 **쓰기도 읽기도 sfx로** 가서 왕복 단언이 **자기 일관되게 통과**한다 — 어떤 단언으로도 못 잡는다 | `TestManager.View.cs:222-225, 255-257` | 확인 |
| **높음** | `MouseCheese()` · `MouseTechStats()` | **산 쥐만** 센다(`a.Body == null` continue). 전멸 판에서 `mice=0 techs=0`. 같은 질문에 답하는 `MouseReport()`는 퇴장분을 이월하므로 **두 훅의 답이 다르다** | `TestBotManager.cs:312-323, 332-347` vs `:388-414` | 확인 |
| **높음** | 「없음」 = 「정상값」 센티널 | `TestGetPhase` → **PhaseDirector 없으면 `"Hunt"`**(`qa_v43.py:38`이 그걸 단언한다) · `TestGetMouseJob`/`TestGetCatClass`/`TestGetMatchResult` → `"None"` · `TestGetCatFrenzyActive`/`TestGetCatSkillActive` → **컴포넌트 없어도 `false`** · `TestGetBotCount`/`TestGetBotMouseCheese` → `0`. **미니맵 훅만 -1 + 경고를 쓴다** | `MouseJob.cs:47-52` · `Tech.cs:295-300` · `Bots.cs:33-45` ↔ `Minimap.cs:36-56` | 확인 |
| **높음** | `TestGetCheerMarkerCount` | `gameObject.name == "CheerMarker"` **문자열 매칭**. 마커 이름이 바뀌면 영원히 0 → 「고양이 화면에는 0이어야 한다」가 **항상 통과**. 같은 파일의 `HudText` 주석이 *"이름으로 GameObject를 찾으면 배선됐다고 착각한다"* 고 경고하는데 **여기서 그 짓을 한다** | `TestManager.View.cs:76-86` vs `Hud.cs:73-83` | 확인 |
| 보통 | `TestGetWarehouseTicks` | 주석이 *"소리·이펙트가 발화한 횟수와 같다"* 고 **훅이 확인하지 않는 등식**을 단언한다. 소리 호출을 떼도 값은 그대로 | `TestManager.Economy.cs:368-377` | 확인 |
| 보통 | `TestKillCat` | 이름은 복수인데 **첫 마리에서 `return`**. 2고양이 판에서 「쥐 승」이 안 난다 | `TestManager.cs:255-264` | 확인 |
| 보통 | `TestGetJobTicketPositions` | 「서버 전용」 주석뿐, `IsServer` 검사 없음. 클라에서 부르면 `Gang:\|Hero:` → **두 판 비교가 「자리가 안 바뀐다」로 읽힌다** | `TestManager.Map.cs:402-410` | 확인 |
| 보통 | `TestGrantAdvanceMischief`·`TestMaxOneUpgrade`·`TestMaxAllUpgrades` | 「서버 전용」인데 가드가 훅이 아니라 **피호출부**에 있다 → 클라에서 부르면 **경고 없이 무동작** | `CatClass.cs:60-91` ← `PlayerMovement.cs:90` | 확인 |
| 보통 | `TestBurnPhaseClock` | `m_Remaining` **필드명 문자열** 리플렉션. 이름이 바뀌면 **로그 한 줄 없이 무동작**(CLAUDE.md 함정 #1과 같은 부류) | `MouseJob.cs:68-78` | 확인 |
| 보통 | `TargetInvocationException`을 안 푼다 | C# 예외 메시지·스택이 `"Exception has been thrown by the target of an invocation."` 로 **소실**된다 | `InvokeBridge.cs:108` | 확인 |
| 보통 | `invoke()`에 **타임아웃 없음** | 게임이 멈추면 **영원히 대기**. 같은 패키지의 `sync_wrapper`는 `wait(timeout=30)`을 쓴다 | `wesqa/__init__.py:46` · `poco/.../simplerpc.py:77` | 확인 |
| 보통 | `qa_v40.py:116` == `:119` | **동일 표현식**을 두 번 단언. 항목 수만 +1 | `qa_v40.py:116, 119` | 확인 |
| 보통 | 부호만 재는 단언 | `struct_after >= struct_before`(구조물 HP는 회복 수단이 없어 감소만 가능) → **총알이 아예 안 나가도 통과** · `fun_after <= fun_before` 동일 | `qa_v41.py:142` · `qa_v38.py:72` · `qa_v41.py:87` | 확인 |
| 보통 | 기대값을 detail에만 쓴 단언 | `check("① 상점 자리가 맵에 섰다", spots >= 1, "%s곳 (CSV 기대 4)")` — **4→1 회귀를 못 잡는다** | `qa_v46.py:80` | 확인 |
| 낮음 | 준-항진명제 | `all(i >= 0 for i in ids)`(연결 실패면 `invoke`가 예외를 던진다) — 6개 스위트가 같은 줄을 쓴다 · `TestGetLocalHp() > 0`(아직 아무도 안 때렸다) · `TestGetBoxCount() == 0`(벽을 안 부쉈다) | `qa_v37:39` `v46:49` `v47:121` `v48:139` `v49:110` `v50:165` · `v42:80` · `v43:42` | 확인 |
| 의심 | 위치 폴백이 **실제로** 잘못 바인딩한 사례 | 코드 논리상 가능하나 로그 증거를 못 찾았다 | — | 의심 |
| 의심 | `--speed 5`/`6` 배속이 봇 승률을 얼마나 왜곡하는가 | **측정도 가드도 없어 평가 불가** | `run_matches.py:35, 80` | 의심 |

---

## 4. EditMode 테스트 품질 (66파일 · `[Test]` 628개)

**총평: 예상보다 높다.** assert 없는 테스트 0 · `Assert.IsTrue(true)` 류 0 · try/catch 삼킴 0 · 조기 `return` 사실상 0. 문제는 **동어반복 ~20건**과 **커버리지 공백**에 집중된다.

| 문제 유형 | 해당 파일:줄 | 예 | 왜 못 잡는가 |
|---|---|---|---|
| **결정성 동어반복 (13건, 최대 집단)** | `PlacementLogicTests:57` · `PantryTextureLogicTests:36` · `ShopSpotLogicTests:71,101,216` · `MapGeneratorTests:52` · `TicketShuffleLogicTests:11` · `TechRandomLogicTests:26` · `MapDressLogicTests:25` · `RoomModuleLogicTests:48` · `MatchRandomLogicTests:19,105` · `BtNodeTests:131` | `CollectionAssert.AreEqual(SpreadPick(pts,4,0), SpreadPick(pts,4,0));` | 대상이 전부 **로컬 `new System.Random(seed)`/순수 해시**다 — `UnityEngine.Random` 전역을 안 쓴다. **같은 인자로 순수 함수를 두 번 부르는 것**이라 구조적으로 실패 불가 |
| **CSV가 자기 자신과 같은지 검사** | `CatClassLogicTests:57-59` | `AreEqual(Balance.CatAdvanceMischief, CatClassLogic.MischiefThreshold)` | `MischiefThreshold`가 그 필드의 패스스루(`CatClassLogic.cs:66`). 좌우변이 같은 필드 |
| **구현 공식 미러링** | `MouseCombatLogicTests:57` · `FunLogicTests:38` · `CheeseSpotLogicTests:102,112` · `AlcoveSpotLogicTests:102,103,107,133` | `AreEqual(RANGE * RANGE_USABLE_RATIO, UsableRange(RANGE), 0.001f)` | 구현식을 글자 그대로 재작성. 비율을 0.85→0.5로 바꿔도 통과 |
| **어서트가 0회 실행** | `AlcoveSpotLogicTests:113-136` | `var cheese = new List<CheesePlacement>();` … `foreach (cp in cheese) { Assert… }` | **빈 리스트**라 내부 assert가 한 번도 안 돈다. 이름(`RoomyMap_NeverNeedsTheFallback`)이 약속한 「폴백을 안 탔다」를 **전혀 검증하지 않는다** |
| **`Assert.Ignore`로 영구 초록** | `AnimationStateLogicTests:75-79` | `if (!File.Exists(path)) { Assert.Ignore(…); return; }` | 경로가 틀어지면 두 테스트가 **영영 초록**. `Assert.Fail`이어야 한다 |
| **알려진 제약을 못박음** | `CatBotLogicTests:72-79` | `PurchaseOrder_NeverBuysFrenzyOrEar` — `AreEqual(-1, IndexOf(CatFrenzy))` | **사용자가 지목한 `NextPurchase_NeverBuysGun`과 같은 패턴의 잔존물.** 쥐 쪽은 **이미 뒤집혔는데**(`MouseBotLogicTests:88-99`, 주석이 봇 감사 A3을 인용) **고양이 액티브·청각 축이 계측에서 통째로 빠져 있다**. 개선이 들어오면 이 테스트가 **먼저 막는다** |
| 죽은 항목을 못박음 | `MouseBotLogicTests:117-119` | 주석이 스스로 *"큰 주머니는 여섯째 — 칸 상한 5라 실전에서는 사실상 안 사진다(감사 A3)"* | 죽은 구매를 테스트가 고정 |
| 소프트 임계치 | `MapGeneratorTests:93-101` | `GreaterOrEqual(ok, 90)` (100 중) | 10% 실패 허용. 게다가 `BuildTestPool()`은 **테스트 전용 5방 축약 풀** — 실제 프로덕션 후보풀은 **한 번도 생성 검증되지 않는다** |
| 선언 복사 | `AnimationStateLogicTests:53-57` | `AreEqual("Spin", AnimationStateLogic.Spin)` | change-detector |
| 내부 모순 | `MinimapPainterTests:66-67` vs `:77` | `:67` 주석 전제와 `:77` 어서트가 어긋나 `:52`와 중복 | — |
| **EditMode인데 순수 로직이 아님 (4파일)** | `MinimapPainterTests:18`(`AssetDatabase.LoadAssetAtPath` + `Texture2D`) · `MapTriplanarShaderTests:11`(`Shader.Find`) · `NetworkObjectHashTests:31`(**상대경로**로 `.unity`/`.prefab` YAML 정규식 파싱) · `AnimationStateLogicTests:73` | — | 에셋 이동·CWD 변경에 깨진다. 나머지 62파일은 진짜 순수 로직 ✔ |

### 무테스트 서브시스템

| 폴더/영역 | 파일 | 비고 |
|---|:--:|---|
| **`Testing/` (TestManager 15파일)** | 15 | **0** — 66개 테스트 어디에도 `TestManager` 문자열이 없다 |
| **`Editor/`** | 54 | **0** — 프리팹 팩토리·씬 빌더·맵풀 빌더 전부. `NetworkObjectHashTests`가 **출력물만** 간접 확인 |
| **`Bots/` 트리·배선** | — | `BtNodeTests`(11)는 **뼈대만**. `BotBrainTrees`(실제 트리 조립) · `BotOrderLogic`(순수 로직!) · `BotAgent`/`BotCommander`/`BotShopper`/`BotNavigator`/**`TestBotManager`** = 0 → **어떤 노드가 어떤 순서로 꽂혔는지 아무도 안 본다** |
| `Intro/` · `Fx/` · `Rendering/` | 9 | 0 (`IntroBootLogic`은 순수 로직인데도 0) |
| 순수 로직인데 0 (바로 쓸 수 있는 공백) | — | `BotOrderLogic` · `ReservationLogic` · `IntroBootLogic` · `HostPortFallback` · `LootBoxLogic` |
| 배선층 0 | — | `TechSlots` · `TechOwner` · `CheeseWallet` · `ShopController` · `ShopHotbar` · `SightLine`(**봇 시야 판정의 근간**) · `NavRoute`(E-F의 출처) · `UserSettings`(E-H의 대상) |

---

## 5. 배포 안전성

**판정: 배포 빌드는 QA 서버를 안 열고 치트 훅도 컴파일되지 않는다 (확인).** 이 프로젝트는 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 규율이 예외적으로 일관되게 지켜져 있다.

| 개발 전용이어야 할 것 | 가드 | 배포에서 빠지는가 | 판정 |
|---|---|:--:|:--:|
| `TestManager` **15 partial 전부** | 파일 전체 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` (예 `TestManager.cs:5/317`) | **예** — 타입 자체가 없다 | 확인 |
| `TestManager` 자동 생성 | `[RuntimeInitializeOnLoadMethod]`가 `#if` **안쪽**(`:32`) | 예 | 확인 |
| `TestBotManager` · `BotLab*` · `BotAgent`/`BotBrainTrees`/`BotCommander`/`BotNavigator`/`BotSenses`/`BotShopper`/`BotOrders`/`SoundLog` | 각 파일 1행 `#if` | 예 | 확인 |
| **WesQA 서버 기동점** | `WesQABootstrap.cs:14` — `[RuntimeInitializeOnLoadMethod]`를 `#if`가 감싼다 | **예** — `Boot()` 호출자 0 | 확인 |
| **`WesQA.dll` 자체** | `WesQA.asmdef` — `includePlatforms:[]`, `defineConstraints:[]` | **아니오** | 확인 |
| BotLab **씬** | `BuildWindowsMenu.cs:48,53,59` `_includeBotLab` | 예 | 확인 |
| `Editor/` 54파일 | `CTC.Editor.asmdef` → `includePlatforms:["Editor"]` | 예 | 확인 |
| EditMode 테스트 | `includePlatforms:["Editor"]` + `defineConstraints:["UNITY_INCLUDE_TESTS"]` | 예 (이중) | 확인 |
| `GameDebug.*` | `[Conditional("DEVELOPMENT_BUILD")]` | 호출만 제거(클래스 잔존) | 확인 |
| `DebugConsoleGate` · `FrameStatsOverlay` · `ConnectionUI` | `#if` 부분 / 역 `#if` + `enabled=false` | 동작은 예 | 확인 |
| `MultiplayerQaProbe` | 클래스 껍데기 무가드(**의도적** — 씬 직렬화 유지), 본문 `#if UNITY_EDITOR` | 동작은 예 | 확인 |

### WesQA 서버 개항 조건

| 항목 | 사실 | 근거 |
|---|---|---|
| 포트 | `5001 + MPPM index`, 실패 시 `+10` 5회 폴백 | `WesQABootstrap.cs:24` · `WesPocoServer.cs:42-47` |
| 바인드 | **`IPAddress.Loopback`** — 외부 노출 없음 ✔ | `WesPocoServer.cs:47` |
| 인증 | **없음** — `AcceptTcpClient()` → 즉시 처리 스레드 | `WesPocoServer.cs:118-133` |
| 포트 공지 | 시스템 temp에 **평문** 기록 | `WesPocoServer.cs:100-108` |
| 릴리스에서 리스닝? | **아니오** | 위 |

### 빌드 메뉴

| | 개발용 | 배포용 |
|---|---|---|
| 옵션 | `BuildOptions.Development` (`BuildWindowsMenu.cs:53`) | **`BuildOptions.None`** (`:59`) ✔ |
| BotLab 씬 | 포함 | 제외 ✔ |
| `ProjectSettings.asset:878` | `scriptingDefineSymbols: {}` — 수동 `DEVELOPMENT_BUILD` 주입 **없음** ✔ | |
| WesQA 에셋 제외 | **없음** | **없음** |

### 남은 문제 2건

| # | 문제 | 왜 | 비용 |
|---|---|---|---|
| 1 | **`WesQA.dll`이 릴리스 바이너리에 실린다** (동작은 안 함) | asmdef에 `includePlatforms`/`defineConstraints`가 비어 있다. `WesPocoServer`(public `Start()`)와 `InvokeBridge`(**임의 메서드 리플렉션 호출기**)가 공개 API로 배포된다. Windows 백엔드가 Mono라 스트리핑에 기대기 어렵다 | **0** — 게임 코드가 WesQA를 **전혀 참조하지 않는다**(단방향). `defineConstraints` 한 줄이면 끝 |
| 2 | **「개발용 빌드」에는 치트면 전체가 살아 있다** | `BuildWindowsMenu.cs:11-12`가 이 산출물을 「테스터 배포용」으로 규정하는데, `DEVELOPMENT_BUILD`가 켜지면 §1의 변경 훅 전부 + loopback 5001 **무인증** TCP가 동작한다. `IsServer` 검사가 다중 사용자 부정행위는 막지만, **호스트 역할 테스터는 치즈 지급·즉사·타이머 조작을 전부 쓸 수 있다** | 신뢰된 내부 테스터 밖으로는 배포 불가 |

---

## 6. 제안 테스트 케이스 — **계속 쓸 자산**

### 규칙

| | |
|---|---|
| **ID는 영구다** | `E-01`… 번호를 **재사용·밀기 금지**. 폐기하면 「폐기」로 표시하고 **번호는 비운다** |
| **케이스 본문은 동작으로 쓴다** | 줄 번호는 「근거」 칸에만. 코드가 움직여도 케이스는 안 썩는다 |
| **자동/수동을 가른다** | 수동은 **절차 → 기대 결과**를 재현 가능하게. 「잘 도는지 확인」은 케이스가 아니다 |
| **잡는 회귀를 한 줄로** | 그게 없으면 그 케이스는 지워도 된다는 뜻이다 |
| **겹치면 겹친다고 쓴다** | 기존 테스트를 대체하는지 보강하는지 명시 |

### 6-1. 자동 — 다리·계약 (게임 없이 돈다 · EditMode/pytest)

| ID | 케이스 (동작) | 어디에 | 잡는 회귀 | 겹침 | 근거 | 우선 |
|---|---|---|---|---|---|:--:|
| **E-01** | `InvokeBridge.Invoke`에 **메서드가 안 쓰는 키**가 섞인 `data`를 주면 **예외를 던진다** | EditMode `InvokeBridgeTests` (JObject 직접 조립 · 게임 불필요) | 인자 이름 오타가 조용히 `default(T)`가 되는 것 | 없음 (신규) | `InvokeBridge.cs:88-106` | **최우선** |
| **E-02** | 기본값 없는 필수 인자를 **빠뜨리면 예외**. `long _clientId`가 **0(=호스트)** 으로 채워지지 않는다 | 위와 같음 | 2026-08-09 「엉뚱한 쥐를 가둔」 사고의 재발 | 없음 | `InvokeBridge.cs:105` · 전례 주석 `TestManager.Map.cs:26-29` | **최우선** |
| **E-03** | 이름이 안 맞는 kwargs가 **개수만 같다고 순서로 바인딩되지 않는다**(위치 폴백을 「전부 이름이 맞을 때만」으로 좁힌 뒤 그걸 고정) | 위와 같음 | 오타 인자가 **다른 인자 자리로 들어가** 조용히 틀린 값이 되는 것 | 없음 | `InvokeBridge.cs:94-102` | **최우선** |
| **E-04** | 파이썬 스위트가 부르는 **모든 훅 이름 문자열**이 `TestManager`의 `public Test*` 명부에 **정확한 대소문자로** 존재한다(리플렉션 명부 ↔ `invoke("...")` 정규식 수집 대조) | pytest `test_hook_names.py`(신규) — 명부는 EditMode가 JSON으로 뱉거나 소스 정규식 | 훅 이름을 바꿨는데 파이썬이 안 따라와 **런타임에만 터지는** 것 · `IgnoreCase`가 가려 온 오타 | 없음 | `InvokeBridge.cs:84` | **최우선** |
| **E-05** | 각 `qa_v*.py`의 `main()`이 **실패 시 0이 아닌 값을 반환**하고, 모듈 최하단이 `sys.exit(main())`이다 | pytest `test_suite_exitcode.py`(신규 · 정적 AST 검사, 게임 불필요) | 절반이 실패해도 통과처럼 보이는 것 · CI에 못 물리는 것 | 없음 | 7개 스위트 최하단 | **최우선** |
| **E-06** | `TestManager`의 **모든 `Test*` 반환값 규약**: 「못 찾음」 값이 **정상값과 겹치지 않는다**(수치 → `-1`/NaN · 문자열 → `""` · 열거 문자열 → 정상 목록 밖 값 · bool 반환은 「못 찾음」을 못 표현하므로 **금지**하고 3값으로 바꾼다). 모르는 인자 이름에는 **경고를 남긴다** | EditMode `TestManagerContractTests`(신규 · 리플렉션 전수) | `TestGetPhase` → `"Hunt"` · `TestGetCatFrenzyActive` → `false` · `TestGetBotMouseCheese` → `0` 처럼 **미배선이 정상으로 읽히는 것** | 없음 | 표준은 `TestManager.Minimap.cs:36-56`(**-1 + 경고**) | **높음** |
| **E-07** | `qa_v*.py` 린터: **`or True`** · 단독 `>= 0` · 단항 부등호만으로 끝나는 `check` · **훅의 실패 센티널(`-1`·`""`·`"None"`)을 성공 쪽으로 변환하는 자리**를 잡는다 | pytest `test_suite_lint.py`(신규 · pytest가 실제로 수집한다) | 절대 실패 불가한 단언이 새로 들어오는 것 · fail-open | 없음 | 표본 `qa_v41.py:77` · `qa_v49.py:97,245` | **높음** |
| **E-08** | 각 스위트가 **선언한 항목 수**와 실제로 실행된 `check` 수가 같다. 다르면 요약이 `N/M (미실행 K)`을 찍는다 | 공용 `check` 헬퍼 + E-07 린터 | 「16/16 통과」가 실은 24항목 중 8개를 안 돈 것인 상황 | 없음 | `qa_v36.py:162,247` 등 | **높음** |
| **E-09** | `invoke()`가 **기본 타임아웃**을 갖고, 초과 시 명확한 예외를 던진다 | `tests/test_connect.py` 확장 (`fake_server`로 무응답 재현) | 게임이 멈추면 무인 실행이 영원히 대기 | `test_connect.py` **보강** | `wesqa/__init__.py:46` | 보통 |
| **E-10** | 훅이 던진 C# 예외의 **원래 메시지가 파이썬까지 온다**(`TargetInvocationException` 언랩) | `InvokeBridgeTests` + `fake_server` 왕복 | 모든 훅 오류가 `"Exception has been thrown by the target of an invocation."` 한 줄이 되는 것 | 없음 | `InvokeBridge.cs:108` | 보통 |

### 6-2. 자동 — 계측기 자신 (EditMode · 집계 로직을 순수 함수로 뽑은 뒤)

> **선행 작업**: `TestBotManager`의 집계(`Retire`·`NavStats`·`MouseReport`·`SightStats`·`CombatStats`)를
> **`BotStatsLogic`(순수 static)** 으로 분리한다. 지금은 `MonoBehaviour` + `#if` 안이라 테스트가 손을 못 댄다.
> 이 프로젝트가 이미 하는 방식이다(`MouseBotLogic`·`BotSenseLogic`이 순수라서 EditMode가 붙어 있다).

| ID | 케이스 (동작) | 잡는 회귀 | 겹침 | 근거 | 우선 |
|---|---|---|---|---|:--:|
| **E-11** | 봇 2마리 중 **하나를 퇴장시킨 뒤** `NavStats`·`MouseReport`·`CombatStats`·`MouseTechStats`·`MouseCheese`가 **퇴장한 쪽 값을 포함**한다 | 봇 감사 **A4의 재발** — 「살아남은 고양이만 잰 값」을 판 통계로 읽는 것 | 없음 | `TestBotManager.cs:357-414` · `:312-347` | **높음** |
| **E-12** | 같은 수량에 답하는 두 훅(`MouseTechStats.techs` ↔ `MouseReport.techs`, `MouseCheese` ↔ `MouseReport.earned`)이 **같은 값을 준다** | 두 훅이 갈라져 어느 쪽을 읽느냐로 결론이 뒤집히는 것 | 없음 | 같은 곳 | **높음** |
| **E-13** | 고양이 A(30초)·B(10초)를 넣고 **A가 목록 앞이어도 첫 목격이 10**이다. **A가 죽은 뒤에도 10**이다 | 「첫 발견」이 늦어질수록 좋아 보이는 것 — 진단서 전 표의 「첫 발견」 칸 | 없음 | `TestBotManager.cs:292-309` | **높음** |
| **E-14** | 길찾기를 **포기한**(코너 0) 경로가 우회율을 **1.00(만점)으로 집계하지 않는다**. 우회율은 **판 전체 누적 평균**이지 마지막 한 건이 아니다 | 「detour 1.00 = 봇이 곧장 간다」 — 포기가 지표를 좋게 만드는 것 | 없음 (`NavRoute`는 **현재 무테스트**) | `Ai/NavRoute.cs:373-378` | **높음** |
| **E-15** | 시야 통계에 **쥐 표본이 안 섞인다**(감사 A5의 고정) · 사인 라벨이 **`죽을때>직전` 두 칸**을 유지한다(A6의 고정) | 이미 고친 A5·A6이 **되돌아가는** 것 | 없음 | `TestBotManager.cs:363-368, 402` | 보통 |
| **E-16** | 치즈 총량을 계산하는 **분모가 하나다** — 훅과 `CheeseSpotDirector`가 **같은 함수**(실제로 선 몸 수)를 부른다 | 봇 판에서 훅이 사람 수로 재 **최대 8배 어긋나는** 것 | 없음 | `TestManager.Balance.cs:55-104` vs `CheeseSpotDirector.cs:148-160` | **높음** |

### 6-3. 자동 — 훅의 정직성 (wesqa 실플레이 항목으로 추가)

| ID | 케이스 (동작) | 어디에 | 잡는 회귀 | 겹침 | 우선 |
|---|---|---|---|---|:--:|
| **E-17** | 모르는 설정 키(`"volume"`)로 **쓰고 읽으면 실패한다** — 지금은 읽기·쓰기가 **둘 다 sfx로 새어** 왕복이 통과한다 | `qa_v37` 옵션 절 + EditMode `UserSettings` 계약 | 오타 키가 **자기 일관되게 통과**하는 것 | `qa_v37:48-56` **보강** | **높음** |
| **E-18** | 미니맵이 **없는 상태**에서 「적 마커 0」 항목이 **실패한다**(-1을 성공으로 바꾸지 않는다) | `qa_v49` ⑥ | 미니맵이 통째로 안 떠도 통과하는 것 | `qa_v49:245` **수정** | **높음** |
| **E-19** | 「고양이 전멸 = 쥐 승」 경로가 실제로 닫힌다 — 고양이를 **전부** 죽이면 결과가 `MouseWin`이다 | `qa_v43`/신규 | 고양이 하나만 죽이는 훅으로 「쥐 승」을 못 재는 것 | 없음 | 보통 |
| **E-20** | 따봉 마커 수가 **오브젝트 이름이 아니라 전용 컴포넌트**로 세어진다 — 마커를 개명해도 수가 안 변한다 | 런타임 컴포넌트 추가 + `qa_v36` 따봉 절 | 「고양이 화면에는 0」이 **영원히 통과**하는 것 | `qa_v36` **보강** | 보통 |
| **E-21** | 소리가 **실제로 재생된 횟수**를 세는 훅(`TestGetSfxPlayCount`)이 있고, 창고 생산 수와 **따로** 잰다 | 런타임 훅 + `qa_v47` ⑦-b SKIP 해제 | 「생산 횟수 = 소리 횟수」라는 **확인되지 않은 등식** · 무음 창고 부채 | `qa_v47:212` **SKIP 해제** | 보통 |
| **E-22** | 「서버 전용」이라고 적힌 훅을 **클라이언트에서 부르면 경고를 남긴다**(조용한 무동작 금지) | EditMode 계약 검사(주석 규약 대신 **속성**으로) + wesqa 클론 항목 | `TestGetJobTicketPositions`·`TestGrantAdvanceMischief` 류가 클라에서 **아무 말 없이 아무 일도 안 하는** 것 | 없음 | 보통 |
| **E-23** | 리플렉션으로 잡는 **비공개 필드 이름**(`m_Remaining` 등)이 **존재하지 않으면 실패한다** — 조용한 무동작 금지 | EditMode(리플렉션 대상 전수) | CLAUDE.md 함정 #1 부류의 무음 파손 | 없음 | 보통 |

### 6-4. 자동 — 계측 러너의 위생

| ID | 케이스 (동작) | 어디에 | 잡는 회귀 | 겹침 | 우선 |
|---|---|---|---|---|:--:|
| **E-24** | 사람 플레이어가 **하나라도** 붙어 있으면 러너가 **판을 안 돌리고 종료**한다(경고만 찍고 진행하지 않는다). 역할을 **안 고른** 클론도 잡힌다 | `run_matches.py` + `TestGetNetDiag`에 접속 클라 수 추가 | 오염된 10판을 다 돌린 뒤에야 「이 표는 쓰지 마라」를 읽는 것 | `run_matches:136-139` **강화** | **높음** |
| **E-25** | 러너가 판을 열고 **「실제로 섰다」를 확인**한다 — 남은 시간·역할·시드를 폴링해 확인한 뒤에만 봇을 세운다 | `run_matches.open_lobby` (`qa_v50`의 `restart_round` 방식을 재사용) | 「1판이 80~100초에 `cats=0`으로 끝나던」 부류 | 없음 | 보통 |
| **E-26** | 러너가 **`health_check`를 먼저 돌리고** 실패하면 멈춘다 | `run_matches.main()` | 굳은 봇·경로 소진 상태로 10판을 재는 것 | `health_check.py` **재사용** | 보통 |
| **E-27** | 요약이 **표본 경고와 집계 방식**을 같이 찍는다 — 승률 옆에 「N판 — 0/N과 2/N은 유의하지 않다」, 첫 발견도 **중앙값**으로 통일 | `run_matches.summarize()` | 10판짜리 승률 차이를 결론으로 인용하는 것 | 없음 | 낮음 |

### 6-5. 자동 — EditMode 자체 정비

| ID | 케이스 (동작) | 잡는 회귀 | 겹침 | 우선 |
|---|---|---|---|:--:|
| **E-28** | 고양이 봇 구매 순위에 **광분·예민한 귀가 들어 있고**, 봇 판 CSV에 그 둘이 **실제로 등장**한다 | 고양이 액티브·청각 축이 **계측에서 통째로 빠지는** 것 | `CatBotLogicTests`의 `PurchaseOrder_NeverBuysFrenzyOrEar`를 **뒤집는다**(쥐 쪽은 이미 뒤집혔다) | 보통 |
| **E-29** | 어서트가 **한 번도 실행되지 않는 테스트가 없다**(빈 컬렉션 위의 `foreach` 안 assert 금지) | 이름만 그럴듯하고 아무것도 안 재는 테스트 | `AlcoveSpotLogicTests`의 폴백 테스트를 **수정** | 보통 |
| **E-30** | 에셋을 못 찾으면 **`Assert.Ignore`가 아니라 실패**한다 | 경로가 틀어져 테스트가 **영영 초록**이 되는 것 | `AnimationStateLogicTests` **수정** | 보통 |
| **E-31** | 결정성 테스트는 「같은 씨앗 → 같은 결과」**만이 아니라** 「**다른 씨앗 → 다른 결과**」를 같이 잰다 | 순수 함수를 두 번 불러 놓고 잰 척하는 13건 | 올바른 형태가 이미 `TicketShuffleLogicTests`·`TechRandomLogicTests`에 있다 — 그 꼴로 통일 | 낮음 |
| **E-32** | 에셋·셰이더·씬 YAML을 만지는 테스트는 **EditMode 논리 테스트와 분리**된 범주에 있다(범주 이름으로 걸러 돌릴 수 있다) | 에셋이 움직였을 때 **논리 회귀와 구분이 안 되는** 것 | 해당 4파일에 범주만 붙인다 | 낮음 |

### 6-6. 수동 — 절차 → 기대 결과

> 자동화할 훅이 아직 없거나(E-21이 들어오면 M-01은 폐기), OS가 만드는 상태라 재현이 불가능한 것들이다.
> **각 케이스는 「무엇을 보면 통과인가」를 눈으로 판정할 수 있게 적었다.**

| ID | 절차 | 기대 결과 | 잡는 회귀 | 자동화 조건 |
|---|---|---|---|---|
| **M-01** | ① `Tools/CTC/Build Windows (개발용)` ② 호스트로 판 시작 ③ 고양이로 대시·끈끈이·요새·광분·구매·채굴을 **각각 1회** 사용하며 화면 녹화 | 여섯 동작 **각각**에 대해 **한 번씩만** 재생되는 애니메이션이 보인다. 같은 동작을 연타해도 재생이 **겹치지 않는다** | 원샷 애니가 안 나가거나 루프로 도는 것 | **E-21**(재생 횟수 훅)이 들어오면 자동화 → **이 케이스 폐기** |
| **M-02** | ① 채팅 입력창을 연다 ② **한글 IME로** 「가나다」를 조합해 마지막 음절을 **확정하지 않은 채** Enter | 마지막 음절이 **잘리지 않고** 그대로 전송된다 | 2026-08-09 한글 절삭 버그의 재발 | OS IME 상태라 **자동화 불가** — 영구 수동 |
| **M-03** | ① `Tools/CTC/Build Windows (배포용)` ② `Builds/Windows`에서 `WesQA.dll` 존재 여부 확인 ③ 실행 후 `netstat -ano` 로 **5001~5051** 리스닝 확인 ④ 바이너리에서 `TestManager` 문자열 검색 | ② **없어야 한다**(E-27 조치 후) · ③ **아무것도 안 뜬다** · ④ **안 나온다** | QA 서버·치트면이 배포 빌드로 새는 것 | 빌드 후 스크립트로 자동화 가능(우선순위 낮음) |
| **M-04** | ① 스팀을 **끈다** ② 메인 에디터 + MPPM 클론 2로 3인 판 ③ `qa_v36`(호스트=고양이)·`qa_v37`(호스트=쥐)을 각각 돌린다 | 두 스위트 모두 **항목 수가 선언값과 일치**하고(미실행 0), 종료 코드가 0이다 | MonoBehaviour 재배치가 깨뜨린 배선 — **EditMode가 못 덮는 층** | E-08 이후 「미실행 0」은 자동 판정 가능. **3인 실플레이 자체는 영구 수동** |
| **M-05** | ① 같은 조건으로 `--speed 1`과 `--speed 6`을 각 10판 ② 판 길이·사망 중앙값·첫 발견을 비교 | 두 배속의 지표가 **의미 있게 다르지 않다**. 다르면 **모든 배속 계측치에 그 편향을 적어 둔다** | 배속이 물리·판정을 왜곡한 채 밸런스 결론을 내는 것 | 1회성 교정 실험 — 결과를 문서에 적고 **폐기** |

### 6-7. 폐기 번호

없음. (폐기 시 이 표에 `E-nn — 폐기(사유·날짜)`로 남기고 **번호는 재사용하지 않는다**.)

---

## 7. 케이스 대장을 어디에 두면 썩지 않는가

### 결론 — **「코드가 대장, 문서는 생성물, 어긋나면 테스트가 깨진다」 3단**

이 프로젝트는 **문서를 손으로 동기화하면 썩는다는 걸 이미 겪었다**(CLAUDE.md 규칙 8이 그 흉터다).
그래서 **사람이 손으로 맞추는 안은 전부 탈락**이다. 아래가 그 판정이다.

| 안 | 판정 | 이유 |
|---|:--:|---|
| **케이스 대장 문서 한 장**(ID ↔ 구현 위치 ↔ 마지막 실행 결과) | **탈락(단독으로는)** | 손 동기화가 전제다. 이미 실측된 드리프트가 있다 — `CLAUDE.md:183`의 「qa_v36 14항목」이 **실제 24**다. 대장이 하나 더 늘면 썩을 자리가 하나 더 는다 |
| **테스트 코드 안에 ID를 박는다** | **채택 (기반)** | 테스트를 지우면 ID도 같이 사라진다. **삭제·이동에 자동으로 따라오는 유일한 방식.** NUnit `[Property("CaseId","E-11")]`은 **기존 인프라**라 새 도구가 필요 없고, 러너가 뱉는 결과 XML에 그대로 실린다 |
| **wesqa 스위트에 ID를 심어 실행 결과가 곧 대장** | **채택 (반쪽)** | 스위트가 이미 `check("항목명", 조건, detail)` 꼴이다 — **`case="E-18"` 한 칸만 늘리면** 실행 출력이 곧 「무엇이 언제 돌았나」가 된다. 다만 **안 돌린 케이스는 출력에 안 나오므로** 이것만으로는 「무엇이 무엇을 덮는가」를 못 답한다 |
| **생성된 대장 + 어긋나면 깨지는 테스트** | **채택 (접착제)** | 위 둘을 묶는다. 문서는 **생성물**이라 손대지 않고, 「선언은 됐는데 구현이 없는 ID」·「구현은 있는데 선언에 없는 ID」를 **테스트가 잡는다** |

### 구현 — 3단

| 단 | 무엇 | 왜 이 프로젝트에 맞나 |
|:--:|---|---|
| **1. 선언** | `Assets/Scripts/Tests/EditMode/QaCases.cs` — `E-01`…과 **한 줄 설명 · 자동/수동 · 상태(구현됨/미구현/폐기)** 만 담은 **C# 상수 표**. 케이스의 유일한 원본 | 코드라 컴파일이 지킨다. 오타 ID가 **빌드에서** 걸린다 |
| **2. 부착** | C#: `[Property("CaseId","E-11")]` · 파이썬: `check(..., case="E-18")` · 수동: `QaCases`에 `Manual("M-02", 절차, 기대)` | 둘 다 **기존 문법 안**이다. 새 러너·새 포맷·새 의존성이 0이다 |
| **3. 강제** | EditMode `QaCaseRegistryTests` — ① 선언에 있는 **자동** ID는 반드시 **하나 이상의 테스트/스위트 줄**이 붙어 있다 ② 코드에 박힌 ID는 반드시 **선언에 있다** ③ **폐기 번호는 아무 데도 안 붙어 있다** ④ 대장 마크다운이 **지금 상태로 다시 생성한 것과 같다**(안 같으면 실패하며 새 파일을 옆에 뱉는다) | 이 프로젝트가 이미 쓰는 **전수 검사 관용구**다 — `TestGetUnwiredSfxIds`가 「클립 안 붙은 SfxId」를 전수로 세는 것과 똑같은 모양. 새 개념을 안 들여온다 |

파이썬 쪽 대칭: `tools/wesqa/tests/test_case_registry.py` — `QaCases.cs`를 정규식으로 읽어 스위트의 `case=` 값과 대조한다.
**pytest가 실제로 수집하는 6개 중 하나가 되므로**(§2-0) 사람이 따로 기억할 필요가 없다.

### 대장 문서의 자리

`docs/design-vault/QA 케이스 대장.md` — **머리에 「이 파일은 생성물이다. 손으로 고치지 마라」를 박는다.**
`docs/design-vault/`가 이 프로젝트의 자동 문서 금고이고 `.auto-doc.log`가 이미 도는 자리다.
칸: **ID · 케이스(동작) · 자동/수동 · 붙어 있는 구현(파일 경로 — 줄 번호 없음) · 상태 · 잡는 회귀.**
「마지막 실행 결과」는 **여기 안 적는다** — 그게 손 동기화의 출발점이다. 실행 결과는 러너가 뱉는
`docs/balance/*.csv`와 EditMode 결과 XML에 남고, 대장은 **덮개(무엇이 존재하는가)만** 답한다.

### 지금 당장의 효과

「무엇이 무엇을 덮는지 아무도 모른다」가 **§4의 무테스트 표를 자동으로 만들어 주는 상태**로 바뀐다 —
`Testing/` 15파일 · `Editor/` 54파일 · `TestBotManager`가 **선언은 됐는데 구현이 없는 ID**로 대장에 뜨고,
그 줄이 채워지지 않는 한 계속 눈에 남는다. 문서가 아니라 **테스트가 그걸 기억한다.**

---

## 8. 한계

- **정적 분석만이다.** 플레이모드·에디터를 안 썼다. 「런타임에 실제로 그 값이 나온다」는 어느 것도 실행으로 확인하지 못했다.
- 특히 다음은 코드 논리로만 판정했다: 위치 폴백이 실제 사고를 낸 적이 있는가(F-1 ③) · `TestGetPhase`의 `"Hunt"` 폴백이 실제 씬에서 발동하는가 · 배속 5~6이 결과를 얼마나 왜곡하는가(M-05가 그걸 재는 케이스다).
- `qa_v*.py`를 **실행해 보지 않았다** — 항목 수·SKIP·조기 반환은 정적 계수다. 실제 실행 시 조건부로 안 도는 항목이 더 있을 수 있다.
- 다른 감사 4개가 동시에 돌아 **파일이 바뀌었을 수 있다.** 근거 칸의 줄 번호는 **2026-09-08 저녁 기준**이다 — 케이스 본문은 줄 번호에 안 기대게 썼으므로 근거만 다시 맞추면 된다.
