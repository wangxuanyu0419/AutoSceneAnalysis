param(
    [switch]$Background,
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskExecutable = Join-Path $taskRoot 'unity\AutoSceneVR\Builds\Windows\RoomIntentionLab.exe'
if (-not (Test-Path -LiteralPath $taskExecutable)) {
    throw 'Build the Windows demo from the Unity AutoScene menu first.'
}
$taskSession = Join-Path $taskRoot ('outputs\unity-session-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
New-Item -ItemType Directory -Path $taskSession | Out-Null
$taskArguments = @('--session-dir', ('"{0}"' -f $taskSession), '-logFile', ('"{0}"' -f (Join-Path $taskSession 'player.log')))
if ($Verify) {
    $taskArguments += @('--smoke-test', ('"{0}"' -f (Join-Path $taskSession 'validation')), '--quit-after-test', 'yes')
}
$taskWindowStyle = if ($Background -or $Verify) { 'Hidden' } else { 'Normal' }
$taskPlayer = Start-Process -FilePath $taskExecutable -ArgumentList $taskArguments -WindowStyle $taskWindowStyle -PassThru
Write-Output "Session: $taskSession"
Write-Output "Player PID: $($taskPlayer.Id)"
if ($Verify) {
    if (-not $taskPlayer.WaitForExit(60000)) { throw 'Validation is still running; inspect this session without stopping other processes.' }
    $taskResultPath = Join-Path $taskSession 'validation\runtime-validation.json'
    if (-not (Test-Path -LiteralPath $taskResultPath)) { throw 'The player exited without a validation result; inspect player.log.' }
    $taskResult = Get-Content -Raw -LiteralPath $taskResultPath | ConvertFrom-Json
    $taskResult | ConvertTo-Json -Depth 6
    if (-not $taskResult.passed) { exit 1 }
}
