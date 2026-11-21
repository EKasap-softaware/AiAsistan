@echo off
rem PC acildiktan sonra her seyi acar: Docker Desktop, konteynerler (Mongo, RabbitMQ, LiteLLM, LangFlow, Postgres), atolye sayfasi.
cd /d "%~dp0.."
docker info >nul 2>nul || (
  echo Docker Desktop aciliyor...
  start "" "C:\Program Files\Docker\Docker\Docker Desktop.exe"
  :bekle
  timeout /t 5 >nul
  docker info >nul 2>nul || goto bekle
)
docker compose up -d
cd atolye
start "" http://localhost:5300
where python >nul 2>nul && (python sunucu.py) || (py sunucu.py)
