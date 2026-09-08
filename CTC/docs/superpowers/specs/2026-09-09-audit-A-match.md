# 전체 검수 — 도메인 A: 매치·로비·네트워크 (2026-09-09)

> **한 줄 결론: 순수 함수(`*Logic`)는 EditMode가 촘촘히 덮지만, 이 도메인의 위험은 전부 `NetworkBehaviour` 쪽 상태 전이에 있고 그쪽은 사실상 안 덮인다 — 특히 3판 시리즈의 라운드 전환(2단계 미초기화·봇 소실·중복 재시작)이 아무 테스트도 없는 채로 돈다.**

**방법과 한계**: 정적 분석만 했다. 유니티 에디터·플레이모드를 쓰지 않았다(다른 감사 에이전트 4개가 동시에 도는 중). 아래 「확인됨」은 **코드를 읽어 경로가 실재함을 확인한 것**이고, 「의심됨」은 코드상 그렇게 보이지만 런타임 실측이 없는 것이다. 프리팹/씬 배선은 `unity-scanner`로 `Game.unity`의 컴포넌트 존재만 확인했고, 인스펙터 참조 값은 열어 보지 않았다.

읽은 범위: `Match/`(14) · `Lobby/`(9+6) · `Network/`(12) · `Room/`(16) · `Core/`(20) · `Tests/EditMode/`(관련 13개) · `tools/wesqa/tests/qa_v36~v50` · `Assets/Scripts/Testing/TestManager.*`.

---

## 1. 책임 지도

### 매치 진행

| 클래스 | 소유하는 것 | 상태의 주인 |
|---|---|---|
| `MatchManager` | 승패 결과(`m_Result`) · 1단계 남은시간(`m_RemainingSeconds`) · `m_Armed`(로컬) | 서버 기록 → 전원 복제. `m_Armed`·`m_WasStarted`는 **서버 로컬**(복제 안 함) |
| `MatchFlowLogic` | 판정 규칙(순수) | 없음 |
| `PhaseDirector` | 단계(`m_Phase`) · 2단계 시계(`m_Remaining`) | 서버 기록 → 전원 복제 |
| `MouseJob` | 쥐 직업(`m_Job`) | 서버만 씀(`ServerSetJob`), 전원 읽기 |
| `GangRecruit` | 첫 임명 여부(`m_HasRecruited`) | 소유자 RPC → 서버 판정 |
| `MatchSeries` | 판 번호·양측 승수·시리즈 활성(`m_Round`/`m_CatWins`/`m_MouseWins`/`m_Active`) · `m_Handling`(로컬) | 서버 기록 → 전원 복제 |
| `MatchAutoStart` | 없음 (일회성 절차) | 서버 전용 |
| `MatchRandom` | 판 시드(`m_Seed`) + `s_Current` 정적 명부 | 서버가 굴리고 전원이 **같은 순수 함수로 재계산**(목록은 복제 안 함) |
| `MatchStatsRecorder` | JSON 기록 | 서버 로컬 파일, 복제 없음 |
| `CheerDirector` | 따봉 쿨다운 표(`m_NextAllowed`) | 서버 로컬 Dictionary |
| `ResultHud`/`TimerHud`/`MatchSeriesView` | 화면만 | 없음(View) |

### 로비(인게임 `Game.unity`)

| 클래스 | 소유하는 것 | 상태의 주인 |
|---|---|---|
| `LobbySession` | 참가자 목록(`NetworkList<LobbyPlayerState>`) · 시작 여부(`m_GameStarted`) | 서버 기록, 전원 복제. 역할 선택은 클라 RPC → 서버 판정 |
| `LobbyController` | 없음 — 조립자. `Session`/`Spawner`/`MapDirector`/스포너 4종에 위임 | — |
| `PlayerSpawner` | 없음 — 순수 공식 + 스폰 | 서버 전용 |
| `LobbyRoleLogic`/`CursorPolicyLogic` | 규칙(순수) | 없음 |
| `LobbyUI` | 화면 + **커서 잠금의 단독 소유자** | 클라 로컬 |
| `LobbyBrowser`(+View/Controller) | 방 목록·선택(`m_Rooms`/`m_Selected`) | 완전 로컬(스팀 조회 결과) |

### 대기실(`Room.unity`)

| 클래스 | 소유하는 것 | 상태의 주인 |
|---|---|---|
| `RoomSession` | 참가자(`NetworkList<RoomMemberState>`) · 방 이름 · 맵 · 봇 수 · 시작 잠금 · 뽑힌 고양이 | 서버 기록, 전원 복제. 맵·봇은 **방장(=서버 clientId)만** RPC 통과 |
| `RoomDirector` | 없음 — 추첨·씬 전환 절차. `m_Handing`(로컬) | 서버 전용 |
| `RoomAvatarSpawner` | `m_Bodies`(clientId→몸) | 서버 로컬 Dictionary |
| `CatDrawLogic`/`RoomLogic`/`RoomBotLogic` | 규칙(순수) | 없음 |
| `RoomController` | 입력만 | 로컬 |

### 네트워크·코어

| 클래스 | 소유하는 것 | 상태의 주인 |
|---|---|---|
| `MatchHandoff`(Core) | **서버 전용 정적 쪽지** — 역할표·맵·직전 고양이·봇 수·`FromRoom` | 복제 안 함. 씬 전환을 건너는 유일한 통로 |
| `SteamLobbyManager` | 현재 스팀 로비(`m_CurrentLobby`) · 트랜스포트 교체 | 로컬 |
| `HostPortFallback` | 호스트 포트(temp 공유 파일) | 프로세스 밖 파일 |
| `NetworkRoot`/`NetworkRootGuard` | `NetworkManager` 단일성 | 씬 로컬 |
| `NetVersionLogic` | 프로토콜 번호(순수) | 씬 빌더가 굽는 시점에만 적용 |
| `UiFocus`/`EscapeRouter`/`LocalPlayerBinder` | 입력 포커스·ESC/Enter 라우팅·로컬 플레이어 스트림 | 전부 클라 로컬 정적 |
| `MppmAutoConnect`/`MultiplayerQaProbe` | QA 발판 | 에디터 전용 |

---

## 2. 이미 검증되는 것

**단언 내용을 읽고 적었다.** 파일이 있다고 덮인 게 아니다.

| 무엇 | 어디서 | 단언 내용 |
|---|---|---|
| 판정 상태기계 | `MatchFlowLogicTests`(12) | `ShouldArm` 5분기 · 쥐 0=고양이승 · 고양이 0=쥐승 · **동시 전멸은 고양이 승 우선** · 만료 시 쥐 생존승/전멸시 고양이승 |
| 시리즈 규칙 | `MatchSeriesLogicTests`(7) | `IsFinalRound`/`NextRound` 상한 · 3판 전에는 승자 없음 · **무승부가 나올 수 없음**(0~3 전수) · 라벨 문구 |
| 매치 시드 | `MatchRandomLogicTests`(11) | `SubSeed` 결정성 · 소금 3종 비상관(1000 시드 충돌 0) · **0을 절대 안 흘림** · `NextSeed`가 직전과 절대 안 같음 · 엔트로피 고정에도 계속 움직임 |
| 로비 역할 | `LobbyRoleLogicTests`(13) | `CanSelect` 정원·자기 재선택·잘못된 인덱스 · `CanStart` 미선택/고양이 0/쥐 0/1인 · `ClampMapIndex` |
| 스폰 공식 | `LobbyRoleLogicTests` 2건 | 고양이 중앙 라인 · 쥐가 자기 요새 입구 앞(오차 1e-4) |
| 대기실 규칙 | `RoomLogicTests`(11) · `RoomBotLogicTests`(4) | 방장+2인 시작 · 게스트 불가 · 원형 자리 비중첩·안쪽 바라보기 · **봇을 사람 수에 더해 시작 판정** · 남은 자리로 봇 클램프 |
| 고양이 추첨 | `CatDrawLogicTests`(15) | 직전 고양이 제외(3인 이상) · 2인이면 유지 · 전원이 직전이면 되돌림 · 음수 roll · 역할표에 고양이 정확히 1 · 봇 자리 추첨·`BotSplit` |
| 직업·2단계 규칙 | `MouseJobLogicTests`(13) | `IsGangSide` · 임명 4조건(직업 중첩 금지 포함) · 거리 경계 2.5/2.6 · 2단계 승패 4분기 + **전멸이 시간초과보다 우선** · 밸런스 범위(2단계 < 판 길이 절반) |
| 커서 정책 | `CursorPolicyLogicTests`(6) | 로비/결과/모달 3항 전수 |
| 결과 문구 | `ResultLabelTests`(3) | 승자 3종이 서로 다른 문구(삼항 회귀 방지) |
| 스팀 로비/초대 | `SteamLobbyLogicTests`(4) · `SteamInviteLogicTests`(8) | 태그 정확 일치 · 방 라벨 · `+connect_lobby` 파싱 7케이스 + 왕복 |
| 프로토콜 번호 | `NetVersionLogicTests`(7) | patch 무시 · minor 차이 구분 · 비정상 문자열 폴백 |
| Enter 라우팅 | `EnterRouteLogicTests`(3) | 채팅 > 확정구매 > 채팅열기 |
| **실플레이 — 2단계 전이** | `qa_v43`(MPPM 3인) | 직업권 획득→복제 확인 · 임명 거리 · **고양이 사망 → `Phase=Gang` · 결과 None 유지 · 새 시계 4분** · 나머지 쥐 전멸 → `GangWin` |
| 실플레이 — 2단계 UI | `qa_v45` | 화면 글자(`TestGetTimerText`/`TestGetResultText`)로 확인 |
| **실플레이 — 판 재시작 3회** | `qa_v50` | `TestExpireTimer`→결과 확정→`TestRestartMatch`→**1.5초 쉬고** 재시작 → 새 시드·상점 자리·역할 유지. 판 3개의 시드/상점/품절/직업권이 서로 다름 |
| 실플레이 — 로비 흐름 | `qa_v44` | 로비부터 직접 몰아 고양이 정원 2 · 쥐 수 · 맵 선택 |
| 실플레이 — 대기실 | `TestCreateRoom`/`TestRoomForceStart`/`TestSetRoomBots` 훅 존재 | `setup_match.py` 경로에서 쓰임 |

---

## 3. 검증 공백 ← **핵심 산출물**

| 심각도 | 무엇 | 왜 위험한가 | 근거 |
|:--:|---|---|---|
| **높음** | **2단계로 끝난 판의 다음 라운드** — `PhaseDirector.ServerReset()`을 **아무도 안 부른다** | 1판이 `GangWin`으로 끝나면 `m_Phase`가 `Gang`인 채 남는다. `MatchManager.RequestRestart`도 `LobbyController.ResetToLobby`도 단계를 안 건드린다 → 2판이 열리자마자 `MatchManager.Update`가 `IsGangPhase` 가지로 들어가 `EvaluateGangPhase`를 돌린다. 2단계 시계는 0이므로 **시작 즉시 `MouseWin`**. CLAUDE.md 함정 7과 같은 부류인데 그쪽은 고쳐졌고 이건 안 고쳐졌다 | `PhaseDirector.cs:53` (호출자 0) · `MatchManager.cs:125-146` · `MatchSeries.cs:316-342` |
| **높음** | **시리즈 2·3판에는 봇이 없다** | `MatchSeries.StartNextRound`가 `MatchHandoff.Set(roles, map, cat)`을 **봇 인자 없이**(기본 0) 부른다 → `BotMice=BotCats=0`. 게다가 `RequestRestart`가 비-씬 오브젝트를 전부 despawn해 1판 봇의 몸도 사라지고, `MatchAutoStart`는 씬 진입 때 한 번만 돌아 다시 안 세운다. 대기실에서 봇 N으로 연 3판 경기가 **1판만 N명, 2·3판은 사람만** | `MatchSeries.cs:335` · `MatchHandoff.cs:161-176` · `MatchAutoStart.cs:138-141` |
| **높음** | **시리즈 도중 인원이 시작 조건 아래로 떨어졌을 때** | `StartNextRound`가 `m_Lobby.RequestStartGame()`을 부르는데 `LobbyRoleLogic.CanStart`가 거짓이면(1명만 남음·전원 쥐 등) 조용히 아무 일도 안 한다. 그런데 `MatchHandoff.FromRoom == true`라 `LobbyUI` 패널이 안 뜨고(`LobbyUI.cs:112`), `ResultHud`도 결과가 None이라 사라진다 → **아무 UI 없는 빈 인게임 씬에 갇힌다.** 나갈 길이 없다 | `MatchSeries.cs:340` · `LobbySession.cs:343-350` · `LobbyUI.cs:112-116` |
| **높음** | **판 도중 난입·재접속** | `LobbySession.EnsureEntry`가 판 도중에도 목록에 넣는데(`OnClientConnectedCallback`) 역할은 `MatchHandoff.RoleOf` — 이미 `ConsumeRoles()`로 비웠으므로 `None`. 몸은 `ServerSpawnAll`이 판 시작에 한 번 돌았으므로 안 생긴다. **몸 없는 참가자**가 목록에 남아 다음 라운드 `MemberIds()`·`CatDrawLogic`에 섞인다 → 2판 고양이로 **몸이 없는 사람이 뽑힐 수 있다** | `LobbySession.cs:297-303,427-439` · `MatchHandoff.cs:182-186` · `MatchSeries.cs:378-383` |
| **높음** | **결과 화면의 재시작 버튼이 시리즈 중에도 살아 있다** | `ResultHud`는 `MatchSeries.Active`를 모른다 — 호스트에게 버튼을 그대로 띄운다(`nm.IsHost`). 호스트가 누르면 `MatchSeries.Advance()`의 5초 대기와 **동시에** `RequestRestart`가 돈다 → 판 번호를 안 올린 채 로비 상태가 되고, 뒤이어 `StartNextRound`가 또 `RequestRestart`를 부른다. 결과: 판 카운트 어긋남 또는 `m_Handling` 잠김 | `ResultHud.cs:349` · `MatchSeries.cs:293-342` |
| 중간 | **`ShouldArm`이 안 걸린 판의 시간 만료** | 고양이 0(또는 쥐 0)으로 시작하면 `m_Armed`가 영영 false다. 타이머가 0에 닿아도 `Evaluate`가 `None`을 돌려주고, `MatchManager.cs:65`의 폴링 가드(`remaining > 0f`)가 풀려 **매 프레임 `FindObjectsByType<RoleTag>()`가 돈다.** 판은 안 끝나고 프레임만 깎인다. 봇 계측판(`DevForceStartGame`)에서 실제로 도달 가능한 조합 | `MatchManager.cs:59-69,101-122` |
| 중간 | **`GangWin`을 시리즈 점수에 어떻게 넣는가** | `MatchSeries.OnRoundEnded`는 `CatWin`이 아니면 전부 쥐 승으로 센다 — `GangWin`도 쥐 승이 된다. 의도일 수 있으나 **어디에도 안 쓰여 있고 테스트도 없다.** 「깡패 승」과 「쥐 승」이 화면에서는 다른 문구인데 점수판에서는 같다 | `MatchSeries.cs:284` · `ResultHud.cs:356-365` |
| 중간 | **관전자 경계(역할 `None`)** | `PlayerSpawner`가 몸을 안 주는 경로는 주석까지 있는데(`PlayerSpawner.cs:86`) 그 상태의 사람이 로비 목록·시리즈 추첨·`CheerDirector` 수신자 그룹에 어떻게 들어가는지 **테스트가 하나도 없다** | `PlayerSpawner.cs:81-86` |
| 중간 | **연결 끊김·재접속 전 구간** | `LobbySession.OnClientDisconnected`·유령 항목 정리(`Update`, `GameStarted`면 **정지**)·`RoomSession.OnClientDisconnected`·`RoomController.OnDisconnected`·`SteamLobbyManager.OnNetworkDisconnected` 다섯 경로가 있는데 **EditMode·wesqa 어느 쪽도 안 건드린다.** 특히 판 도중 유령 정리가 꺼져 있어 반쯤 죽은 접속이 판이 끝날 때까지 목록에 남는다 | `LobbySession.cs:420-425,444-460` |
| 중간 | **`NetworkVariable` 복제 자체를 확인하는 테스트가 거의 없다** | 도메인 A의 복제 필드 14개 중 wesqa가 **다른 인스턴스에서 읽어** 확인하는 것은 `MouseJob`(v43), 시드(v50 ⑤ 호스트=클론 비교) 둘뿐. `m_Result`·`m_RemainingSeconds`·`m_Phase`·`m_Remaining`·`m_Round`/`m_CatWins`/`m_MouseWins`/`m_Active`·`RoomSession`의 6개는 **호스트에서만 읽는다** — 클라 화면이 비어도 통과한다(2026-08-09 볼륨 사고와 같은 눈속임) | `Testing/TestManager.*` 훅 전수 |
| 중간 | **대기실 → 인게임 인계의 경계** | `MatchAutoStart`의 10초 조인 타임아웃 초과(사람이 덜 붙은 채 시작)·`MapIndex < 0`·`TestBotManager` 부재 세 분기 전부 무테스트. 봇 스폰은 하드코딩 1초 대기 뒤에 돈다 | `MatchAutoStart.cs:132-201` |
| 중간 | **`MatchRandom`이 스폰 전에 굴려질 때** | `ServerRoll`이 `IsSpawned==false`면 `m_PendingSeed`에 담아 두는 경로가 있는데, 그 경로를 타는 테스트가 없다(EditMode는 `NextSeed`/`SubSeed` 순수 함수만 본다). `OnNetworkSpawn`에서 흘리는 부분이 조용히 깨져도 시드 0이 되고, 읽는 쪽이 「로비다」로 오해한다 | `MatchRandom.cs:238-263,274-285` |
| 낮음 | **`HostPortFallback` 포트 소진** | 20개 전부 막힌 경로·`ReadHostPort`가 낡은 파일을 읽는 경로 무테스트. `IsPortFree` 후 실제 바인드 사이의 TOCTOU도 그대로 | `HostPortFallback.cs:170-247` |
| 낮음 | **`LobbyBrowser`의 에디터 분기** | `Refresh()`가 `#if UNITY_EDITOR`에서 **무조건 가짜 목록으로 즉시 return** — 에디터에서는 실제 스팀 목록 경로(`OnLobbies`)가 한 번도 안 돈다. `m_Raw`가 늘 비어서 `Join`의 인덱스 검사도 에디터에선 안 걸린다 | `LobbyBrowser.cs:287-307,314-337` |
| 낮음 | **`CheerDirector` 쿨다운·수신자 그룹** | 훅(`TestCheer`/`TestGetCheerMarkerCount`)은 있는데 **3초 쿨다운**과 **고양이 제외**를 단언하는 스위트가 없다(v37이 채팅 쪽만 본다) | `CheerDirector.cs:66-104` |

---

## 4. 발견한 결함

| 심각도 | 무엇 | 근거 | 상태 |
|:--:|---|---|:--:|
| **높음** | `PhaseDirector.ServerReset()`이 **죽은 코드**다. 로비 복귀 경로 어디에서도 안 불린다 → 2단계로 끝난 판 뒤에 단계가 남는다 | `PhaseDirector.cs:52-58`, 전체 grep 호출자 0(`TestManager`도 안 부른다) | **확인됨**(코드) / 런타임 영향은 **의심됨** |
| **높음** | 시리즈 2·3판에 봇이 안 선다 | `MatchSeries.cs:335`가 `MatchHandoff.Set`의 봇 인자를 생략 → `BotMice/BotCats = 0` | **확인됨** |
| **높음** | 시리즈 중에도 결과 패널의 재시작 버튼이 호스트에게 뜬다(이중 재시작) | `ResultHud.cs:349` — `MatchSeries.Active` 참조 없음 | **확인됨**(경로) / 실제 어긋남은 **의심됨** |
| 중간 | `MatchManager.RequestRestart`가 `m_Lobby`를 **널 검사 없이** 쓴다. 같은 클래스 `Update`(:52)는 검사한다 | `MatchManager.cs:144` `m_Lobby.ResetToLobby();` | **확인됨** |
| 중간 | 시간 만료 + 미장전이면 폴링 가드가 풀려 **매 프레임 `FindObjectsByType`** | `MatchManager.cs:65` `if (m_Timer < POLL && m_RemainingSeconds.Value > 0f) return;` | **확인됨** |
| 중간 | `SteamLobbyManager.UseSteamTransport`가 `FacepunchTransport`를 널 검사 없이 쓴다 → 컴포넌트 누락 시 NRE로 방 생성이 통째로 실패 | `SteamLobbyManager.cs:250-251` | **확인됨** |
| 중간 | `PlayerSpawner.ServerSpawnAll`이 `GetComponent<NetworkObject>()`를 널 검사 없이 쓴다 | `PlayerSpawner.cs:92` | **확인됨** |
| 중간 | `RoomAvatarSpawner`가 자리를 「슬롯 고정」이라 주석했지만 실제로는 **그때그때 목록 인덱스**다. 누가 나간 뒤 새로 들어오면 남아 있던 몸과 **같은 자리**를 받을 수 있다 | `RoomAvatarSpawner.cs:213-219` vs 클래스 주석 :182-184 | **확인됨**(주석 불일치) / 겹침은 **의심됨** |
| 중간 | `EscapeRouter`가 Enter를 **모달 중에도** 읽는다. 옵션 창이 떠 있을 때 Enter가 `m_Chat.Open()`으로 간다. ESC 쪽은 팝업 스택을 보는데 Enter 쪽은 안 본다 | `EscapeRouter.cs:47,51-68` (`UiFocus.ModalOpen` 미참조) | **의심됨** |
| 낮음 | `LobbyController.GetPlayer`만 `m_Session` 널 검사가 없다(같은 클래스의 다른 조회 8개는 전부 있다) | `LobbyController.cs:81` | **확인됨** |
| 낮음 | `MatchSeries.ReturnToRoom`은 성공 시 `m_Handling`을 true로 남긴다 — 씬이 넘어가니 의도지만, `LoadScene`이 `Started`를 주고도 실제 전환이 실패하면 복구 경로가 없다 | `MatchSeries.cs:348-366` | 확인됨(설계) |
| 낮음 | `MatchStatsRecorder.m_Hooked`가 `Health`를 판 내내 붙들고, `BeginRecord`에서만 해제한다. 판이 안 끝나고 씬이 바뀌면 구독이 남는다 | `MatchStatsRecorder.cs:181,207-211` | **의심됨** |
| 낮음 | `LobbyBrowser.Refresh()`가 에디터에서 **`m_Busy`/`m_Raw`를 갱신하지 않고** 즉시 반환 — 에디터의 `Join` 인덱스 검증 경로가 죽어 있다 | `LobbyBrowser.cs:289-293` | **확인됨**(에디터 한정) |
| 낮음 | `MatchAutoStart`가 봇 스폰 전에 **고정 1초**를 기다린다. `LobbyController.SpawnBotsWhenReady`는 NavMesh 폴리곤을 폴링하는데 이쪽만 상수다 | `MatchAutoStart.cs:191` vs `LobbyController.cs:177-192` | **확인됨**(비일관) |

**죽은 코드·모순**
- `PhaseDirector.ServerReset()` — 호출자 0 (위 표 참조).
- `RoomLogic.CanStart`는 살아 있다(`RoomBotLogic`을 통해서만 불린다) — 죽은 게 아니라 **한 단계 감싸인 것**이다. `RoomDirector`의 XML 주석이 `RoomLogic.CanStart`를 가리키는데 실제 판정은 `RoomBotLogic.CanStartWithBots`다(문서 어긋남).
- `MatchFlowLogic.MATCH_DURATION_SECONDS`는 CSV를 매번 읽는 프로퍼티다 — `MatchManager.Update`에서 판 시작 1회만 읽히므로 문제는 없다.
- `MatchSeries.CurrentCat()`이 `m_Lobby` 널 검사를 안 하지만 `StartNextRound`가 앞서 검사하므로 도달 불가.

---

## 5. 제안 테스트 케이스

우선순위 P1 = 위 「높음」 공백을 직접 겨눈다.

| ID | 무엇을 검증 | 종류 | 훅 | 우선 |
|---|---|:--:|---|:--:|
| **A-01** | **2단계로 끝난 판 뒤 단계가 초기화되는가.** `qa_v43`처럼 고양이를 죽여 `Phase=Gang` → `TestBurnPhaseClock`으로 판을 끝냄 → `TestRestartMatch` → `TestStartGame` → **`TestGetPhase()=="Hunt"` 이고 시작 5초 뒤 `TestGetMatchResult()=="None"`** 이어야 한다. 지금은 `Gang`이 남아 즉시 종료될 것으로 본다 | wesqa | **있음**(`TestGetPhase`·`TestBurnPhaseClock`·`TestRestartMatch`·`TestStartGame`) | **P1** |
| **A-02** | **시리즈 2판에 봇이 남는가.** 대기실에서 봇 3으로 시작(`TestSetRoomBots(3)`+`TestRoomForceStart`) → 1판을 `TestExpireTimer`로 끝냄 → 5초 대기(시리즈 자동 진행) → **`TestGetBotMouseCount()`가 1판과 같아야 한다.** 지금은 0으로 떨어질 것으로 본다 | wesqa | **있음**(`TestSetRoomBots`·`TestRoomForceStart`·`TestGetBotMouseCount`·`TestGetBotCount`) | **P1** |
| **A-03** | **시리즈 중 재시작 버튼이 안 뜬다.** 시리즈 판에서 판이 끝난 직후 결과 패널의 재시작 버튼이 **비활성**이어야 한다. 지금은 호스트에게 뜬다 | wesqa | **없음** — `TestIsRestartButtonVisible()` 신규 필요(`ResultHud`의 버튼 `activeInHierarchy`를 센다. Model이 아니라 **켜진 오브젝트**를 세는 규칙) | **P1** |
| **A-04** | **인원이 빠져 다음 판을 못 열 때 갇히지 않는가.** 3인 시리즈 1판을 끝내고 클론 둘을 끊은 뒤, 5초 후 호스트 화면에 **무언가 조작 가능한 것**(로비 패널 또는 결과 패널)이 떠 있어야 한다 | wesqa | **없음** — `TestGetVisiblePanels()` 신규 필요(`LobbyPanel`/`ResultPanel`의 `activeInHierarchy`) | **P1** |
| **A-05** | **판 도중 난입자가 다음 판 고양이로 뽑히지 않는다.** 판 시작 뒤 클론 하나를 새로 붙이고(`TestStartClient`) → 라운드 종료 → 2판 역할을 전원 조회. **몸이 없는 clientId가 `Cat`을 받으면 실패** | wesqa | 부분 — `TestGetRole`·`TestStartClient` 있음. **`TestHasBody(clientId)` 신규 필요** | **P1** |
| **A-06** | `ShouldArm`이 false인 판에서 타이머가 만료돼도 판이 안 끝난다 — 이때 **무엇이 일어나야 하는가**를 먼저 정한다. 후보 ①「미장전 만료 = 무효 판, 로비 복귀」 ②「미장전이면 타이머를 안 돌린다」. 정한 뒤 `MatchFlowLogic`에 순수 함수로 박고 전수 검증 | EditMode(+wesqa 1건) | 순수 함수라 훅 불필요. 지금 `MatchFlowLogic`에 이 케이스를 표현할 함수가 **없다** — 신설 대상 | **P1** |
| **A-07** | 판 3개 연속(`qa_v50` 확장): 매 라운드 끝에 **`TestGetPhase()=="Hunt"` · `TestGetMatchSeed()!=0` · 봇 수 유지 · 역할 유지**를 한꺼번에 단언 | wesqa | 있음(A-02의 봇 훅 포함) | P2 |
| **A-08** | **결과·타이머·단계가 클라이언트에도 복제된다.** 판을 끝낸 뒤 **클론 인스턴스에서** `TestGetMatchResult`/`TestGetTimerText`/`TestGetPhase`를 읽어 호스트와 같은지 본다. 지금은 호스트만 읽는다 | wesqa | 있음(훅은 로컬 조회라 클론에서도 돈다) — **읽는 인스턴스만 바꾸면 된다** | P2 |
| **A-09** | 시리즈 점수판이 클론에도 같은 값으로 뜬다(`m_Round`/`m_CatWins`/`m_MouseWins` 복제) | wesqa | **없음** — `TestGetSeriesLabel()` 신규 필요(`MatchSeriesView`의 실제 텍스트를 읽는다) | P2 |
| **A-10** | `GangWin`이 시리즈 점수에서 어느 쪽인가 — 규칙을 정하고 `MatchSeriesLogic`에 `SideOf(MatchResult)` 순수 함수로 뽑아 3종 전수 검증 | EditMode | 불필요(순수 함수 신설) | P2 |
| **A-11** | 관전자(역할 `None`)가 있는 판: 몸이 안 생기고 · `CountAlive`에 안 세어지고 · 따봉 수신자 그룹에는 들어간다 | wesqa | 부분 — `TestGetMouseCount`·`TestCheer`·`TestGetCheerMarkerCount` 있음. `TestHasBody` 필요(A-05와 공유) | P2 |
| **A-12** | 따봉 3초 쿨다운과 고양이 제외: 관전 쥐가 `TestCheer`를 0.5초 간격 3번 → 마커가 **1개만** 늘고, 고양이 인스턴스에서는 `TestGetCheerMarkerCount()==0` | wesqa | 있음 | P2 |
| **A-13** | 판 도중 접속 끊김: `LobbySession`이 목록에서 빼고 · `CountAlive`가 줄고 · 마지막 고양이가 끊기면 **쥐 승으로 판이 끝나는가**(끝나야 하는가부터 정한다) | wesqa | **없음** — `TestDisconnectClient()` 또는 클론 프로세스를 죽이는 절차 필요 | P2 |
| **A-14** | `MatchRandom`이 스폰 전에 굴려졌을 때 `m_PendingSeed`가 `OnNetworkSpawn`에서 흘러 나온다(시드가 0이 아니다) | wesqa | 있음(`TestGetMatchSeed`) — 다만 **스폰 전 호출을 만드는 경로**가 필요 | P3 |
| **A-15** | `MatchAutoStart` 조인 타임아웃: 기대 인원보다 적게 붙은 채 10초가 지나면 **있는 사람만으로 판이 열린다** | wesqa | 있음(`TestGetRole`·`TestGetMatchRemaining`) | P3 |
| **A-16** | 대기실 자리 안정성: 3명 중 가운데가 나가고 새 사람이 들어와도 **두 몸이 같은 좌표에 안 선다** | wesqa | **없음** — `TestGetRoomBodyPositions()` 신규 필요 | P3 |
| **A-17** | `HostPortFallback`: 7788을 미리 점유한 상태에서 호스트가 7789로 올라가고, 공유 파일에 그 번호가 적히고, 클라가 그 번호로 붙는다 | EditMode(파일 I/O) + wesqa | 파일 경로가 `HostPortFallback.PortFilePath`로 공개돼 있어 EditMode에서 읽기 가능 | P3 |
| **A-18** | 옵션 창이 열린 채 Enter를 눌러도 채팅이 안 열린다 | wesqa | 있음(`TestOpenOptions`·`TestGetChatOpen`) — **입력 주입만 필요**(게임 뷰 포커스 전제, 메모리 「커서 QA 사각지대」 참조) | P3 |
| **A-19** | `MatchStatsRecorder`가 판마다 정확히 파일 1개를 쓰고, `winner`/`timeUp`이 실제 결과와 일치한다 | wesqa | **없음** — `TestGetLastMatchStatsPath()` 신규 필요 | P3 |
| **A-20** | 고양이 정원 경계: 3인이 전부 고양이를 눌렀을 때 **정확히 2명만** 고양이가 되고 시작이 막힌다(쥐 0) | wesqa | 있음(`TestSelectRoleCat`·`TestGetRole`·`TestGetCatCapacity`) — `qa_v44`가 2명까지만 확인, **3번째 거절은 미확인** | P3 |

### 신규 훅 요약 (필요한 것만)

| 훅 | 왜 | 어느 케이스 |
|---|---|---|
| `TestIsRestartButtonVisible()` | 켜진 오브젝트를 센다 — Model만 보면 배선 누락을 못 잡는다 | A-03 |
| `TestGetVisiblePanels()` | 「갇힘」은 **아무 패널도 안 뜬 상태**로만 정의된다 | A-04 |
| `TestHasBody(clientId)` | 관전자·난입자 구분의 최소 단위 | A-05·A-11 |
| `TestGetSeriesLabel()` | 시리즈 점수의 **화면 글자** | A-09 |
| `TestGetRoomBodyPositions()` | 대기실 자리 겹침 | A-16 |
| `TestGetLastMatchStatsPath()` | 통계 기록 | A-19 |
| `TestDisconnectClient()` | 끊김 시나리오 | A-13 |

---

## 부록 — 이 도메인에서 CLAUDE.md 함정이 남아 있는 자리

| 함정 | 자리 | 상태 |
|---|---|:--:|
| 7 「같은 프레임에 껐다 켬」 | `MatchSeries.StartNextRound`(`UniTask.NextFrame`) | 지켜짐 |
| 7의 사촌 「끄는 걸 아예 안 함」 | `PhaseDirector` 단계 | **미해결** |
| 스폰 전 `NetworkVariable` 쓰기 | `MatchRandom`(pending) · `MatchAutoStart`(1프레임 양보) | 지켜짐 |
| 규칙 9 (`?.`/`??` 오용) | 도메인 A 전수 확인 — `UnityEngine.Object`에 쓴 곳 **0건** | 깨끗 |
| 「Model 값만 보는 QA」 | 도메인 A 복제 필드 14개 중 12개가 **호스트 단독 조회** | 공백(§3) |
