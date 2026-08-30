# CTC UI 이미지 스타일 가이드

> 2D·텍스처 자산을 AI로 생성할 때 **프롬프트 맨 앞에 붙이는 프리픽스**다.
> 세트 일관성을 위해 같은 세트는 같은 채팅에서 이어 생성한다(designer 에이전트 규칙).
> 근거: 인트로 씬(`Assets/Scenes/Intro.unity`)에서 실제로 굳힌 톤이다.

## 프리픽스 (그대로 복사해 앞에 붙인다)

```
Cute stylized low-poly 3D render, night scene. Warm amber spotlight pooling on a
dark floor, everything outside the light falls to near-black. Palette: deep navy
blacks (#0a0f16, #141b27), warm cheese yellow accent (#ffd84d, #e8b535), muted
teal props. Soft shadows, gentle ambient occlusion, no harsh specular. Simple
rounded shapes, low polygon count, hand-painted feel. Cozy but slightly ominous.
No text, no watermark, no UI elements, no humans.
```

## 지켜야 할 것

| | |
|---|---|
| 광원 | **하나의 따뜻한 스포트라이트.** 그 밖은 거의 검다 — 게임의 핵심 대비다 |
| 강조색 | 치즈 노랑 `#ffd84d` 하나만. 다른 채도 높은 색을 끌어들이지 않는다 |
| 형태 | 저폴리·둥근 단순 형태. 사실적 질감 금지 |
| 인물 | 넣지 않는다. 캐릭터는 3D 모델(Quirky Series)이 담당한다 |
| 글자 | **절대 넣지 않는다.** UI 텍스트가 그 위에 올라간다 |

## UI 배경으로 쓸 때 추가 조건

- **중앙과 좌측이 비어 있어야 한다** — 방 목록과 메뉴가 그 위에 온다.
- 대비가 세면 글자가 안 읽힌다. 완성본에 **어두운 오버레이(알파 0.55 이상)** 를 덮는 것을 전제로,
  원본은 조금 밝게 뽑아도 된다.
- 해상도 16:9, 최소 1920×1080.

## 저장 위치

CTC에는 `GameResource` 폴더가 없다. 용도별로 아래에 둔다.

| 용도 | 경로 |
|---|---|
| UI 배경 | `Assets/Materials/UI/` |
| 아이콘 | `Assets/Materials/UI/Icons/` |
| 이펙트 텍스처 | `Assets/Materials/Intro/` (인트로 전용) |

import 후 반드시 Sprite(single)로 정규화한다 — Unity 기본 import는 Default/Sprite를 비결정적으로 정한다.
