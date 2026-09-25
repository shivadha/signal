@echo off
cd /d "%~dp0\.."
echo Starting SIGNAL 24/7 Background Service...
dotnet run --project src/Signal.Worker
