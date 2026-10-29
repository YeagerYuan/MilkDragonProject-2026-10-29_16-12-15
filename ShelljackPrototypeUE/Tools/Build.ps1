$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$engineRoot = 'D:\UnrealEngine\UE_5.7'
$projectFile = Join-Path $projectRoot 'ShelljackPrototype.uproject'
& "$engineRoot\Engine\Build\BatchFiles\Build.bat" ShelljackPrototypeEditor Win64 Development "-Project=$projectFile" -WaitMutex -NoHotReloadFromIDE -2026 "-CompilerVersion=14.51.36251" "-Log=$projectRoot\Build.log"
exit $LASTEXITCODE
