# Unity 메인 에디터 창을 포커스한다 (백그라운드 자동 컴파일/리임포트 유도용).
# 사용: powershell -NoProfile -File tools/focus-unity.ps1 [-Restore]
#   -Restore: 포커스 후 1초 뒤 이전 포그라운드 창으로 복귀 (사용자 방해 최소화)
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

# 메인 에디터 = MainWindowTitle에 프로젝트명(CTC)이 있는 Unity 프로세스 (클론은 'Player N')
$unity = Get-Process Unity -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -match 'CTC.*Unity' } |
    Select-Object -First 1

if (-not $unity) {
    Write-Output "UNITY_NOT_FOUND"
    exit 1
}

# SetForegroundWindow는 호출자가 포그라운드가 아니면 조용히 실패한다(포그라운드 잠금).
# Alt 키 입력으로 잠금을 풀어야 실제로 포커스가 넘어간다 (2026-07-23 확인).
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.SendKeys]::SendWait('%')
[Win32]::ShowWindowAsync($unity.MainWindowHandle, 9) | Out-Null  # SW_RESTORE
[Win32]::SetForegroundWindow($unity.MainWindowHandle) | Out-Null
Write-Output "FOCUSED pid=$($unity.Id)"

if ($Restore) {
    Start-Sleep -Seconds 1
    [Win32]::SetForegroundWindow($prev) | Out-Null
    Write-Output "RESTORED"
}
