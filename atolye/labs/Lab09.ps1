function Lab09 {
    $t = Icerik '.github/workflows/atolye-ci.yml'
    Kontrol 'G1 .github/workflows/atolye-ci.yml dosyası var' ($t.Length -gt 0) 'Örnek: .github/workflows/ci.yml (gerçek projenin CI''ı). Yeni dosya aç, kendi başına yaz'
    if (-not $t) { return }
    Kontrol 'G2 tetikleyici: push (main) ve pull_request' ($t -match '(?m)^on:' -and $t -match 'push:' -and $t -match 'pull_request:') 'on: push: branches: [main]  /  pull_request:'
    Kontrol 'G3 ubuntu-latest üzerinde bir job' ($t -match '(?m)^jobs:' -and $t -match 'runs-on:\s*ubuntu-latest') 'jobs: test: runs-on: ubuntu-latest'
    Kontrol 'G4 actions/checkout + actions/setup-dotnet (dotnet-version 9)' ($t -match 'actions/checkout@' -and $t -match 'actions/setup-dotnet@' -and $t -match 'dotnet-version:\s*[''"]?9') 'steps: - uses: actions/checkout@v4 / - uses: actions/setup-dotnet@v4 with: dotnet-version: 9.0.x'
    Kontrol 'G5 dotnet restore/build ve dotnet test adımları' ($t -match 'dotnet build' -and $t -match 'dotnet test') '- run: dotnet build --no-restore -c Release / - run: dotnet test --no-build -c Release'
    Kontrol 'G6 NuGet önbelleği (cache)' ($t -match 'actions/cache@' -or $t -match '(?m)^\s*cache:\s*true') 'setup-dotnet with: cache: true  (cache-dependency-path ister) veya actions/cache'
    Kontrol 'G7 en az yetki: permissions: contents: read' ($t -match '(?m)^permissions:' -and $t -match 'contents:\s*read') 'dosyanın üstüne: permissions: contents: read  (token gereksiz yetki taşımasın)'
    Kontrol 'G8 timeout-minutes ve concurrency (takılan/yığılan işleri kes)' ($t -match 'timeout-minutes:\s*\d+' -and $t -match '(?m)^concurrency:') 'job''a timeout-minutes: 15 ; kökte concurrency: group: ${{ github.workflow }}-${{ github.ref }} / cancel-in-progress: true'
    Kontrol 'G9 dosyada düz yazı parola/anahtar yok' (-not ($t -match '(?i)(password|api[_-]?key|token)\s*:\s*[''"]?[A-Za-z0-9]{16,}')) 'gizli değerler secrets.ADI ile okunur: ${{ secrets.ADI }}'
    $al = Get-Command actionlint -ErrorAction SilentlyContinue
    if ($al) {
        $r = Calistir 'actionlint' @((Yol '.github/workflows/atolye-ci.yml')) $Kok 60
        Kontrol 'G10 actionlint (workflow dosyası sözdizimi/mantık kontrolü) temiz' ($r.Kod -eq 0) ($r.Cikti.Trim())
    } else { Write-Host '  • actionlint kurulu değil: G10 atlandı (isteğe bağlı: choco install actionlint)' -ForegroundColor DarkGray }
}
