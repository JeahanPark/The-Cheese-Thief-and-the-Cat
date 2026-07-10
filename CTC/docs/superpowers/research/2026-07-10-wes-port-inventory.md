# WES → CTC 이식 인벤토리 (2026-07-10 조사)

읽기 전용 조사 결과. 이식 실행 전 필독. WES: `C:/GitFork/WES_Project/WES`.

## 핵심 결론

**패키지 격차가 최대 리스크** — CTC에는 UniTask / UniRx(R3) / Newtonsoft / Addressables / UGS(Core·Auth·Relay)가 전부 없다. WES 코어 대부분이 이 중 1개 이상에 의존 → **⑤ 패키지가 모든 항목의 선행 조건**.

| 패키지 | WES | 비고 |
|---|---|---|
| UniTask | git URL (`Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`) | 버전 핀 없음 — `#커밋해시` 고정 권장 |
| UniRx | git URL (`neuecc/UniRx.git?path=Assets/Plugins/UniRx/Scripts`) | WES에 R3 없음(순수 UniRx). 사용자 요청은 R3 도입 — 이식 코드 R3 전환은 대규모(Subject/IObservable/AddTo 전면 수정) → **결정 필요** |
| Newtonsoft | `com.unity.nuget.newtonsoft-json` 3.2.1 | WesQA 필수 |
| Addressables | 2.7.6 | Resource/Popup/Audio 3개 매니저가 동시 의존 |
| UGS Core/Auth/Relay | 1.16.0/3.6.0/1.1.0 | GameNetworkManager Relay 경로만 사용 — 다이어트 이식 시 불필요 |
| NuGetForUnity | WES에 없음 | R3 코어 도입 시에만 필요 |

## 항목별 요약

| # | 항목 | 소스 | 규모 | 핵심 포인트 |
|---|---|---|---|---|
| ② | 코어 프레임워크 | `Assets/Scripts/Manager/` + `Managers.cs` | 중 | MonoSingleton/Singleton/IManager는 **Managers.cs 안에 정의**. `Managers.cs`가 InfoManager(④)를 배선 → ④ 정리 없이는 컴파일 실패(숨은 강결합) |
| ② | GameNetworkManager | 413줄 | 대 | Relay/Auth 강결합 — CTC 로컬직결·LobbyManager와 정면 충돌. `StartLocalHost/Client`만 취하는 다이어트 이식 권장 |
| ② | InputManager | 237줄, UniRx | 대(충돌) | CTC는 `Keyboard.current` 직접 폴링 — 두 모델 병존 결정 필요. QuickSlotRegistry 상수 의존 |
| ⑥ | GameDebug | 23줄, 무의존 | 소 | 그대로 복사. 거의 모든 파일이 참조하므로 최우선 |
| ⑥ | TestManager | 3140줄 | 대(대부분 버림) | 재사용 골격은 ~120줄(클래스 + SimulateKeyPress 저수준 입력 주입)뿐. 시나리오 40+개는 전부 WES 게임 의존 → 골격만 재생성 |
| ③ | Controller 골격 | `GameController`(46)+`NetworkGameController`(50) | 소 | 이 2개만 순수 골격. Intro/Lobby/InGame은 WES 게임 플로우 — CTC 로비 이미 완성이라 **이식 불필요/중복** |
| ① | WesQA C# 서버 | `Assets/WesQA/Runtime/` 9파일, Editor asmdef | 중 | 게임 무의존, Newtonsoft만 필요. `InvokeBridge`가 `TestManager` 타입명 하드코딩 → CTC QA 진입점(현 `LobbyManager.Dev*`) 명명 통일 필요. CTC `tools/wesqa/`(Python Poco)는 이 서버가 있어야 동작 |
| ④ | CSV 파이프라인 | `Assets/Scripts/Info/` + `Assets/CSVInfo/` 14종 | 중~대 | 로더 메커니즘(리플렉션+`Name.TYPE` 헤더)은 재사용 가치. 단 `File.ReadAllLines("Assets/...")` 직접 읽기 = 빌드에서 깨짐, Info 타입 14종 전부 WES 스키마 → CTC 스키마로 재작성 |

## 이식 순서 (컴파일 가능 단위)

1. ⑤ 패키지 (UniTask·Newtonsoft·Addressables 필수 / UniRx vs R3 결정 / UGS 보류)
2. ⑥ GameDebug (무의존)
3. ② Managers.cs 베이스 3형 (매니저 배선은 스텁)
4. ③ 골격 2개 (GameController·NetworkGameController)
5. ② 저의존 매니저: Resource → AudioKey+Audio → GameScene(씬명 교체) → Chat
6. ② InputManager (아키텍처 결정 후)
7. ① WesQA C# 서버 (+ Python 연결 확인)
8. ④ CSV 로더 + CTC 스키마 Info
9. ② Popup / ⑥ TestManager 시나리오 (사실상 신규 개발 — 후순위)

## 위험 Top 5

1. 패키지 5종 전무 (Addressables가 매니저 3개 동시 블로킹)
2. UniTask/UniRx git URL 버전 핀 없음 → 재현성
3. GameNetworkManager Relay 강결합 vs CTC 로컬직결 (다이어트 필수)
4. 입력 아키텍처 이원화 (UniRx push vs 직접 poll)
5. `Managers.cs`↔`InfoManager` 숨은 강결합 + CSV 로더 빌드 비호환

부수: WES 전 파일이 글로벌 네임스페이스 → `CTC.Runtime.*` 이관 시 타입명 충돌 주의. CTC 로비(LobbyManager 계열)는 이미 완성 — WES Lobby/Intro 이식 금지(중복).
