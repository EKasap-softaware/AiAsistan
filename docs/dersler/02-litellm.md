# Ders 2 · LiteLLM (ayrıntılı)

Dosyalar: `litellm/config.yaml`, `docker-compose.yml` (litellm servisi), `src/Asistan.Infrastructure/Llm/LiteLlmIstemcisi.cs`. Bu derste verilen sayılar bu projede **ölçülmüştür**.

---

## 1. LiteLLM nedir?

LiteLLM, 100'den fazla yapay zekâ sağlayıcısını (Anthropic, OpenAI, Google, Azure, yerel Ollama...) **tek bir OpenAI uyumlu API** arkasında toplayan açık kaynaklı bir araçtır. İki biçimde kullanılır: bir Python kütüphanesi (SDK) ya da bağımsız bir **sunucu** (Proxy / AI Gateway). Biz sunucu olarak kullanıyoruz, çünkü .NET Python kütüphanesi çağıramaz ama HTTP çağırır.

**Benzetme:** Elektrik adaptörü. Her ülkenin prizi farklıdır; adaptör tek bir fiş biçimini hepsine uydurur. Yazdığın kod hep aynı fişi (`POST /v1/chat/completions`) kullanır, LiteLLM arkadaki sağlayıcının prizine çevirir.

```
.NET (LiteLlmIstemcisi) ─┐
                          ├─► LiteLLM :4000 ──(Anthropic formatı)──► Claude
LangFlow (OpenAI bileşeni)┘        │
                                   └─ config.yaml: "akilli" → anthropic/claude-sonnet-5-5
```

## 2. Neden gerekli?

| Özellik | Anlamı | Bizde kullanıyor muyuz? |
|---|---|---|
| **Tek arayüz** | Kod hep aynı adrese konuşur; model değiştirmek kod değil ayar değişikliğidir | **Evet** |
| **Takma ad (alias)** | Kodda `akilli`, gerçek model `config.yaml`'da | **Evet** |
| **Parametre uyarlama** | `drop_params`: sağlayıcının desteklemediği parametreleri atar | **Evet** |
| **Master key** | LiteLLM'e girişi koruyan anahtar | **Evet** |
| Fallback | Claude yanıt vermezse otomatik başka modele geç | Hayır (tanımlamadık) |
| Load balancing | Aynı modelin birden çok anahtarı/bölgesi arasında yük dağıtır | Hayır |
| Maliyet/bütçe takibi, sanal anahtarlar | Kullanıcı/proje başına limit | Hayır (veritabanı gerekir, bkz. bölüm 7) |
| Log, rate limit, cache | | Hayır |

Dürüst özet: LiteLLM'in büyük özelliklerinin çoğunu kullanmıyoruz; şimdilik **adaptör ve takma ad** görevinde. Kullanmadıklarımız yarın ayar dosyasına eklenerek açılabilir, uygulama koduna dokunmadan.

## 3. `config.yaml` satır satır

```yaml
model_list:
  - model_name: akilli                       # KODUN çağırdığı ad (takma ad)
    litellm_params:
      model: anthropic/claude-sonnet-5-5     # sağlayıcı/model: GERÇEK model
      api_key: os.environ/ANTHROPIC_API_KEY  # "anahtarı ortam değişkeninden oku"
  - model_name: hizli
    litellm_params:
      model: anthropic/claude-haiku-5-5
      api_key: os.environ/ANTHROPIC_API_KEY

litellm_settings:
  drop_params: true                          # desteklenmeyen parametreleri sessizce at

general_settings:
  master_key: os.environ/LITELLM_MASTER_KEY  # API'nin LiteLLM'e girişini korur
```

- **`os.environ/ADI`:** değer dosyaya yazılmaz, Docker'ın verdiği ortam değişkeninden okunur (`.env` → compose → container). Anahtar Git'e girmez.
- **İki anahtar karışmasın:** `LITELLM_MASTER_KEY` = bizim programların LiteLLM'e girişi. `ANTHROPIC_API_KEY` = LiteLLM'in Anthropic'e girişi.
- **Model adı değişirse** (sağlayıcı yeni sürüm çıkarır) sadece bu dosya düzenlenir; sonra `docker compose restart litellm`. Config dosyası container'a **salt-okunur** bağlıdır (`:ro`).
- **İki takma ad neden?** `akilli` asıl cevaplar için, `hizli` ucuz yan işler için (özetleme, hafıza çıkarma). Aşağıda neden önemli olduğunu sayıyla göreceksin.

## 4. İstek ve cevap biçimi

```http
POST http://localhost:4000/v1/chat/completions
Authorization: Bearer <LITELLM_MASTER_KEY>

{ "model": "akilli",
  "messages": [ {"role": "system", "content": "Sen bir .NET eğitmenisin."},
                {"role": "user",   "content": "DI nedir?"} ],
  "max_tokens": 200 }
```

Cevap (kısaltılmış) ve gerçek bir örnek:
```json
{ "model": "akilli",
  "choices": [ { "message": { "role": "assistant", "content": "Merhaba!" } } ],
  "usage": { "prompt_tokens": 22, "completion_tokens": 8 } }
```

**Mesaj rolleri:** `system` (modelin kimliği ve kuralları), `user` (kullanıcı), `assistant` (modelin önceki cevabı; sohbet geçmişini böyle verirsin). **Model durumsuzdur:** her istekte geçmişi sen gönderirsin.

**.NET'te:** `LiteLlmIstemcisi`, typed `HttpClient` ile bu isteği atar; adres `LiteLlm:BaseUrl`, anahtar `LiteLlm:Key`, zaman aşımı 120 sn. Cevabın `choices[0].message.content` alanını okur. **LangFlow da aynı kapıdan geçer** (OpenAI bileşeninin adresi `http://litellm:4000/v1`). İki istemci, tek kapı: maliyet ve günlükler tek yerde toplanabilir.

## 5. Gerçek deneyler

| Deney | Sonuç |
|---|---|
| `GET /v1/models` | `["akilli", "hizli"]`: sadece takma adlar görünür, gerçek model adı dışarı sızmaz |
| Aynı istek `akilli` ile (3 deneme) | ort. **1.12 sn** |
| Aynı istek `hizli` ile (3 deneme) | ort. **0.84 sn** |
| Aynı istek, LiteLLM'in hesapladığı maliyet | `akilli` **0.000124 $**, `hizli` **0.0000062 $**: yaklaşık **20 kat** fark |
| İstekte `seed=1, temperature=0.1` | **200 OK**. (`drop_params` olmasaydı Claude `seed`'i reddedip hata verirdi) |
| Olmayan model adı | **400**: *"Invalid model name ... Call `/v1/models` to view available models"* (hata mesajı çözümü söylüyor) |
| Yanlış anahtar | **400** `No connected db.` (bölüm 7'de neden) |

Maliyet rakamı, LiteLLM'in **kendi fiyat tablosuna göre tahmindir** (cevap başlığı `x-litellm-response-cost`), fatura değildir. Ama oran önemli: yan işleri (özetleme, hafıza çıkarma) `hizli`ya vermek her çağrıyı kabaca 20 kat ucuzlatır.

**Token ve geçmiş (hafıza dersine bağlantı):** Aynı soruya 0, 4 ve 12 önceki mesajla cevap istedik, `prompt_tokens`: **56 → 216 → 536**. Her ek mesaj ~40 token ekler ve **her istekte yeniden ödenir**. Bu yüzden "hafıza yönetimi" (özetleme, pencere) bir maliyet konusudur.

## 6. Yaşadığımız gerçek hata: `drop_params`

LangFlow'un OpenAI bileşeni isteğe `seed` ve `temperature` ekler. Claude bunlardan bazılarını desteklemez; LiteLLM şu hatayı verdi: *"anthropic does not support parameters: ['temperature', 'seed']"* ve çözümü de söyledi (`drop_params: true`). Ayarı `litellm_settings` altına ekleyip `docker compose restart litellm` yapınca düzeldi.

Bu, LiteLLM'in adaptör görevinin tam örneği: **iki tarafın dili tam uyuşmuyor, aradaki katman uyduruyor.** Önemli not: .NET istemcimiz bu parametreleri hiç göndermez; sorun sadece LangFlow'daydı.

## 7. Sanal anahtarlar neden yok? (`No connected db.`)

LiteLLM'e master key dışında bir anahtar verirsen onu bir **sanal anahtar** (virtual key) sanar ve doğrulamak için bir **veritabanına** bakmak ister. Biz LiteLLM'i veritabansız çalıştırıyoruz, bu yüzden yanlış anahtar **401 yerine** `No connected db.` (400) döner. Anlamı: bu kurulumda sadece master key çalışır, kullanıcı/proje başına anahtar, bütçe ve harcama panosu yoktur. Bunlar için LiteLLM'e bir PostgreSQL verilir (`DATABASE_URL`); bizde yapılmadı.

## 8. Fallback ve yeni model eklemek (kavramsal, bizde denenmedi)

- **Yeni model:** `model_list`'e bir blok eklenir (`model_name` + `litellm_params`), sonra restart. Kod değişmez, istek `"model": "yeni-ad"` ile gelir.
- **Fallback:** Bir modelin hata verdiği durumda otomatik başka modele geçme. LiteLLM belgelerinde yapılandırması anlatılır; bu projede tanımlamadık, o yüzden burada denenmiş bir örnek vermiyorum.

## 9. Güvenlik

- Master key ve Anthropic anahtarı `.env`'de, Git'te değil.
- LiteLLM portu (4000) bilgisayarında; internete açmak için ek koruma gerekir.
- Her istek model başına para harcar: anahtarı sızdıran biri senin hesabından harcama yapar. Konsolda **harcama limiti** koy ve anahtarı sızarsa iptal edip yenile.

## 10. Hata ayıklama

| Belirti | Neden / çözüm |
|---|---|
| `Connection refused :4000` | LiteLLM çalışmıyor: `docker compose ps`, `docker compose logs litellm --tail 50` |
| `Missing Anthropic API Key` (401) | `.env`'deki `ANTHROPIC_API_KEY` boş/yanlış. Değişince `docker compose up -d --force-recreate litellm` (restart yetmez; ortam değişkeni container yaratılırken okunur) |
| `credit balance is too low` | Anthropic hesabında kredi yok (API aboneliğinden ayrı faturalanır) |
| `does not support parameters` | `drop_params: true` |
| `Invalid model name` | `config.yaml`'daki `model_name` ile istekteki `model` uyuşmuyor; `GET /v1/models` ile bak |
| `No connected db.` | Master key dışında bir anahtar gönderilmiş |
| Model adı artık geçersiz | Sağlayıcı model adını değiştirdi: `config.yaml`'ı güncel adla düzenle |

## 11. Bilmen gerekenler

1. LiteLLM = OpenAI uyumlu tek kapı; arkada Claude, GPT, yerel model... Kod modeli bilmez, takma adı bilir.
2. Kod `akilli`/`hizli` der; gerçek model `config.yaml`'da. Model değiştirmek ayar değişikliğidir.
3. `os.environ/...` ile anahtarlar dosyaya girmez. İki anahtarı karıştırma (master key ≠ Anthropic anahtarı).
4. `drop_params`: uygulamaların gönderdiği ama sağlayıcının desteklemediği parametreleri atar.
5. Ucuz yan işler için ucuz model: ~20 kat maliyet farkı ölçtük.
6. Geçmiş her istekte yeniden gönderilir ve ücretlendirilir (12 mesaj ≈ 536 token): hafıza yönetimi maliyet konusudur.
7. Veritabanı olmadan sanal anahtar, bütçe ve harcama panosu yok; sadece master key.
8. Compose/ortam değişkeni değişince `restart` değil `up -d` (gerekirse `--force-recreate`).
