@echo off
REM Quick launcher for Android APK build, release push, and run
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-android.ps1" %*
