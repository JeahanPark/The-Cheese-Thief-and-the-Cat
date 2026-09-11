# Dismiss the MPPM "Player N unexpectedly stopped. Do you want to restart it?" dialog.
#
# Why this exists:
#   That modal blocks Unity's editor update loop, so McpBridge stops answering and every
#   MCP call times out. It looks exactly like "the clone stole the named pipe", but the
#   real cause is a modal nobody can see from the outside.
#
#   It appears when a virtual player's PROCESS dies without MPPM being told - i.e. when
#   someone kills "Player N" from Task Manager or Stop-Process. The correct way to shut a
#   clone down is the editor menu (Tools/CTC/Dev/MPPM 1인 ...), which deactivates it through
#   the MPPM API and raises no dialog. Use this script only when the modal is already up.
#
# ASCII only on purpose: PowerShell 5.1 reads .ps1 as ANSI, so non-ASCII text in the file
# breaks parsing outright (CLAUDE.md trap 8).
#
# Usage:
#   dismiss-mppm-modal.ps1              # click "No"  (do not restart the clone)
#   dismiss-mppm-modal.ps1 -Restart     # click "Yes" (restart the clone)
#   dismiss-mppm-modal.ps1 -Watch 120   # keep watching for that many seconds
param(
  [switch]$Restart,
  [int]$Watch = 0
)
$ErrorActionPreference = 'Stop'

Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class MppmDlg {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h, EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
  public const uint BM_CLICK = 0x00F5;
  public static string Text(IntPtr h) {
    var sb = new StringBuilder(512); GetWindowTextW(h, sb, 512); return sb.ToString();
  }
  public static string Cls(IntPtr h) {
    var sb = new StringBuilder(256); GetClassNameW(h, sb, 256); return sb.ToString();
  }
}
'@

function Find-Dialogs {
  $found = New-Object System.Collections.ArrayList
  $cb = [MppmDlg+EnumProc]{
    param($h, $l)
    if ([MppmDlg]::IsWindowVisible($h)) {
      $t = [MppmDlg]::Text($h)
      # Unity localises the body but keeps this English title.
      if ($t -match 'unexpectedly stopped') { [void]$found.Add([pscustomobject]@{ H = $h; T = $t }) }
    }
    return $true
  }
  [void][MppmDlg]::EnumWindows($cb, [IntPtr]::Zero)
  return $found
}

function Click-Button($dlg, $want) {
  $hit = [IntPtr]::Zero
  $cb = [MppmDlg+EnumProc]{
    param($h, $l)
    if ([MppmDlg]::Cls($h) -eq 'Button') {
      $label = ([MppmDlg]::Text($h) -replace '&', '')
      if ($label -eq $want) { $script:hit = $h; return $false }
    }
    return $true
  }
  $script:hit = [IntPtr]::Zero
  [void][MppmDlg]::EnumChildWindows($dlg, $cb, [IntPtr]::Zero)
  if ($script:hit -ne [IntPtr]::Zero) {
    [void][MppmDlg]::SendMessageW($script:hit, [MppmDlg]::BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero)
    return $true
  }
  return $false
}

$want = if ($Restart) { 'Yes' } else { 'No' }
$deadline = (Get-Date).AddSeconds([Math]::Max($Watch, 0))
$closed = 0

do {
  foreach ($d in Find-Dialogs) {
    if (Click-Button $d.H $want) {
      $closed++
      Write-Output ("CLOSED '" + $d.T + "' with " + $want)
    } else {
      Write-Output ("FOUND '" + $d.T + "' but no '" + $want + "' button - close it by hand")
    }
  }
  if ($Watch -gt 0) { Start-Sleep -Milliseconds 700 }
} while ((Get-Date) -lt $deadline)

if ($closed -eq 0) { Write-Output 'NONE: no MPPM restart dialog is open' }
