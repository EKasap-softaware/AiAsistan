# Ders 1 · LangFlow (ayrıntılı)

Bu ders, projedeki **gerçek** LangFlow akışı ve bu akışı kurarken yaşadığımız **gerçek** hatalar üzerinden anlatılır. Kod ve dosyalar: `docker-compose.yml` (langflow servisi), `langflow/dokuman-asistani.json`, `scripts/langflow-akisi-yukle.ps1`, `src/Asistan.Infrastructure/LangFlow/LangFlowIstemcisi.cs`, `src/Asistan.Application/Dokuman/DokumanServisi.cs`.

---

## 1. LangFlow nedir?

LangFlow, yapay zekâ uygulamalarını **sürükle-bırak ile görsel olarak** kurduğun açık kaynaklı bir araçtır (Python tabanlı). Ekrana kutular (bileşenler) koyar, aralarını çizgilerle bağlarsın: "şu girdi → şu şablon → şu model → şu çıktı". Kurduğun her akış **otomatik olarak bir REST adresi** olur; kendi programın (bizde .NET) o adrese HTTP isteği atıp akışı çalıştırır.

**Benzetme:** Lego. Her parça (bileşen) tek bir iş yapar; sen parçaları bağlayarak bir şey inşa edersin. Parçaları sen yazmazsın, hazırdır (model, dosya okuyucu, vektör veritabanı...).

## 2. Ne zaman kullanılır, ne zaman kullanılmaz?

| Kullan | Kullanma |
|---|---|
| Bir AI fikrini kod yazmadan dakikalar içinde denemek (prototip) | Basit "soru sor, cevap al" (doğrudan LiteLLM'i çağırmak daha sade) |
| Karmaşık akış: çok adım, doküman arama (RAG), araç kullanan agent | Yetki, ödeme, kayıt gibi **iş mantığı** (bunlar .NET'te kalır) |
| Prompt'u/akışı programı yeniden derlemeden değiştirmek | Çok sıkı performans/güvenlik gereken kritik yol |
| Teknik olmayan biri akışı görsün/değiştirsin | |

**Bizde:** Normal sohbet LangFlow'suz çalışır (`sohbet` modu: `SohbetServisi` → LiteLLM). LangFlow'u **sadece doküman modu** için kullandık: proje dokümanlarını okuyup soruyu onlara dayanarak cevaplayan bir akış. Yani "karmaşıklaşan akış" için değer kattığı yerde kullandık.

## 3. Temel kavramlar

| Kavram | Anlamı | Bizdeki örnek |
|---|---|---|
| **Bileşen (component)** | Tek işlik hazır kutu | `Directory`, `Parser`, `Prompt Template`, `OpenAI`, `Chat Input` |
| **Giriş/çıkış (port)** | Bileşenin alıp verdiği veri noktaları | Directory'nin çıkışı `dataframe`, Parser'ın girişi `input_data` |
| **Bağlantı (edge)** | Bir çıkışı bir girişe bağlayan çizgi | `Directory.dataframe → Parser.input_data` |
| **Akış (flow)** | Bağlı bileşenler bütünü | `Dokuman Asistani` |
| **Veri türleri** | Portların konuştuğu tür: `Message` (metin), `Data`, `DataFrame`/`Table` (tablo) | Directory `Table` verir; Parser `Message` verir |
| **Playground** | Arayüzde akışı sohbetle denemek | (UI'da) |
| **Global değişken** | Akışların dışında saklanan, adıyla çağrılan değer (şifreli "credential" olabilir) | `LITELLM_KEY` |
| **API anahtarı** | Akışı dışarıdan çağıran programın kimliği | `.env` → `LANGFLOW_API_KEY` |
| **Session** | Aynı sohbete ait çağrıları bağlayan kimlik | `session_id` = bizim `oturumId` |

## 4. Bizim akışımız: 6 kutu

```
Chat Input ─────────────────────────────────┐ (question)
                                             ▼
Directory ──► Parser ──► Prompt Template ──► OpenAI ──► Chat Output
(/projedocs)  (birleştir)  ({context},{question})  (LiteLLM'e bağlı)
```

| Kutu | Ayar (gerçek değer) | Ne yapar |
|---|---|---|
| **Chat Input** | (yok) | Kullanıcının sorusunu akışa sokar. API çağrısındaki `input_value` buraya düşer |
| **Directory** | `path = /projedocs`, `types = ["md"]` | Container içindeki `/projedocs` klasöründeki `.md` dosyalarını okur. O klasör bizim `docs/` klasörümüz: `docker-compose.yml`'daki `./docs:/projedocs:ro` ile **salt-okunur** bağlandı |
| **Parser** | `pattern = "=== Dosya: {file_path} ===\n{text}\n"` | Her dosyayı "dosya adı + metin" olarak tek bir uzun metne birleştirir. Dosya adını ekledik ki model "kaynak" gösterebilsin |
| **Prompt Template** | Şablonda `{context}` (Parser çıktısı) ve `{question}` (Chat Input) | Modele giden asıl mesajı kurar: "SADECE aşağıdaki dokümanlara dayan, yoksa 'Bu bilgi dokümanlarda yok.' de" + dokümanlar + soru |
| **OpenAI** | `openai_api_base = http://litellm:4000/v1`, `model_name = akilli`, `api_key = LITELLM_KEY` (değişken **adı**) | Modeli çağırır. Gerçekte OpenAI değil, LiteLLM'e konuşur (aşağıda) |
| **Chat Output** | (yok) | Cevabı akışın sonucu olarak döndürür |

**Prompt'taki kural önemli:** Modele "yoksa uydurma, 'yok' de" demezsen model boşlukları kendi bilgisiyle doldurur (halüsinasyon). Gerçek denemede dokümanda olmayan bir soruyu sorduk ("2027 pazarlama bütçesi") ve cevap tam olarak **"Bu bilgi dokümanlarda yok."** geldi.

## 5. "OpenAI" bileşeni ama Claude cevap veriyor, nasıl?

LiteLLM **OpenAI'nin konuştuğu dili** konuşur (`/v1/chat/completions`). Bu yüzden LangFlow'daki OpenAI bileşeninin sadece **adresini** LiteLLM'e çevirdik:

```
LangFlow ──(OpenAI formatı)──► LiteLLM ──(Anthropic formatı)──► Claude
```

Faydası: LangFlow'a hiçbir Claude'a özel şey öğretmedik. Yarın modeli değiştirmek `litellm/config.yaml`'daki tek satır. Maliyet, günlük ve yedek model (fallback) kuralları da tek yerde kalır.

**Yaşadığımız ilgili hata (`drop_params`):** LangFlow'un OpenAI bileşeni isteğe `seed` ve `temperature` gibi OpenAI'ye özgü parametreler ekler; Claude bazılarını desteklemez. LiteLLM şu hatayı verdi: *"anthropic does not support parameters: ['temperature', 'seed']"*. Çözüm `litellm/config.yaml`'a:

```yaml
litellm_settings:
  drop_params: true   # desteklenmeyen parametreleri hata vermek yerine sessizce at
```

Bu, LiteLLM'in "elektrik adaptörü" görevinin canlı örneği: iki tarafın dili tam uyuşmuyor, aradaki katman uyduruyor.

## 6. Akışı dışarıdan çağırmak (REST)

Her akış bir adres olur. Bizim akışın adı (`endpoint_name`) `dokuman-asistani`:

```http
POST http://localhost:7860/api/v1/run/dokuman-asistani?stream=false
x-api-key: <LANGFLOW_API_KEY>
Content-Type: application/json

{ "input_value": "Worker nedir?", "input_type": "chat", "output_type": "chat", "session_id": "<oturumId>" }
```

Yanıt (kısaltılmış) ve cevabın bulunduğu yol:

```json
{ "outputs": [ { "outputs": [ { "results": { "message": { "text": "Cevap burada..." } } } ] } ] }
```
`outputs[0].outputs[0].results.message.text`

**.NET tarafı:** `LangFlowIstemcisi.CalistirAsync` bunu yapar. Dikkat edilecek iki nokta:
1. **Yanıt biçimi LangFlow sürümüne göre değişebilir.** O yüzden yolu "sessizce boş dön" yerine, bulunamazsa *"yanıtı beklenen biçimde değil"* diyen açık bir hatayla okuruz. Bunu 3 farklı bozuk yanıtla ve boş metinle test ettik (`DokumanVeLangFlowTests`).
2. HTTP 5xx gelince `HttpRequestException` fırlar. Worker bunu **yeniden dener** (RabbitMQ dersindeki retry). LangFlow çökükse geçici, kalıcı değilse sonunda `Hata`.

**Neden `x-api-key`?** LangFlow'ta `LANGFLOW_AUTO_LOGIN=false` yaptık: girişsiz erişim kapalı. Programların akışı çağırması için LangFlow'un kendi API anahtarı gerekir.

## 7. Anahtarlar: akış dosyasına gömme!

OpenAI bileşeninin `api_key` alanı iki biçimde çalışabilir: (a) anahtarı doğrudan yazarsın, (b) bir **global değişkenin adını** yazarsın ve `load_from_db: true` yaparsın. Biz (b)'yi seçtik: `LITELLM_KEY` adlı "credential" değişkeni LangFlow'un şifreli deposunda durur, akışta sadece **adı** yazılıdır.

**Neden?** Akışı JSON olarak export edip Git'e koyarız. (a) olsaydı anahtar GitHub'a giderdi. Bunu **otomatik taradık**: export dosyasında `.env`'deki hiçbir gizli değerin geçmediğini doğruladık ve `api_key` alanının değerinin sadece `'LITELLM_KEY'` olduğunu gördük.

`LANGFLOW_SECRET_KEY` (compose'da): LangFlow'un kayıtlı gizli değerleri şifrelediği anahtardır. **Değişirse eski şifreli değerler okunamaz.**

## 8. Kalıcılık: veri kaybı hikâyesi (gerçek hata)

LangFlow akışları, değişkenleri ve API anahtarlarını **kendi veritabanında** (SQLite) tutar. Rehber de uyarıyor: "container silinirse kaybolmaması için volume bağla". Biz `langflow_data` volume'ünü `/app/langflow`'a bağladık, **ama akışlar yine kayboldu**.

**Neden?** LangFlow veritabanını varsayılan olarak *kütüphane klasörünün içine* yazıyor (`.../site-packages/langflow/langflow.db`), bizim bağladığımız `/app/langflow` klasörüne değil. Container yeniden oluşunca (`docker compose up -d` ayarı değiştiğinde yapar) akış, değişken ve API anahtarı gitti. Ayrıca bağladığımız volume'de sadece `secret_key` ve bir klasör kalmıştı; bunu `ls` ile gördük.

**Çözüm:** `docker-compose.yml`'a `LANGFLOW_SAVE_DB_IN_CONFIG_DIR: "true"`. **Kanıt:** container'ı yeniden oluşturduk, akış ve `LITELLM_KEY` değişkeni yerinde durdu.

**İkinci güvence: akış kod gibi Git'te.** `langflow/dokuman-asistani.json` akışın tam tarifidir. Silindiyse ya da yeni kurulumdaysa:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\langflow-akisi-yukle.ps1
```
Script giriş yapar, `LITELLM_KEY` değişkenini oluşturur (yoksa), eski akışı silip dosyadakini yükler. **Test ettik:** akışı ve değişkeni sildik, script'le geri yükledik, .NET üzerinden gelen doküman sorusu doğru cevaplandı.

## 9. Üç güvenlik katmanı (gerçek hatalar)

LangFlow "akışlar kullanıcı tarafından değiştirilebilir ve bir akış sunucuda kod çalıştırabilir" varsayımıyla güvenli gelir. Bizim akışın çalışması için üç koruma ile karşılaştık, hepsi **bilerek** var:

| Hata mesajı | Koruma | Ne yaptık | Üretimde |
|---|---|---|---|
| *"Directory path escapes the allowed root."* | Directory bileşeni sadece **izinli klasörleri** okuyabilir | `LANGFLOW_DIRECTORY_COMPONENT_ALLOWED_ROOTS: /projedocs` | Aynı: sadece gereken klasör |
| *"Access to local file paths ... is disabled (LANGFLOW_RESTRICT_LOCAL_FILE_ACCESS=true)"* | Akışlar sunucudaki **yerel dosyaları** hiç okuyamaz, sadece yüklenen dosyaları | `LANGFLOW_RESTRICT_LOCAL_FILE_ACCESS: "false"` (yerel geliştirme) | **`true` bırak**, dokümanları LangFlow'a yükle |
| *"SSRF Protection: Hostname litellm resolves to blocked IP"* | Akış **iç ağdaki** (özel IP) adreslere istek atamaz (SSRF: sunucuyu saldırganın yerine iç ağda gezdirme) | `LANGFLOW_SSRF_ALLOWED_HOSTS: litellm` (sadece bu ad) | Aynı: sadece bilerek bağlanılan servisler |

**Küçük ama önemli bir tuzak:** İlk denemede `ALLOWED_ROOTS`'u `'["/projedocs"]'` (JSON listesi) yazdım ve LangFlow bunu **metin olarak** okudu: ayar `['["/projedocs"]']` oldu (içinde JSON metni olan tek bir klasör adı). Container içinde `python` ile ayarın gerçek değerini yazdırarak buldum. Düz `/projedocs` yazınca düzeldi. Ders: bir ayar çalışmıyorsa **uygulamanın gerçekte hangi değeri okuduğuna** bak, ne yazdığına değil.

## 10. Bu "RAG" mı? Dürüst cevap: yarısı

**RAG (Retrieval-Augmented Generation)**, "soruyla ilgili parçaları bul, modele ver, cevap ürettir" demektir. Gerçek RAG'in adımları:
1. Dokümanı küçük parçalara (chunk) böl.
2. Her parçayı **embedding** modeliyle sayı dizisine (vektör) çevir.
3. Vektörleri bir **vektör veritabanında** sakla.
4. Soru gelince soruyu da vektöre çevir, **en benzer parçaları** bul.
5. Sadece onları modele ver.

**Bizim akışımız** bunun yerine **tüm dokümanı** her soruda modele veriyor ("context stuffing", bağlam doldurma). Çalışır çünkü dokümanlarımız küçük, ama bedeli var. Ölçtük:

| Ölçüm | Değer |
|---|---|
| Doküman boyutu | ~112 000 karakter, ~13 400 kelime |
| Her soruda modele giden | **yaklaşık 34 000 token** (soru ne kadar kısa olursa olsun) |
| Cevap süresi | 3-10 sn (sohbet modunda 2-5 sn) |

Her soruda 34 bin token **giriş** ücretlendirilir. Dokümanlar büyüdükçe maliyet ve süre artar, sınırı da aşar (modelin bağlam penceresi). Gerçek RAG'de modele sadece 3-5 ilgili parça (birkaç bin token) gider.

**Neden gerçek RAG yapmadık?** Embedding modeli gerekir. Anthropic'in embedding modeli yok, yani ayrı bir sağlayıcı (OpenAI, yerel Ollama, HuggingFace...) gerekirdi. Elimizdeki tek anahtar Anthropic. LangFlow'da "Vector Store RAG" hazır başlangıç akışı var; bu akışı oluşturan adımlar yukarıdaki beşlidir. **Ne zaman geçilir?** Dokümanlar bağlam penceresini zorlamaya başlayınca ya da soru başına maliyet can sıkınca.

## 11. Hata ayıklama: nereden başlanır?

| Belirti | Nereye bak |
|---|---|
| Akış çağrısı HTTP 500, mesajda `Error building Component X` | **X** bileşeninin ayarı. Mesajın gerisi nedeni söyler (yukarıdaki tablolar) |
| HTTP 403 `Invalid or missing API key` | `x-api-key` yanlış/boş. Container yeniden oluşunca veritabanı sıfırlandıysa anahtar da silinmiş olur (bkz. 8) |
| Akış var ama sonuç boş/yanlış | LangFlow arayüzünde (http://localhost:7860) **Playground**'da aynı soruyu dene; her bileşenin çıktısını görebilirsin |
| LangFlow'a ulaşılamıyor | `docker compose ps`, `docker compose logs langflow --tail 50` |
| Değişiklik görünmüyor | Compose ayarı değiştiyse `docker compose up -d langflow` (restart yetmez, ortam değişkenleri container yaratılırken okunur) |

## 12. İş bölümü: LangFlow ne yapar, .NET ne yapar?

- **LangFlow:** AI akışı (hangi doküman, hangi prompt, hangi model).
- **.NET:** her şeyin etrafı: kimlik, kuyruk, yeniden deneme, kayıt, hata yönetimi, takip fişi.

Akışı çağıran `DokumanServisi` ve `AiIstegiConsumer` aynen normal sohbetle aynı kuyruk ve takip fişi mekanizmasını kullanır; fark sadece `mod` alanı (`sohbet` | `dokuman`). Yani LangFlow, sistemin "cevabı kim üretir?" sorusuna **bir seçenek** olarak takıldı. Bu, ileride akışı başka bir akışla değiştirmenin kolay olduğu anlamına gelir (sadece LangFlow'da, .NET'e dokunmadan).

## 13. Bilmen gerekenlerin özeti

1. LangFlow = görsel akış kurucu; her akış bir REST adresi olur.
2. Akış = bileşenler + bağlantılar. Portların **türleri** uyuşmalı (`Message`, `Data`, `Table`).
3. Bizde: `Chat Input + Directory → Parser → Prompt → OpenAI(LiteLLM) → Chat Output`.
4. OpenAI bileşeni LiteLLM'e bakıyor: LangFlow Claude'u bilmez, LiteLLM çevirir (`drop_params`).
5. Anahtarı akışa **gömme**, global değişken **adını** kullan; export'u sır için tara.
6. Veritabanını **volume'e** taşı (`SAVE_DB_IN_CONFIG_DIR`) ve akışı **Git'te** tut.
7. Üç güvenlik katmanı (izinli klasör, yerel dosya, SSRF) bilerek var; yerelde gevşettik, üretimde sıkı tut.
8. Doküman asistanımız gerçek RAG değil, "bağlam doldurma": küçük dokümanda iyi, büyüyünce pahalı. Çözümü embedding + vektör arama.
9. Akış yanıtı biçimi sürüme göre değişebilir: açık hata veren bir okuyucu yaz ve test et.
10. İş mantığı .NET'te kalır, LangFlow sadece AI akışını yönetir.

## 14. Bu derste projede bakacakların

| Ne | Nerede |
|---|---|
| Servis ve güvenlik ayarları | `docker-compose.yml` → `langflow` |
| Akışın tarifi | `langflow/dokuman-asistani.json` |
| Yükleme betiği | `scripts/langflow-akisi-yukle.ps1` |
| .NET istemcisi | `src/Asistan.Infrastructure/LangFlow/LangFlowIstemcisi.cs` |
| Mod ve iş mantığı | `src/Asistan.Application/Dokuman/DokumanServisi.cs`, `Kuyruk/KuyrukSozlesmeleri.cs` (`SohbetModlari`) |
| Testler | `tests/Asistan.Tests/DokumanVeLangFlowTests.cs` |
| Deneme | `POST /api/sohbet` gövdesine `"mod": "dokuman"` ekle |
