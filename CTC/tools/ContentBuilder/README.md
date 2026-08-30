# ContentBuilder — 스팀 업로드

## 처음 한 번

1. **Steamworks SDK**를 받아 `sdk/tools/ContentBuilder/builder/` 내용을 `builder/`에 넣는다
   (`steamcmd.exe` 등). SDK는 레포에 커밋하지 않는다 — `.gitignore`에 있다.
2. `steam-app.config`의 `APP_ID`·`DEPOT_ID`를 발급받은 값으로 고친다.
   **AppID는 여기 한 곳에만 적는다.** 흩어지면 하나를 빠뜨린다.
3. 빌드 계정에 권한 2개가 있어야 한다 — **Edit App Metadata** + **Publish App Changes To Steam**.
   출시된 앱은 전화번호·모바일 앱 연결 후 **3일 대기**해야 빌드를 라이브로 올릴 수 있다.

## 올리기

```powershell
# 1) 배포 빌드
#    Unity 메뉴: Tools/CTC/Build Windows (배포용)

# 2) 드라이런 — 매니페스트만 만들고 업로드는 안 한다
.\Upload-Build.ps1 -Account <스팀ID>

# 3) 실제 업로드
.\Upload-Build.ps1 -Account <스팀ID> -Live

# 4) 베타 브랜치에 바로 올리기
.\Upload-Build.ps1 -Account <스팀ID> -Live -Branch beta
```

**첫 로그인은 대화형으로** 해야 한다 — Steam Guard 코드를 넣으면
`<Steam>\config\config.vdf`에 토큰이 남고, 그 뒤로는 비번 없이 붙는다.
그 파일을 지우면 자동화가 깨진다.

`default`(라이브) 브랜치는 **자동으로 못 올린다.** App Admin > Builds에서 손으로 올린다.

## 폴더

| 폴더 | 무엇 | 커밋 |
|---|---|---|
| `builder/` | steamcmd (SDK에서 복사) | ✗ |
| `content/` | 업로드할 파일 (스크립트가 채운다) | ✗ |
| `output/` | 로그 + **청크 캐시**(`.csm`/`.csd`) | ✗ — 다만 **지우지 마라.** 남아 있어야 다음 업로드가 빠르다 |
| `scripts/` | vdf 템플릿과 생성본 | 템플릿만 |

## 뎁포에서 빼는 것 (vdf의 FileExclusion)

| 대상 | 왜 |
|---|---|
| `*_BurstDebugInformation_DoNotShip*` | 이름 그대로 배포용이 아니다 |
| `*_BackUpThisFolder_...` | IL2CPP 심볼 백업 |
| **`steam_appid.txt`** | 남으면 `RestartAppIfNecessary`가 **언제나 false**를 돌려줘 "스팀 거쳐 재실행"이 죽는다. exe 직접 실행이 그대로 통과해 오버레이·초대·플레이시간이 안 붙는다 |
| `*.pdb` | 개발 빌드 심볼 |

## 패치 크기를 망치는 것 (밸브 문서)

미리 압축·암호화한 팩 파일 · 에셋 메타데이터의 빌드 타임스탬프 · 빌드마다 뒤바뀌는 에셋 순서.
셋 다 청크 재사용을 깨서 **작은 패치가 전체 재다운로드**가 된다.
