@echo off
rem Atolye ogretim sayfasini baslatir: http://localhost:5300
cd /d "%~dp0"
start "" http://localhost:5300
where python >nul 2>nul && (python sunucu.py) || (py sunucu.py)
