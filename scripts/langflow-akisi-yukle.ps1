<#
.SYNOPSIS
  langflow/dokuman-asistani.json akışını çalışan LangFlow'a yükler (yeni kurulumda ya da akış silindiğinde).

.DESCRIPTION
  1) LangFlow'a .env'deki süper kullanıcıyla giriş yapar.
  2) LITELLM_KEY adlı GİZLİ değişkeni oluşturur (yoksa). Akış anahtarı değil, bu değişkenin ADINI kullanır;
     böylece akış dosyası Git'e girse de anahtar sızmaz.
  3) Aynı endpoint adlı eski akış varsa siler, JSON dosyasındakini yükler.
  4) .env'de LANGFLOW_API_KEY boşsa yeni bir API anahtarı üretip .env'ye yazar (.NET programları bunu kullanır).

  Kullanım:  powershell -ExecutionPolicy Bypass -File scripts\langflow-akisi-yukle.ps1
#>
param(
    [string]$BaseUrl = "http://localhost:7860",
    [string]$AkisDosyasi = (Join-Path $PSScriptRoot "..\langflow\dokuman-asistani.json"),
    [string]$EnvDosyasi = (Join-Path $PSScriptRoot "..\.env")
)
$ErrorActionPreference = "Stop"

# .env'yi oku (değerler ekrana yazılmaz)
$ayar = @{}
Get-Content $EnvDosyasi -Encoding UTF8 | ForEach-Object {
    if ($_ -match '^\s*([^#=][^=]*)=(.*)$') { $ayar[$Matches[1].Trim()] = $Matches[2] }
}
foreach ($g in "LANGFLOW_SUPERUSER", "LANGFLOW_SUPERUSER_PASSWORD", "LITELLM_MASTER_KEY") {
    if (-not $ayar[$g]) { throw ".env dosyasında $g yok. .env.example'a bak." }
}

function Json-Gonder($yontem, $adres, $govdeMetni, $baslik) {
    Invoke-RestMethod -Method $yontem -Uri $adres -Headers $baslik -ContentType "application/json; charset=utf-8" `
        -Body ([Text.Encoding]::UTF8.GetBytes($govdeMetni))
}

# 1) Giriş
$token = (Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/login" `
    -Body @{ username = $ayar.LANGFLOW_SUPERUSER; password = $ayar.LANGFLOW_SUPERUSER_PASSWORD }).access_token
$yetki = @{ Authorization = "Bearer $token" }
Write-Host "Giriş yapıldı."

# 2) LITELLM_KEY gizli değişkeni
$degiskenler = Invoke-RestMethod -Uri "$BaseUrl/api/v1/variables/" -Headers $yetki
if (-not ($degiskenler | Where-Object { $_.name -eq "LITELLM_KEY" })) {
    $gov = @{ name = "LITELLM_KEY"; value = $ayar.LITELLM_MASTER_KEY; type = "Credential"; default_fields = @() } | ConvertTo-Json
    Json-Gonder Post "$BaseUrl/api/v1/variables/" $gov $yetki | Out-Null
    Write-Host "LITELLM_KEY değişkeni oluşturuldu."
} else {
    Write-Host "LITELLM_KEY değişkeni zaten var."
}

# 3) Aynı endpoint adlı eski akışı sil
$ham = Get-Content $AkisDosyasi -Raw -Encoding UTF8
$endpoint = ($ham | ConvertFrom-Json).endpoint_name
$mevcut = Invoke-RestMethod -Uri "$BaseUrl/api/v1/flows/?get_all=true&header_flows=false" -Headers $yetki |
    Where-Object { $_.endpoint_name -eq $endpoint }
foreach ($m in $mevcut) {
    Invoke-RestMethod -Method Delete -Uri "$BaseUrl/api/v1/flows/$($m.id)" -Headers $yetki | Out-Null
    Write-Host "Eski akış silindi: $($m.id)"
}

# 4) Akışı yükle (JSON dosyası olduğu gibi gönderilir)
$yeni = Json-Gonder Post "$BaseUrl/api/v1/flows/" $ham $yetki
Write-Host "Akış yüklendi: $($yeni.name) (endpoint: $($yeni.endpoint_name))"

# 5) API anahtarı yoksa üret ve .env'ye yaz
if (-not $ayar.LANGFLOW_API_KEY) {
    $anahtar = (Json-Gonder Post "$BaseUrl/api/v1/api_key/" (@{ name = "aiasistan-dotnet" } | ConvertTo-Json) $yetki).api_key
    $icerik = Get-Content $EnvDosyasi -Raw -Encoding UTF8
    $icerik = [regex]::Replace($icerik, '(?m)^LANGFLOW_API_KEY=.*$', "LANGFLOW_API_KEY=$anahtar")
    [IO.File]::WriteAllText((Resolve-Path $EnvDosyasi), $icerik, (New-Object Text.UTF8Encoding($false)))
    Write-Host "LANGFLOW_API_KEY üretildi ve .env'ye yazıldı. .NET için kasayı güncelle: dotnet user-secrets set LangFlow:ApiKey <değer> -p src/Asistan.Api"
}
Write-Host "Tamam."
