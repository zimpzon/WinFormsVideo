@echo off
setlocal

set "DIR=C:\WinFormsVideo"

if not exist "%DIR%\ffmpeg.exe" (
    echo.
    echo ERROR: FFmpeg is missing.
    echo Please place ffmpeg.exe in:
    echo %DIR%
    echo.
    pause
    exit /b 1
)

if not exist "%DIR%\mediamtx.exe" (
    echo.
    echo ERROR: MediaMTX is missing.
    echo Please place mediamtx.exe in:
    echo %DIR%
    echo.
    pause
    exit /b 1
)

if not exist "%DIR%\mediamtx.yml" (
    echo.
    echo ERROR: MediaMTX configuration is missing.
    echo Please place mediamtx.yml in:
    echo %DIR%
    echo.
    pause
    exit /b 1
)

if not exist "%DIR%\video.mp4" (
    echo.
    echo ERROR: Video file is missing.
    echo Please place a video named video.mp4 in:
    echo %DIR%
    echo.
    pause
    exit /b 1
)

echo Starting MediaMTX...
start "MediaMTX" cmd /k ""%DIR%\mediamtx.exe" "%DIR%\mediamtx.yml""

timeout /t 1 /nobreak >nul

echo Starting video stream...
echo.
echo RTSP stream: rtsp://127.0.0.1:8554/live
echo.

"%DIR%\ffmpeg.exe" -re -stream_loop -1 ^
    -i "%DIR%\video.mp4" ^
    -map 0:v:0 ^
    -c:v libx264 ^
    -preset ultrafast ^
    -tune zerolatency ^
    -profile:v baseline ^
    -g 15 ^
    -bf 0 ^
    -an ^
    -rtsp_transport udp ^
    -f rtsp rtsp://127.0.0.1:8554/live
    
echo.
echo Video stream stopped.
pause
