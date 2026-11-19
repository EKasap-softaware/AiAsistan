function Lab08 {
    $yol = Yol 'langflow/atolye-akisi.json'
    $ham = if (Test-Path $yol) { Get-Content $yol -Raw -Encoding UTF8 } else { '' }
    $j = try { $ham | ConvertFrom-Json } catch { $null }
    Kontrol 'G1 langflow/atolye-akisi.json var ve geçerli JSON (LangFlow''dan Export)' ($null -ne $j -and $j.data) `
        'http://localhost:7860 > akışın üstünde isim > Export > JSON dosyasını langflow/atolye-akisi.json olarak kaydet'
    if (-not $j -or -not $j.data) { return }
    $tipler = @($j.data.nodes | ForEach-Object { $_.data.type })
    Kontrol ("G2 akışta ChatInput ve ChatOutput bileşenleri var (bulunan: {0})" -f ($tipler -join ', ')) ($tipler -contains 'ChatInput' -and $tipler -contains 'ChatOutput') 'Sol menüden Inputs > Chat Input ve Outputs > Chat Output sürükle'
    Kontrol 'G3 en az 4 bileşen ve bunları bağlayan en az 3 kablo (edge)' (@($j.data.nodes).Count -ge 4 -and @($j.data.edges).Count -ge 3) 'Chat Input > Prompt > Language Model > Chat Output zinciri (en az 4 bileşen)'
    Kontrol 'G4 bir Prompt bileşeni var (şablon ile)' ($tipler -contains 'Prompt' -or $tipler -contains 'Prompt Template') 'Prompts > Prompt bileşeni; şablonda {soru} gibi bir değişken kullan'
    $e = EnvOku
    $sizinti = $false
    foreach ($k in @('LITELLM_MASTER_KEY', 'ANTHROPIC_API_KEY', 'LANGFLOW_API_KEY', 'LANGFLOW_SECRET_KEY', 'LANGFLOW_SUPERUSER_PASSWORD')) {
        if ($e[$k] -and $e[$k].Length -ge 8 -and $ham.Contains($e[$k])) { $sizinti = $true }
    }
    if ($ham -match 'sk-[A-Za-z0-9_-]{20,}') { $sizinti = $true }
    Kontrol 'G5 dışa aktarılan dosyada GİZLİ anahtar yok (API anahtarı akışa gömülmedi)' (-not $sizinti) `
        'Anahtarı bileşene yazma; Settings > Global Variables''a Credential olarak ekle (LITELLM_KEY gibi) ve bileşende onu seç, sonra yeniden Export et'
}
