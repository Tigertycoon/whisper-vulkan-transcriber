@echo off
setlocal
chcp 65001 >nul
set "MEDIA=%~f2"
if "%~2"=="" (
    echo Drag one audio or video file onto a launcher.
    pause
    exit /b 1
)
pushd "%~dp0.." || exit /b 1
set "WHISPER_TRANSCRIBER_HOME=%CD%"
set "MODE=--plain"
if /i "%~1"=="tracks" set "MODE=--multi-track"
if /i "%~1"=="two" set "MODE=--single-track --speakers 2"
if /i "%~1"=="three" set "MODE=--single-track --speakers 3"
dotnet build src\WhisperTranscriber -c Release --nologo
if errorlevel 1 (
    popd
    pause
    exit /b 1
)
src\WhisperTranscriber\bin\Release\net10.0\WhisperTranscriber.exe "%MEDIA%" %MODE%
set "RESULT=%ERRORLEVEL%"
popd
pause
exit /b %RESULT%
