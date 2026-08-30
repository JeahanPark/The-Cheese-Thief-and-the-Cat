# Unity 메인 에디터 창을 포커스한다 (백그라운드 자동 컴파일/리임포트 유도용).
# 사용: powershell -NoProfile -ExecutionPolicy Bypass -File tools/focus-unity.ps1 [-Restore]
#   -Restore: 포커스 후 1초 뒤 이전 포그라운드 창으로 복귀 (사용자 방해 최소화)
#
# 2026-08-30 개정 — 두 가지가 실제로 물렸다:
#   1. 제목 필터가 'CTC.*Unity'였다. **컴파일·도메인 리로드 중에는 창 제목이 비거나 바뀐다.**
#      하필 포커스가 가장 필요한 그 순간에 UNITY_NOT_FOUND로 빠졌고, 그걸 기다리던
#      sentinel 루프가 통째로 타임아웃했다(120초 × 2).
#   2. SetForegroundWindow는 포그라운드 잠금에 걸려 조용히 실패한다. Alt를 눌러 푸는 우회를
#      쓰고 있었지만 그래도 안 붙는 경우가 있었다. WScript.Shell.AppActivate가 확실히 붙는다.
param(
    [switch]$Restore
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
}
"@

$prev = [Win32]::GetForegroundWindow()

# 창 제목이 있는 Unity 프로세스만 후보(제목 없는 것은 보조 프로세스).
# MPPM 클론은 제목에 'Player N'이 붙으므로 제외한다.
$candidates = Get-Process Unity -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -ne '' -and $_.MainWindowTitle -notmatch 'Player \d' }

# 프로젝트명이 제목에 살아 있으면 그걸 우선. 리로드 중이라 제목이 비면 남은 것 중 하나를 쓴다.
$unity = $candidates | Where-Object { $_.MainWindowTitle -match 'CTC' } | Select-Object -First 1
if (-not $unity) { $unity = $candidates | Select-Object -First 1 }

if (-not $unity) {
    Write-Output "UNITY_NOT_FOUND"
    exit 1
}

# AppActivate가 포그라운드 잠금을 가장 잘 넘는다. 실패하면 Win32로 한 번 더 시도한다.
$activated = $false
try {
    $ws = New-Object -ComObject WScript.Shell
    $activated = $ws.AppActivate($unity.Id)
} catch {
    $activated = $false
}

if (-not $activated) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait('%')
    [Win32]::ShowWindowAsync($unity.MainWindowHandle, 9) | Out-Null  # SW_RESTORE
    $activated = [Win32]::SetForegroundWindow($unity.MainWindowHandle)
}

Write-Output "FOCUSED pid=$($unity.Id) activated=$activated title='$($unity.MainWindowTitle)'"

if ($Restore) {
    Start-Sleep -Seconds 1
    [Win32]::SetForegroundWindow($prev) | Out-Null
    Write-Output "RESTORED"
}
