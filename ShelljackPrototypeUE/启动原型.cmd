@echo off
cd /d "%~dp0"
start "" "D:\UnrealEngine\UE_5.7\Engine\Binaries\Win64\UnrealEditor.exe" "%~dp0ShelljackPrototype.uproject" /Game/Prototype/Island -game -windowed -ResX=1440 -ResY=900
