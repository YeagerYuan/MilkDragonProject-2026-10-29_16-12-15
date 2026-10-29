$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$engineExe = 'D:\UnrealEngine\UE_5.7\Engine\Binaries\Win64\UnrealEditor-Cmd.exe'
$projectFile = Join-Path $projectRoot 'ShelljackPrototype.uproject'
$resultFile = Join-Path $projectRoot 'Saved\SmokeTest.txt'
$started = Get-Date
& $engineExe $projectFile /Game/Prototype/Island -game -nullrhi -unattended -nosound -ShellSmokeTest "-abslog=$projectRoot\SmokeTest.log"
if ($LASTEXITCODE -ne 0) { throw "Engine exited with $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $resultFile)) { throw 'Smoke test did not produce a result.' }
if ((Get-Item -LiteralPath $resultFile).LastWriteTime -lt $started) { throw 'Smoke test result is stale.' }
if ((Get-Content -LiteralPath $resultFile -Raw).Trim() -ne 'PASS') { throw 'Smoke tests failed; see SmokeTest.log.' }
Select-String -LiteralPath (Join-Path $projectRoot 'SmokeTest.log') -Pattern 'SHELLTEST' | ForEach-Object { $_.Line }
Write-Output 'All Shelljack smoke tests passed.'
