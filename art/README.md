# art — 원본 작업 파일

여기 있는 것은 **유니티가 읽지 않는다.** `Assets/` 밖이다.

| | |
|---|---|
| `blender/*.blend` | 블렌더 작업 파일 |
| 산출물 | `CTC/Assets/Animations/Pets/*.fbx` (커밋한다) |

## 왜 `Assets/` 안에 두지 않는가

유니티는 `.blend`를 발견하면 **블렌더를 직접 실행해** FBX로 변환한다. 그 순간부터
**블렌더가 안 깔린 기계에서는 프로젝트가 열리지 않는다** — 빌드 머신·다른 사람 전부.
FBX를 커밋하면 그 의존이 생기지 않는다.

## 도구

**Blender 5.2.1 LTS** (2026-08-25 빌드). `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`

버전을 올릴 때는 **먼저 파이썬 API가 그대로인지 재라.** 인자 이름이 하나라도 빠지면
`export_scene.fbx`가 `TypeError`로 죽는다:

    blender -b --factory-startup --python-expr "import bpy; print(sorted(p.identifier for p in bpy.ops.export_scene.fbx.get_rna_type().properties))"

## 들이기 · 내보내기

    blender -b --factory-startup -P tools/blender/import_pack.py -- <in.fbx> art/blender/mouse.blend
    blender -b art/blender/mouse.blend -P tools/blender/export_clips.py -- CTC/Assets/Animations/Pets/CTC_Mouse_Clips.fbx

설정은 스크립트 안에 있다. **체크박스를 사람이 외우지 않는다** — `add_leaf_bones`(기본 켜짐)와
`bake_anim_simplify_factor`(기본 1.0)가 각각 없던 뼈를 만들고 키를 솎아내는데, 둘 다 에러가 안 난다.

내보낸 뒤에는 유니티에서 `Tools/CTC/Dev/Probe Clip Parity`로 잰다.

## `.blend`·FBX는 바이트로 재현되지 않는다

같은 입력으로 다시 돌려도 **파일 바이트는 달라진다**(타임스탬프·내부 id). 내용은 같다 —
2026-09-12에 처음부터 다시 돌려 `판정: 같다`를 확인했다.

그러니 **`git diff`로 「제대로 나왔나」를 판정하지 마라.** 판정하는 것은 자다. 다시 구웠는데
자가 초록이면 그 바이너리는 커밋하지 말고 `git checkout --`로 되돌려라 — 뜻 없는 변경이다.

설계: `CTC/docs/superpowers/specs/2026-09-12-blender-animation-pipeline-design.md`
계획: `CTC/docs/superpowers/plans/2026-09-12-blender-animation-pipeline.md`
