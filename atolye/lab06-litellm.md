# Lab 06: LiteLLM

**Hedef:** LiteLLM proxy'sine kendi model takma adını (alias) ekle, sınırlarını koy, gerçek bir istek at.
**Dosya:** `litellm/config.yaml`

Zihinsel model: uygulamalar `akilli`, `hizli` gibi **takma ad** çağırır; arkadaki gerçek model config'te değişir. Böylece
sağlayıcı/model değişince kod değişmez, ucuz/pahalı model ayrımı tek yerde yönetilir. API anahtarı yalnızca LiteLLM'de durur.

## Görevler

**G1. Yeni model kaydı `atolye` (haiku'ya bağlı).**
> Örnek (mevcut kayıtlara bak, aynı kalıp):
> ```yaml
> model_list:
>   - model_name: hizli
>     litellm_params:
>       model: anthropic/claude-haiku-5-5
>       api_key: os.environ/ANTHROPIC_API_KEY   # değer .env'den gelir, dosyaya yazılmaz
> ```
> Sen aynısından `model_name: atolye` olanı ekle.

**G2. Maliyet sınırı: `max_tokens: 50`** (`litellm_params` içine). Cevap en fazla 50 token olur.
> Örnek: `litellm_params:` altına `max_tokens: 50`. Neden? Hata/kötüye kullanım hesabı şişirmesin.

**G3. Dış çağrıya zaman sınırı.** `litellm_settings:` altına `request_timeout: 30`.
> Örnek: projenin kuralı "dış sisteme giden her çağrının zaman sınırı olsun" — takılan sağlayıcı bütün sistemi kilitlemesin.

**G4. Uygula.** Config sadece açılışta okunur:
```powershell
docker compose restart litellm
```
Doğrulayıcı `/v1/models` listesine bakar (master key ile). Elle dene (anahtarı ekrana yazmadan):
```powershell
$k = (Select-String -Path .env -Pattern '^LITELLM_MASTER_KEY=(.*)$').Matches[0].Groups[1].Value
curl.exe -s -H "Authorization: Bearer $k" http://localhost:4000/v1/models
```

**G5. Gerçek istek.** Doğrulayıcı `atolye` modeline kısa bir istek atar (~0.00001 $). Sen de dene:
> Örnek gövde: `{"model":"hizli","messages":[{"role":"user","content":"merhaba de"}]}` → `POST /v1/chat/completions`
> Cevapta `usage` (prompt/completion tokens) gör: maliyet bununla hesaplanır.

## Sık hatalar
- `restart` unutuldu → model listede yok.
- YAML girinti: boşluk kullan, TAB değil; `model_name` ile `litellm_params` aynı hizada.
- 401: master key yanlış. 400 "No connected db": anahtar yönetimi için veritabanı lazım (bu kurulumda yok).
- `drop_params: true` neden var? OpenAI formatındaki istekteki Claude'un desteklemediği parametreler hata vermesin.

## Mülakat soruları
1. LLM gateway/proxy neden kullanılır (tek API, anahtar gizleme, fallback, maliyet, rate limit)?
2. Alias (model_name) ile gerçek model ayrımının faydası?
3. Master key ile sağlayıcı anahtarı farkı? Hangisi nerede durur?
4. Token nedir, maliyet nasıl hesaplanır? Neden küçük model (hizli) özetlemede?
5. Fallback ve retry nasıl kurulur (router_settings)?
