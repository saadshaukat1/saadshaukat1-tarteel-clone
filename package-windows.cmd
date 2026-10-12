@echo off
REM 1-click launcher to build, package and push self-contained Windows release to GitHub Releases
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\release\make-setup.ps1" %*
