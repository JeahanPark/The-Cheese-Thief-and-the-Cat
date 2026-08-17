# 능력 바 — 스킬 쿨다운 UI 설계 (2026-08-17)

시안: https://claude.ai/code/artifact/b2064be0-fda8-4285-a484-d5a5640bda63

## 왜

- 지금 쿨다운은 좌하단 글자 칩(`Q 스킬 3`) — 초 숫자만 있고 게이지가 없다.
- 고양이 스킬 쿨다운은 **서버 전용 float**이라 클라 고양이에게는 항상 0(칩이 늘 밝다).
- 다른 게임(DbD·Identity V·OW·LoL)은 전부 하단 중앙/우하단 슬롯 + 시계방향 덮개 + 남은 초. 좌하단은 팀·채팅 자리.

## 결정

| 항목 | 내용 |
|---|---|
| 자리 | 상점 핫바 **아래 줄**, 하단 중앙 정렬(84px 정사각) — 핫바는 한 칸(84+12px) 위로 올린다. 핫바와 함께 게임 중에만 |
| 슬롯 | 쥐 `Space 대시`(구매 후) · `F 임명`(깡패만) / 고양이 `Q 스킬`(전직 후) / 관전자 없음 (기본 공격·살금은 슬롯 없음). **해금 안 된 칸은 안 보인다**(2026-08-17 사용자 결정 — 미보유 흐림 표시 폐기) |
| 모양 | **핫바 칸과 같은 생김새**(2026-08-17 사용자 요청) — 같은 132×84·같은 어두운 바탕·세 줄 `Q  불` / 반줄 힌트 / 상태. 키캡 칩·밝은 타일 폐기 |
| 상태 3종 | 준비(상태 줄 "준비" 초록) · 쿨다운(뿌연 덮개가 12시부터 시계방향으로 걷힘 — 바탕이 검어서 검은 덮개는 안 보였다 → 밝은 회갈색 0.72 · 걷히는 가장자리 선 · **최상단** 남은 초 큰 숫자) · 지속 중(상태 줄 "지속 n초" 금색 + 하단 잔여 바) |
| 준비 알림 | 쿨다운→준비 순간 슬롯이 0.25초 밝게 번쩍(소리 없음) |
| 좌하단 칩 | `ControlsHud` 삭제. 살금 안내는 첫 스폰 토스트에 남는다 |
| 키 변경 | 살금 **LCtrl → LShift**(쥐·고양이 공통) · 대시 **LShift → Space** |

### 칸이 생기는 조건

| 슬롯 | 보이는 조건 |
|---|---|
| Space 대시 | 쥐가 대시를 산 뒤 |
| F 임명 | 직업이 깡패(Gang)일 때 |
| Q 스킬 | 고양이가 전직한 뒤 — 이름은 전직명(불·바람·빛·어둠) |

해금된 칸이 0개면 바 자체를 안 그린다.

## 구조

```
CatSkill (NetworkVariable ReadyAt·ActiveUntil 서버 시각)  ┐
MouseDash (로컬 쿨다운·지속)                              ├─ AbilityBar(View) ── AbilitySlotView ×N
MouseJob (JobChanged) · CatClass (ClassChanged)          ┘        │
                                                          AbilityBarLogic (순수: 슬롯 목록·상태·비율·글자)
```

| 단위 | 역할 | 의존 |
|---|---|---|
| `Player/CatSkill` | `m_ReadyAt`·`m_ActiveUntil` **NetworkVariable\<double\>**(서버 시각) 추가. `CooldownRemaining`/`ActiveRemaining`은 `NetworkManager.ServerTime.Time`으로 계산 → 클라 고양이도 본다. 기존 `m_Cooldown`·`m_Remaining` 서버 float는 이 둘로 대체 | NGO |
| `Player/MouseDash` | 키 Space. `CooldownRemaining` 그대로 + `CooldownTotal`·`ActiveRemaining`(대시 중) 노출 | Balance |
| `Player/Sneak` | 키 LShift | — |
| `UI/AbilityBarLogic` (순수) | `Build(role, job, dashOwned, catClass) → AbilitySlot[]`(키·이름·상태·잠금 사유), `FillRatio(remaining,total)`, `SecondsText(remaining)`(올림 정수) | 없음 — EditMode 대상 |
| `UI/AbilityBar` (View) | 로컬 플레이어 바뀔 때 슬롯 구성 재구독(JobChanged·OwnedChanged·ClassChanged → 재구성). 쿨다운·지속이 하나라도 살아 있을 때만 `EveryUpdate`로 채움 갱신, 아니면 구독 해제. 준비 플래시 타이머 | R3, LocalPlayerBinder |
| `UI/AbilitySlotView` | 슬롯 1개의 그림(배경·방사형 덮개 Image Filled Radial360·키캡·이름·초·잔여 바·보조 글자) `Apply(slot, remaining, total, active…)`만 | UGUI |
| `Editor/Setup/LobbyCanvasBuilder.BuildAbilityBar` | 루트: 앵커 하단 중앙, 피벗 (0.5,0), y = 28 · 핫바 y = 28+84+12. 슬롯 최대 2칸을 가운데 정렬(빌더가 2개 만들고 View가 켜고 끄며 재정렬) | — |
| `ShopHotbar` | 문구: 첫 스폰 토스트 `LShift 살금살금`, 대시 힌트/설명 `Space` | — |
| `Testing/TestManager.Hud` | `TestGetControlsText` → `TestGetAbilityBar()`: "키:이름:상태:남은초" 를 슬롯마다 `|`로 이어 반환 | — |

## 데이터 흐름

1. `LocalPlayerBinder.LocalPlayer` → 역할·직업·대시 보유·전직 스트림 재구독 → `AbilityBarLogic.Build` → 슬롯 View 켜고 끄고 글자 세팅.
2. 서버가 스킬 사용 시 `m_ReadyAt = now + 쿨`, `m_ActiveUntil = now + 지속` 기록 → NetworkVariable 복제 → 소유자 View가 `ServerTime`으로 남은 초 계산.
3. View의 `EveryUpdate`(살아 있을 때만): 각 슬롯 `remaining/total` → 덮개 `fillAmount`, 초 글자(정수 바뀔 때만 세팅), 잔여 바. remaining이 0이 되는 프레임에 플래시 시작.

## 오류·경계

- 로컬 플레이어 없음/관전자 → 바 자체를 끈다.
- 전직 전 Q 슬롯은 "전직 필요"로 흐리게 — 클라가 `CatClassId.None` 을 보고 판단.
- 서버 시각은 클라에서 약간 앞뒤가 있다 → 남은 초는 `max(0, …)`, 덮개는 0~1 클램프.
- 컴파일 중 플레이 금지(NGO NRE 폭풍) — 구현 전 플레이 모드 종료.

## 검증

| 층 | 무엇 |
|---|---|
| EditMode | `AbilityBarLogicTests`: 역할·직업·보유·전직별 슬롯 목록 / 상태 판정 / `FillRatio` 클램프 / `SecondsText` 올림. `ControlsHudTests` 삭제 |
| wesqa | qa_v45 §조작 안내 → 능력 바로 교체: 쥐 슬롯에 `대시`(미보유 표시), 깡패 되면 `임명` 슬롯 등장, 클라 고양이가 스킬 쓴 뒤 `TestGetAbilityBar`에 남은 초 > 0 (**복제 검증**) |
| 눈 | 스크린샷 — 핫바 왼쪽 정렬·겹침 없음(쥐·고양이) |

## 문서

- `규칙과-현황.html` 키 표(LShift 살금·Space 대시) · 대시 행 · 잠행 문단
- `코드 다이어그램.md`: ControlsHud → AbilityBar/AbilitySlotView/AbilityBarLogic
- `.claude/handoff-active.md`
