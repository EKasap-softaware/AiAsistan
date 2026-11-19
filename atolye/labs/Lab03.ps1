function Lab03 {
    $d = Yol 'atolye/kum/lab03'
    $df = if (Test-Path "$d/Dockerfile") { Get-Content "$d/Dockerfile" -Raw -Encoding UTF8 } else { '' }
    $user = [regex]::Matches($df, '(?m)^\s*USER\s+(\S+)') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Last 1

    Kontrol 'G1 Dockerfile: FROM, COPY, EXPOSE 8000, CMD/ENTRYPOINT' `
        ($df -match '(?m)^\s*FROM\s+\S+' -and $df -match '(?m)^\s*COPY\s+' -and $df -match '(?m)^\s*EXPOSE\s+8000' -and $df -match '(?m)^\s*(CMD|ENTRYPOINT)\s+') `
        'atolye/kum/lab03/Dockerfile: FROM python:3.12-slim / WORKDIR /app / COPY app.py . / EXPOSE 8000 / CMD ["python","app.py"]'
    Kontrol 'G2 Dockerfile: root olmayan kullanıcı (USER)' ($user -and $user -notmatch '^(root|0)$') `
        'RUN useradd -m uygulama  ve sonra  USER uygulama  (CMD''den ÖNCE)'
    $di = if (Test-Path "$d/.dockerignore") { Get-Content "$d/.dockerignore" -Raw } else { '' }
    Kontrol 'G3 .dockerignore: .git ve __pycache__ dışarıda' ($di -match '\.git' -and $di -match '__pycache__') `
        '.dockerignore dosyasına satır satır .git ve __pycache__ yaz (imaja gereksiz dosya girmesin)'

    if (-not ($df -match 'FROM')) { Kontrol 'G4 imaj derleniyor' $false 'önce Dockerfile yaz'; return }
    Write-Host '  … docker build (ilk seferde imaj indirilebilir, bekle)' -ForegroundColor DarkGray
    $b = Calistir 'docker' @('build', '-t', 'atolye-lab03', $d) $Kok 400
    Kontrol 'G4 docker build başarılı' ($b.Kod -eq 0) ('docker build -t atolye-lab03 atolye/kum/lab03  | hata: ' + ((SonSatir $b.Cikti)))
    if ($b.Kod -ne 0) { return }

    Calistir 'docker' @('rm', '-f', 'atolye-lab03-test') $Kok 30 | Out-Null
    $r = Calistir 'docker' @('run', '-d', '--name', 'atolye-lab03-test', '-p', '18099:8000', 'atolye-lab03') $Kok 60
    Start-Sleep -Seconds 3
    $c = Calistir 'curl.exe' @('-s', '-m', '5', 'http://localhost:18099') $Kok 15
    Kontrol 'G5 konteyner çalışıyor ve 8000 portu cevap veriyor' ($r.Kod -eq 0 -and $c.Cikti -match 'merhaba') `
        'docker run -d -p 18099:8000 atolye-lab03 ; curl http://localhost:18099  ("merhaba atolye" gelmeli). docker logs ile bak'
    $u = Calistir 'docker' @('exec', 'atolye-lab03-test', 'id', '-u') $Kok 20
    Kontrol 'G6 konteyner root olarak ÇALIŞMIYOR (id -u ≠ 0)' ($u.Kod -eq 0 -and $u.Cikti.Trim() -ne '0') 'Dockerfile''da USER satırı CMD''den önce olmalı'
    Calistir 'docker' @('rm', '-f', 'atolye-lab03-test') $Kok 30 | Out-Null

    $cf = if (Test-Path "$d/docker-compose.yml") { Get-Content "$d/docker-compose.yml" -Raw -Encoding UTF8 } else { '' }
    Kontrol 'G7 docker-compose.yml: servis web, build, ports, environment (MESAJ), volumes' `
        ($cf -match '(?m)^\s*web:' -and $cf -match 'build:' -and $cf -match 'ports:' -and $cf -match 'MESAJ' -and $cf -match 'volumes:') `
        'services: web: build: . / ports: ["18098:8000"] / environment: MESAJ: ... / volumes: - veri:/veri  (en altta volumes: veri:)'
    if ($cf) {
        $up = Calistir 'docker' @('compose', '-p', 'atolye-lab03', 'up', '-d', '--build') $d 400
        Start-Sleep -Seconds 3
        $p = Calistir 'docker' @('compose', '-p', 'atolye-lab03', 'port', 'web', '8000') $d 30
        $hostPort = ($p.Cikti.Trim() -split ':')[-1]
        $h = if ($hostPort -match '^\d+$') { Calistir 'curl.exe' @('-s', '-m', '5', '-o', 'NUL', '-w', '%{http_code}', "http://localhost:$hostPort") $Kok 15 } else { @{ Cikti = '' } }
        Kontrol 'G8 docker compose up çalışıyor (web 200 dönüyor)' ($up.Kod -eq 0 -and $h.Cikti.Trim() -eq '200') ('docker compose up -d --build  | ' + (SonSatir $up.Cikti))
        Calistir 'docker' @('compose', '-p', 'atolye-lab03', 'down', '-v') $d 120 | Out-Null
    } else { Kontrol 'G8 docker compose up çalışıyor' $false 'önce docker-compose.yml yaz' }
}
