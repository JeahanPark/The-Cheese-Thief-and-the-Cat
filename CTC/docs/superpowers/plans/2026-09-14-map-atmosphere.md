# 맵 분위기 패스 — 작업 계획

설계: [2026-09-14-map-atmosphere-design.md](../specs/2026-09-14-map-atmosphere-design.md)
상태: ☐ 할 일 · ◐ 진행 중 · ☑ 완료(검증까지) · ✕ 보류(사유)

## 작업

- ☑ `CTC/LightCone` 셰이더 — 가산 빛줄기(위→아래 감쇠 · 가장자리 소거 · 먼지 일렁임 · 안개)
- ☑ `MapAtmosphereProfile`(Resources) · `MapAtmosphere` · `LampFlicker`
- ☑ `MapDresser` 빛웅덩이마다 전등/달빛 기둥 + 첫 적용 때 하늘·Volume·카메라 후보정·달빛/환경광
- ☑ 에디터 `MapAtmosphereBuilder` — 메시·머티리얼·먼지·프리팹·밤 하늘·후보정 프로필 (`Tools/CTC/Map/Build Atmosphere`)
- ☑ 에디터 `MapAtmosphereTuner` — 플레이 중 `%TEMP%/ctc-atmo.json`으로 수치 조정 (`Tools/CTC/Dev/Map Atmosphere 수치 적용`)
- ☑ **버그 수정: `CTC_MapTriplanar`가 Forward+에서 점광원을 못 받았다** — 조건이 `_ADDITIONAL_LIGHTS`뿐이었다(Forward+에선 안 켜진다). 바닥·벽이 빛웅덩이를 원래부터 못 받고 있었다.
- ☑ 튜닝값을 생성기·프로필에 확정

## 검증

- ☑ EditMode 908 통과(로직 변경 없음 — 수 동일이 정상)
- ☑ `qa_v53` 12/12
- ☑ 스크린샷: 창고(대형) 전·후, 기본 맵 — `docs/design-vault/images/map-atmosphere-after.png`
- ☑ 프레임: 에디터 3인 기준 전 58~60 fps / 후 57~60 fps (참고치)

## 튜닝 기록 (창고 맵, 같은 자리)

| 회차 | 결과 | 바꾼 것 |
|---|---|---|
| 1 | 캄캄하고 화면 전체가 파랬다 | 스플릿 톤 그림자 남색이 너무 셌다 · ACES가 중간톤을 눌렀다 |
| 2 | 빛줄기·밤 하늘은 살았는데 바닥에 빛웅덩이가 없다 | 노출 1.1 · 스플릿 은은하게 · 전등 10 |
| 3 | 원인 발견 — 트라이플래너 셰이더의 Forward+ 조건 | 셰이더 수정 → 벽돌 바닥에 앰버 웅덩이 |
| 4 | 가독성 확보 | 노출 1.7 · 달빛 0.7 · 환경광 (0.15,0.17,0.23) · 전등 14 |

## 남은 것(다음 후보)

- 치즈 지점 원반이 아직 **Gridbox 단색 원판**이다(빨강·주황·노랑 기능색). 색 의미는 유지하고 바닥 링(가장자리 발광·옅은 채움)으로 바꾸면 화면에서 가장 크게 좋아진다.
- 창고 전등 하나가 계단 박스 위에 걸려 빛줄기가 박스를 관통한다 — 빛 자리(`WarehouseLayoutBuilder.LIGHTS`)는 스텔스 판정이라 사용자 확인 후.
- 사용자 눈 검수.
