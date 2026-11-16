param([switch]$Background,[switch]$Verify)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskExecutable = Join-Path $taskRoot 'unity\AutoSceneVR\Builds\Reconstruction\RoomReconstruction.exe'
if (-not (Test-Path -LiteralPath $taskExecutable)) { throw 'Import a reconstruction and build the viewer first; see docs/reconstruction-workflow.md.' }
$taskRun = Join-Path $taskRoot ('outputs\reconstruction-player-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
New-Item -ItemType Directory -Path $taskRun | Out-Null
$taskArguments = @('-logFile',('"{0}"' -f (Join-Path $taskRun 'player.log')))
if ($Verify) { $taskArguments += @('--verify-reconstruction',('"{0}"' -f $taskRun)) }
$taskStyle = if ($Background -or $Verify) { 'Hidden' } else { 'Normal' }
$taskPlayer = Start-Process -FilePath $taskExecutable -ArgumentList $taskArguments -WindowStyle $taskStyle -PassThru
Write-Output "Run: $taskRun"
if ($Verify) {
    if (-not $taskPlayer.WaitForExit(60000)) { throw 'Verification is still running; inspect this run without stopping other jobs.' }
    $taskResult = Get-Content -Raw -LiteralPath (Join-Path $taskRun 'runtime-validation.json') | ConvertFrom-Json
    $taskResult | ConvertTo-Json -Depth 5
    if (-not $taskResult.passed) { exit 1 }
}
