# 스팀 업로드 (SteamPipe) — 배포 빌드를 content/로 옮기고 steamcmd로 올린다.
#
# 사용:
#   .\Upload-Build.ps1 -Account <스팀ID>              # 드라이런(Preview) — 업로드 안 한다
#   .\Upload-Build.ps1 -Account <스팀ID> -Live        # 실제 업로드
#   .\Upload-Build.ps1 -Account <스팀ID> -Live -Branch beta
#
# 처음 한 번은 **대화형으로** 로그인해 Steam Guard 코드를 넣어야 한다.
# 그러면 <Steam>\config\config.vdf에 토큰이 남아 다음부터는 비번 없이 붙는다.
# 그 config.vdf를 지우면 CI가 깨진다 — 자동화가 깨지는 1순위 원인이다.
param(
    [Parameter(Mandatory = $true)][string]$Account,
    [switch]$Live,
    [string]$Branch = "",
    [string]$BuildDir = "..\..\Builds\Windows"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# ── 설정 읽기 ────────────────────────────────────────────────
$configPath = Join-Path $root "steam-app.config"
if (-not (Test-Path $configPath)) { throw "설정이 없다: $configPath" }

$config = @{}
foreach ($line in Get-Content $configPath) {
    if ($line -match '^\s*#') { continue }
    if ($line -notmatch '=') { continue }
    $pair = $line -split '=', 2
    $config[$pair[0].Trim()] = $pair[1].Trim()
}

$appId = $config['APP_ID']
$depotId = $config['DEPOT_ID']

if ($appId -eq '480') {
    # 480은 Spacewar(밸브 공용 테스트 앱)다. 여기에 올리면 남의 앱에 올리는 것이고 실패한다.
    throw "AppID가 아직 480(테스트용)이다. steam-app.config에 발급받은 AppID를 넣어라."
}

# ── 빌드 산출물을 content/로 ─────────────────────────────────
$source = Resolve-Path (Join-Path $root $BuildDir) -ErrorAction SilentlyContinue
if (-not $source) { throw "배포 빌드가 없다: $BuildDir — Tools/CTC/Build Windows (배포용)을 먼저 돌려라" }

$exe = Join-Path $source "CanadianMouse.exe"
if (-not (Test-Path $exe)) { throw "CanadianMouse.exe가 없다: $exe" }

$content = Join-Path $root "content"
# 폴더 셋은 .gitignore로 빠지므로 클론 직후엔 없다 — 매번 확인해 만든다.
foreach ($dir in @("content", "output", "builder")) {
    $path = Join-Path $root $dir
    if (-not (Test-Path $path)) { New-Item -ItemType Directory $path | Out-Null }
}

Write-Output "[Upload] content/ 비우는 중"
Get-ChildItem $content -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

Write-Output "[Upload] $source -> content/"
Copy-Item (Join-Path $source '*') $content -Recurse -Force

# ── vdf 생성 ────────────────────────────────────────────────
# output/은 지우지 않는다. 청크 캐시(.csm/.csd)가 남아 있어야 다음 업로드가 빠르다.
$preview = if ($Live) { "0" } else { "1" }
$template = Get-Content (Join-Path $root "scripts\app_build.vdf.template") -Raw

$vdf = $template.
    Replace('__APP_ID__', $appId).
    Replace('__DEPOT_ID__', $depotId).
    Replace('__DESCRIPTION__', $config['DESCRIPTION']).
    Replace('__PREVIEW__', $preview)

if ($Branch -ne "") {
    if ($Branch -match '\s') { throw "브랜치 이름에 공백을 쓸 수 없다: '$Branch'" }
    if ($Branch -eq 'default') { throw "default(라이브)는 자동으로 못 올린다. App Admin > Builds에서 손으로 올려라." }
    $vdf = $vdf.Replace('"SetLive" ""', "`"SetLive`" `"$Branch`"")
}

$vdfPath = Join-Path $root "scripts\app_build_$appId.vdf"
Set-Content -Path $vdfPath -Value $vdf -Encoding utf8
Write-Output "[Upload] vdf 생성: $vdfPath (Preview=$preview)"

# ── steamcmd ────────────────────────────────────────────────
$steamcmd = Join-Path $root "builder\steamcmd.exe"
if (-not (Test-Path $steamcmd)) {
    throw "steamcmd.exe가 없다: $steamcmd`n  Steamworks SDK의 tools/ContentBuilder/builder/ 내용을 여기 넣어라."
}

if (-not $Live) { Write-Output "[Upload] 드라이런이다 — 실제 업로드는 -Live" }

& $steamcmd +login $Account +run_app_build "..\scripts\app_build_$appId.vdf" +quit
if ($LASTEXITCODE -ne 0) { throw "steamcmd 실패 (exit $LASTEXITCODE) — output\ 로그를 봐라" }

Write-Output "[Upload] 완료. App Admin > Builds에서 브랜치에 올려라."
