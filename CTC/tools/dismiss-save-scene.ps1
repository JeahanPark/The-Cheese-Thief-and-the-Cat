# Cancel Unity's native "Save Scene" file dialog from outside the editor.
#
# Why this exists (measured 2026-09-12):
#   The MCP plugin saves the scene in two places -
#     McpBridgeScene.cs:32      EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()
#     McpBridgeInstantiate.cs:77 EditorSceneManager.SaveScene(...)
#   When the open scene is an unsaved "Untitled" one it has no path, so Unity opens a native
#   Save-File dialog asking where to put it. That dialog is modal: it stops the editor update
#   loop, so McpBridge stops answering and every MCP call times out. From the outside it looks
#   exactly like a dead bridge.
#
#   Saving is the wrong answer - it would write a junk scene into Assets/. Cancel is right.
#
#   The real fix is to keep a real scene open (or not to call those MCP paths at all). This
#   script is the seatbelt for when something does it anyway.
#
# ASCII only on purpose: PowerShell 5.1 reads .ps1 as ANSI, so non-ASCII text in the file
# breaks parsing outright (CLAUDE.md trap 8).
#
# Usage:
#   dismiss-save-scene.ps1              # cancel once if the dialog is up right now
#   dismiss-save-scene.ps1 -Watch 600   # keep cancelling for that many seconds
param(
  [int]$Watch = 0
)
$ErrorActionPreference = 'Stop'

Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class SaveSceneDlg {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern IntPtr PostMessageW(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
}
'@

# Titles Unity uses for the file dialog that blocks the loop. "Save Scene" is the one the
# MCP auto-save paths produce; the others show up from menu-driven saves.
$TITLES = @('Save Scene', 'Save Scene As')

$WM_COMMAND = 0x0111
$IDCANCEL   = 2

function Find-Dialog {
  $hits = New-Object System.Collections.ArrayList
  $cb = [SaveSceneDlg+EnumProc]{
    param($h, $l)
    if ([SaveSceneDlg]::IsWindowVisible($h)) {
      $sb = New-Object System.Text.StringBuilder 512
      [void][SaveSceneDlg]::GetWindowTextW($h, $sb, 512)
      $title = $sb.ToString()
      if ($TITLES -contains $title) {
        $procId = 0
        [void][SaveSceneDlg]::GetWindowThreadProcessId($h, [ref]$procId)
        $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if ($p -and $p.ProcessName -eq 'Unity') {
          [void]$hits.Add([pscustomobject]@{ Handle = $h; Title = $title; Pid = $procId })
        }
      }
    }
    return $true
  }
  [void][SaveSceneDlg]::EnumWindows($cb, [IntPtr]::Zero)
  return $hits
}

function Cancel-Dialog($dlg) {
  [void][SaveSceneDlg]::PostMessageW($dlg.Handle, $WM_COMMAND, [IntPtr]$IDCANCEL, [IntPtr]::Zero)
  Write-Output ("CANCELLED '{0}' pid={1}" -f $dlg.Title, $dlg.Pid)
}

if ($Watch -le 0) {
  $found = Find-Dialog
  if ($found.Count -eq 0) { Write-Output 'NO-DIALOG'; exit 0 }
  foreach ($d in $found) { Cancel-Dialog $d }
  exit 0
}

Write-Output ("WATCHING {0}s" -f $Watch)
$deadline = (Get-Date).AddSeconds($Watch)
$count = 0
while ((Get-Date) -lt $deadline) {
  foreach ($d in (Find-Dialog)) { Cancel-Dialog $d; $count++ }
  Start-Sleep -Milliseconds 400
}
Write-Output ("DONE cancelled={0}" -f $count)
