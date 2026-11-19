function Lab06 {
    $e = EnvOku
    $cfg = Icerik 'litellm/config.yaml'
    Kontrol 'G1 litellm/config.yaml: model_name: atolye (haiku''ya bağlı)' ($cfg -match '(?m)^\s*-\s*model_name:\s*atolye\s*$' -and $cfg -match '(?s)model_name:\s*atolye\s.*?claude-haiku') `
        'model_list''e yeni bir kayıt: model_name: atolye / litellm_params: model: anthropic/claude-haiku-5-5, api_key: os.environ/ANTHROPIC_API_KEY'
    Kontrol 'G2 atolye modeline max_tokens: 50 sınırı koydun' ($cfg -match '(?s)model_name:\s*atolye\s.*?max_tokens:\s*50') 'litellm_params içine: max_tokens: 50  (maliyet ve kaçak kullanım sınırı)'
    Kontrol 'G3 dış çağrıya zaman sınırı: request_timeout (litellm_settings)' ($cfg -match '(?m)^\s*request_timeout:\s*\d+') 'litellm_settings altına: request_timeout: 30   ("her dış çağrıya zaman sınırı" kuralı)'

    if (-not $e['LITELLM_MASTER_KEY']) { Kontrol 'ön koşul: .env içinde LITELLM_MASTER_KEY' $false '.env.example''ı kopyalayıp .env yap'; return }
    $m = Calistir 'curl.exe' @('-s', '-m', '10', '-H', ('Authorization: Bearer ' + $e['LITELLM_MASTER_KEY']), 'http://localhost:4000/v1/models') $Kok 20
    $ids = try { @(($m.Cikti | ConvertFrom-Json).data | ForEach-Object { $_.id }) } catch { @() }
    Kontrol ("G4 LiteLLM'de 'atolye' modeli görünüyor (görünenler: {0})" -f ($ids -join ', ')) ($ids -contains 'atolye') `
        'config değişince:  docker compose restart litellm   (config yalnızca açılışta okunur)'
    if ($ids -contains 'atolye') {
        $govde = '{"model":"atolye","messages":[{"role":"user","content":"Sadece su kelimeyi yaz: tamam"}]}'
        $c = Calistir 'curl.exe' @('-s', '-m', '40', '-H', ('Authorization: Bearer ' + $e['LITELLM_MASTER_KEY']), '-H', 'Content-Type: application/json', '-d', $govde, 'http://localhost:4000/v1/chat/completions') $Kok 60
        $j = try { $c.Cikti | ConvertFrom-Json } catch { $null }
        Kontrol 'G5 atolye modeline gerçek bir istek attın ve cevap geldi (çok ucuz: ~0.00001$)' ($j -and $j.choices -and $j.usage.total_tokens -gt 0) `
            'curl ile POST /v1/chat/completions {"model":"atolye",...} ; hata varsa docker compose logs litellm'
    } else { Kontrol 'G5 atolye modeline istek' $false 'önce G4' }
}
