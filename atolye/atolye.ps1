param(
    [Parameter(Position = 0)][string]$Komut = 'oyna',
    [Parameter(Position = 1)][string]$Lab = '',
    [string]$Kok = '',
    [switch]$Kilitsiz
)
if (-not $Kok) { $Kok = Split-Path -Parent $PSScriptRoot }
[Console]::OutputEncoding = [Text.Encoding]::UTF8
# Türkçe yerel ayarda büyük/küçük harf eşleşmesi 'I' harfinde bozulur (RABBITMQ_USER okunmaz). Kültürü sabitle.
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture
$ErrorActionPreference = 'Continue'
$script:hata = 0
$Ilerleme = Join-Path $PSScriptRoot '.ilerleme.json'

$Labler = @(
    @{ Id = '01'; Ad = '.NET + EF Core + Solution + Unit Test'; Fn = 'Lab01' },
    @{ Id = '02'; Ad = 'Git / GitHub'; Fn = 'Lab02' },
    @{ Id = '03'; Ad = 'Docker'; Fn = 'Lab03' },
    @{ Id = '04'; Ad = 'MongoDB'; Fn = 'Lab04' },
    @{ Id = '05'; Ad = 'RabbitMQ'; Fn = 'Lab05' },
    @{ Id = '06'; Ad = 'LiteLLM'; Fn = 'Lab06' },
    @{ Id = '07'; Ad = 'AI Memory'; Fn = 'Lab07' },
    @{ Id = '08'; Ad = 'LangFlow'; Fn = 'Lab08' },
    @{ Id = '09'; Ad = 'CI/CD'; Fn = 'Lab09' },
    @{ Id = '10'; Ad = 'Claude CLI / Skills / MCP / Markdown'; Fn = 'Lab10' }
)

# ---------- ortak yardımcılar (labs/*.ps1 bunları kullanır) ----------
function Kontrol([string]$ad, [bool]$tamam, [string]$ipucu) {
    if ($tamam) { Write-Host "  ✔ $ad" -ForegroundColor Green }
    else { Write-Host "  ✘ $ad" -ForegroundColor Red; Write-Host "      ipucu: $ipucu" -ForegroundColor DarkYellow; $script:hata++ }
}
function Yol([string]$g) { Join-Path $Kok $g }
function Icerik([string]$g) { if (Test-Path (Yol $g)) { Get-Content (Yol $g) -Raw -Encoding UTF8 } else { '' } }
function TumKod([string]$klasor) {
    Get-ChildItem (Yol $klasor) -Recurse -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
}
# Dış programı çalıştırır; stderr'i hata saymaz. Dönüş: @{Kod; Cikti}
function Calistir([string]$exe, [string[]]$argumanlar, [string]$cwd = $Kok, [int]$sn = 120) {
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = $exe; $psi.WorkingDirectory = $cwd
    $psi.Arguments = (($argumanlar | ForEach-Object { if ($_ -eq '' -or $_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }) -join ' ')
    $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true; $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8; $psi.StandardErrorEncoding = [Text.Encoding]::UTF8
    try { $p = [Diagnostics.Process]::Start($psi) } catch { return @{ Kod = -2; Cikti = "çalıştırılamadı: $exe" } }
    $o = $p.StandardOutput.ReadToEndAsync(); $e = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($sn * 1000)) { try { $p.Kill() } catch {}; return @{ Kod = -1; Cikti = 'zaman aşımı' } }
    return @{ Kod = $p.ExitCode; Cikti = ($o.Result + $e.Result) }
}
# .env dosyasını okur (değerler ekrana yazılmaz)
function EnvOku {
    $h = @{}
    $f = Yol '.env'
    if (Test-Path $f) {
        Get-Content $f -Encoding UTF8 | ForEach-Object { if ($_ -cmatch '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { $h[$Matches[1]] = $Matches[2].Trim() } }
    }
    return $h
}
function SonSatir([string]$metin) { ($metin -split "`n" | Where-Object { $_.Trim() } | Select-Object -Last 1) }

# ---------- lab dosyaları ----------
Get-ChildItem (Join-Path $PSScriptRoot 'labs') -Filter 'Lab*.ps1' | Sort-Object Name | ForEach-Object { . $_.FullName }

# ---------- seviye sistemi ----------
function Tamamlananlar {
    if (Test-Path $Ilerleme) { try { return @((Get-Content $Ilerleme -Raw | ConvertFrom-Json).tamam) } catch { return @() } }
    return @()
}
function Kaydet($liste) { @{ tamam = @($liste) } | ConvertTo-Json | Set-Content $Ilerleme -Encoding UTF8 }
function Guncel { $t = Tamamlananlar; foreach ($l in $Labler) { if ($t -notcontains $l.Id) { return $l } }; return $null }
function Kilitli($id) {
    if ($Kilitsiz) { return $false }
    $t = Tamamlananlar
    foreach ($l in $Labler) { if ($l.Id -eq $id) { return $false }; if ($t -notcontains $l.Id) { return $true } }
    return $true
}
function Hazirla($id) {   # başlangıç dosyalarını kum klasörüne kopyalar (yoksa)
    $kaynak = Join-Path $PSScriptRoot "baslangic/lab$id"; $hedef = Join-Path $PSScriptRoot "kum/lab$id"
    if ((Test-Path $kaynak) -and -not (Test-Path $hedef)) { Copy-Item $kaynak $hedef -Recurse; Write-Host "  (başlangıç dosyaları hazırlandı: atolye/kum/lab$id)" -ForegroundColor DarkGray }
}
function MdYolu($id) { (Get-ChildItem $PSScriptRoot -Filter "lab$id-*.md" | Select-Object -First 1).FullName }
function Cubuk { $n = (Tamamlananlar).Count; return ('█' * $n) + ('░' * (10 - $n)) + " $n/10" }
function Calistir-Lab($l) {
    $script:hata = 0
    Write-Host ("`n=== LEVEL {0}/10 · {1} ===" -f [int]$l.Id, $l.Ad) -ForegroundColor Cyan
    Hazirla $l.Id
    & $l.Fn | Out-Host
    return ($script:hata -eq 0)
}
function Seviye-Atla($l) {
    $t = @(Tamamlananlar) + $l.Id | Select-Object -Unique
    Kaydet $t
    Write-Host ("`n🎉 LEVEL {0} TAMAM!  {1}" -f [int]$l.Id, (Cubuk)) -ForegroundColor Green
    $sonraki = Guncel
    if ($null -eq $sonraki) { Write-Host '🏆 10 LABIN HEPSİ TAMAM. İşe hazırsın; mülakat sorularını tekrar et.' -ForegroundColor Yellow }
    else {
        Write-Host ("Sıradaki: Lab {0}: {1}" -f $sonraki.Id, $sonraki.Ad) -ForegroundColor Cyan
        Write-Host ("Görevler ve örnekler: " + (MdYolu $sonraki.Id)) -ForegroundColor Cyan
        Write-Host 'Hazır olunca: ./atolye/atolye.ps1' -ForegroundColor DarkGray
    }
}

switch ($Komut) {
    'oyna' {
        $l = Guncel
        if ($null -eq $l) { Write-Host '🏆 Hepsi tamam!' -ForegroundColor Yellow; break }
        if (Calistir-Lab $l) { Seviye-Atla $l }
        else {
            Write-Host ("`n{0} adım eksik. Düzelt, sonra yine ./atolye/atolye.ps1 çalıştır." -f $script:hata) -ForegroundColor Yellow
            Write-Host ("Görevler ve örnekler: " + (MdYolu $l.Id)) -ForegroundColor DarkGray; exit 1
        }
    }
    'dogrula' {
        $l = $Labler | Where-Object { $_.Id -eq $Lab.PadLeft(2, '0') }
        if (-not $l) { Write-Host "Lab '$Lab' yok. 01-10 arası." -ForegroundColor Red; exit 2 }
        if (Kilitli $l.Id) { Write-Host "🔒 Lab $($l.Id) kilitli. Önce önceki labları bitir (./atolye/atolye.ps1 durum)." -ForegroundColor Red; exit 3 }
        if (Calistir-Lab $l) { if ((Tamamlananlar) -notcontains $l.Id -and -not $Kilitsiz) { Seviye-Atla $l } else { Write-Host "`nLab $($l.Id) geçti." -ForegroundColor Green } }
        else { Write-Host ("`n{0} adım eksik." -f $script:hata) -ForegroundColor Yellow; exit 1 }
    }
    'durum' {
        Write-Host ("İlerleme: " + (Cubuk)) -ForegroundColor Cyan
        $t = Tamamlananlar; $g = Guncel
        foreach ($l in $Labler) {
            $sim = if ($t -contains $l.Id) { '✔' } elseif ($g -and $g.Id -eq $l.Id) { '▶' } else { '🔒' }
            Write-Host ("  {0} Lab {1}  {2}" -f $sim, $l.Id, $l.Ad)
        }
    }
    'oku' {
        $l = if ($Lab) { $Labler | Where-Object { $_.Id -eq $Lab.PadLeft(2, '0') } } else { Guncel }
        if (-not $l) { Write-Host 'Lab bulunamadı.'; exit 2 }
        if (Kilitli $l.Id) { Write-Host '🔒 Kilitli.' -ForegroundColor Red; exit 3 }
        Hazirla $l.Id; Get-Content (MdYolu $l.Id) -Encoding UTF8
    }
    'ipucu' { Write-Host "Her ✘ satırının altındaki 'ipucu' ne yapacağını söyler. Örnekler için: ./atolye/atolye.ps1 oku" }
    'cozum' {
        $id = if ($Lab) { $Lab.PadLeft(2, '0') } else { (Guncel).Id }
        if (Kilitli $id) { Write-Host '🔒 Kilitli.' -ForegroundColor Red; exit 3 }
        Write-Host "Referans çözüm: $(Join-Path $PSScriptRoot "cozumler/lab$id") (önce kendin dene!)"
    }
    'sifirla' { if (Test-Path $Ilerleme) { Remove-Item $Ilerleme }; Write-Host 'İlerleme sıfırlandı (Lab 01).' }
    default { Write-Host 'Kullanım: atolye.ps1 [oyna] | durum | oku [NN] | dogrula NN | ipucu | cozum [NN] | sifirla' }
}
