# Third-Party Notices

이 저장소에는 아래 서드파티 패키지·에셋이 포함되거나 참조됩니다. 각 항목은 원 저작자의 라이선스를 따르며, 이 프로젝트의 코드 라이선스는 해당 항목에 적용되지 않습니다.

## 코드 라이브러리 (오픈소스)

| 패키지 | 버전 | 라이선스 | 위치 |
|---|---|---|---|
| [UniTask](https://github.com/Cysharp/UniTask) | 2.5.11 | MIT | `Packages/manifest.json` (git) |
| [R3](https://github.com/Cysharp/R3) | 1.3.1 | MIT | `Packages/manifest.json` (git) · `Assets/Packages/R3.1.3.1` (NuGet) |
| [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) | — | MIT | `Packages/manifest.json` (git) |
| [In-game Debug Console](https://github.com/yasirkula/UnityIngameDebugConsole) | — | MIT | `Packages/manifest.json` (git) · 개발 빌드에서만 켜짐 |
| [Netcode Transport — Facepunch](https://github.com/Unity-Technologies/multiplayer-community-contributions) | — | MIT | `Packages/com.community.netcode.transport.facepunch` (로컬 패치 포함) |
| [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks) | — | MIT | 위 트랜스포트에 동봉 |
| Steamworks SDK 재배포 바이너리 (`steam_api64.dll` 등) | — | Valve Steamworks SDK 라이선스 | 위 트랜스포트에 동봉 |
| Microsoft.Bcl.AsyncInterfaces · Microsoft.Bcl.TimeProvider · System.ComponentModel.Annotations · System.Runtime.CompilerServices.Unsafe · System.Threading.Channels | 각 폴더명 참조 | MIT | `Assets/Packages/*` (각 폴더에 `LICENSE.TXT`) |
| Unity 공식 패키지 (URP · Netcode for GameObjects · Cinemachine · Input System · Addressables · Multiplayer Play Mode · Recorder · TextMesh Pro 등) | `Packages/manifest.json` | Unity Companion License / Unity Package Distribution License | 패키지 매니저 |
| MCP_Unity_Plugin | — | 직접 작성 (별도 저장소) | `Assets/MCP_Unity_Plugin` |

## 폰트

| 폰트 | 라이선스 | 위치 |
|---|---|---|
| [Pretendard](https://github.com/orioncactus/pretendard) | SIL Open Font License 1.1 | `Assets/Fonts/Pretendard-Regular.otf` |

## 아트 에셋 (Unity Asset Store)

아래 에셋은 **Unity Asset Store EULA**에 따라 라이선스된 것으로, 이 저장소는 포트폴리오 열람 목적으로만 공개됩니다. 해당 에셋을 이 저장소에서 추출하여 재사용·재배포하는 것은 허용되지 않습니다. 필요하다면 Asset Store에서 직접 구매·다운로드해 주세요.

| 에셋 | 제작사 | 위치 |
|---|---|---|
| Quirky Series — Ultimate Animals Pack | Omabuarts Studio | `Assets/Quirky Series` |
| Poly Universal Pack | polyperfect | `Assets/polyperfect` (선별 임포트) |
| Gridbox Prototype Materials | Ciathyza | `Assets/Thirdparty/Ciathyza` |

## 사운드

`Assets/Audio` 아래 배경음·효과음은 [ElevenLabs](https://elevenlabs.io)(Creator 플랜, 상업적 이용 허용)로 생성한 뒤 직접 후처리한 것입니다.

## 개발 도구

- QA 자동화 `tools/wesqa`는 [Poco](https://github.com/AirtestProject/Poco) · [Airtest/aircv](https://github.com/AirtestProject/Airtest)(Apache-2.0)를 기반으로 직접 작성했습니다.
- 애니메이션 클립 제작 스크립트 `tools/blender`는 [Blender](https://www.blender.org)(GPL) Python API를 사용합니다. 스크립트 자체는 이 저장소의 라이선스를 따릅니다.
