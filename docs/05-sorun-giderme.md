# 05 · Sorun giderme

Önce genel yöntem, sonra **bu projede gerçekten yaşadığımız** hatalar.

## Genel yöntem (3 adım)
1. **Hata mesajını oku.** Çoğu zaman cevabı içinde yazar. Son satırdan değil, `error` geçen satırdan başla.
2. **Hangi katmanda?** Derleme hatası mı (kod), çalışma hatası mı (API), altyapı mı (Docker/LiteLLM)?
3. **Zinciri kısalt.** API → LiteLLM → Anthropic zincirinde, LiteLLM'e doğrudan istek atıp sorunun hangi halkada olduğunu bul.

```
Sorun hangi halkada?
  Derleme   ──► dotnet build
  Testler   ──► dotnet test
  Docker    ──► docker compose ps  /  docker compose logs <servis>
  LiteLLM   ──► LiteLLM'e doğrudan istek (bkz. 04-komut-rehberi.md)
  API       ──► Scalar (/scalar/v1) veya Asistan.Api.http
```

---

## Yaşadığımız hatalar

### 1. `Run "dotnet tool restore" to make the "dotnet-ef" command available`
**Neden:** `dotnet-ef` aracı bu bilgisayarda global kurulu değildi.
**Çözüm:** Projeye özel olarak kuruldu (`.config/dotnet-tools.json`). Yeni bir yerde: `dotnet tool restore`.

### 2. `Package 'Scalar.AspNetCore' is incompatible with 'all' frameworks`
**Neden:** En yeni Scalar sürümü .NET 11 istiyor, biz .NET 9'dayız.
**Çözüm:** Eski sürüm sabitlendi: `Scalar.AspNetCore` **2.12.\***.
**Ders:** Paket sürümleri projenin .NET sürümüyle uyumlu olmalı. `*` kullanırsan dikkat, yeni sürüm gelince kırılabilir.

### 3. `'IServiceCollection' does not contain a definition for 'AddHttpClient'`
**Neden:** `AddHttpClient` `Microsoft.Extensions.Http` paketindedir. ASP.NET projeleri bunu hazır getirir, **class library projeleri getirmez**.
**Çözüm:** Paket Infrastructure'a eklendi.

### 4. `Missing required parameter: 'messages'` (curl ile JSON gönderirken)
**Neden:** Git Bash/Windows'ta komut satırına yazılan JSON'daki tırnaklar ve Türkçe karakterler bozulabiliyor.
**Çözüm:** JSON'u dosyaya yazıp `-d @dosya.json` ile gönder, veya PowerShell'de `Invoke-RestMethod` kullan. Kolayı: Scalar / `.http` dosyası.

### 5. `.env` değişti ama LiteLLM yeni değeri görmüyor (`Missing Anthropic API Key`)
**Neden 1:** Değer dosyaya gerçekten yazılmamıştı (editörde kaydedilmemiş ya da başka dosya düzenlenmiş). Kontrol (değeri göstermeden):
```powershell
(Select-String -Path .env -Pattern '^ANTHROPIC_API_KEY=(.*)$').Matches[0].Groups[1].Value.Length
```
0 ise değer boş. Dosyanın değişiklik saatine de bak: `(Get-Item .env).LastWriteTime`.
**Neden 2:** Ortam değişkenleri container **yaratılırken** okunur. Çözüm: `docker compose up -d --force-recreate litellm` (`restart` yetmez).

### 6. Anahtarın sonunda garip bir karakter (`ü`)
**Neden:** Klavyeden fazladan bir tuş kaçtı. Anahtarlar sadece ASCII karakter içerir.
**Çözüm:** Yazdırılamayan/ASCII dışı karakterleri temizle. Anahtarı elle yazmak yerine konsoldan kopyala-yapıştır.

### 7. `Your credit balance is too low to access the Anthropic API`
**Neden:** Anthropic API'si ücretlidir ve Claude.ai aboneliğinden **ayrıdır**. Anahtar geçerli (kimlik doğrulama geçti) ama hesapta kredi yok.
**Çözüm:** Anthropic konsolu → **Plans & Billing** → küçük bir kredi yükle. Harcama limiti koy.
**Not:** Bu hata aslında iyi haber: kimlik doğrulama geçti, zincir çalışıyordu. Kredi yüklenince sorun kalmadı (yaşadığımız çözüm buydu).

### 8. Anahtarı sohbete yapıştırmak
**Neden:** Gizli anahtar bir konuşma kaydına girdi.
**Çözüm:** İşin sonunda konsoldan o anahtarı **sil**, yenisini üret. Bundan sonra anahtarı sadece `.env`'ye yaz.

### 9. `warning: LF will be replaced by CRLF`
**Anlamı:** Windows satır sonu dönüşümü hakkında bilgi. **Zararsız**, yok sayılabilir.

### 10. MongoDB'de `countDocuments` 0 döndü ama kayıtlar orada
**Neden:** MongoDB alan adları **büyük/küçük harf duyarlıdır**. Kod önce `OturumId` (PascalCase) yazdı, sorgu `oturumId` arıyordu. Driver varsayılan olarak C# özellik adını aynen kullanır.
**Çözüm:** `CamelCaseElementNameConvention` ile alanlar `oturumId` biçimine çevrildi (`MongoSinifHaritalari`). Eski biçimdeki deneme kayıtları silindi (`db.mesajlar.drop()`).
**Ders:** Alan adı biçimini (camelCase) projenin başında seç ve kod/convention ile sabitle; sonradan değiştirirsen eski belgeler eşleşmez.

### 11. Aynı adlı namespace ve sınıf (`Hafiza`)
**Belirti:** `Hafiza` hem `Asistan.Application.Hafiza` (namespace) hem `Asistan.Domain.Entities.Hafiza` (sınıf) olunca kodda `Hafiza` yazınca hangisi kastedildiği belirsiz; sınıfa `Domain.Entities.Hafiza` diye uzun yazmak gerekti.
**Çözüm:** Namespace `Asistan.Application.Bellek` olarak yeniden adlandırıldı.
**Ders:** Namespace adı ile içindeki/yanındaki bir sınıf adı aynı olmasın.

### 12. Testte `SetupSequence(...).Returns(delege)` derlenmiyor
**Belirti:** `Cannot convert lambda expression to type 'Task<string>'`. Moq'un `SetupSequence`'i sadece sabit değer kabul eder, delege kabul etmez.
**Çözüm:** Normal `Setup(...).Returns((args) => ...)` kullanıp sayaçla sırayla cevap veren ve gelen istekleri bir listeye kaydeden küçük bir yardımcı yazıldı (`HafizaServisiTests.LlmKaydet`).

### 13. `ArgumentNullException: Value cannot be null. (Parameter 'connectionString')` (MongoClient satırında)
**Belirti:** Uygulama açılırken `new MongoClient(config["Mongo:Url"])` satırında çöker.
**Neden:** `Mongo:Url` (ve `ConnectionStrings:Sql`, `LiteLlm:Key`) Git'te **yoktur**; geliştirmede `user-secrets` kasasından okunur. Kasa ise **sadece `Development` ortamında yüklenir**. Uygulama Production ortamında (ya da ayarlar hiç yoksa) başlarsa değer `null` gelir ve MongoDB sürücüsü anlamsız bir hata fırlatır. Bunu bilerek Production'da başlatıp birebir yeniden ürettik.
**Çözüm (en sık):** Visual Studio'da çalıştır düğmesinin yanındaki oktan **`http` veya `https`** profilini seç (IIS Express veya başka bir profil değil). Bu profiller `ASPNETCORE_ENVIRONMENT=Development` ayarlar. Komut satırında: `dotnet run --project src/Asistan.Api --launch-profile http` (`--no-launch-profile` **kullanma**: ortam Production kalır).
**Kasada gerçekten yoksa** (yeni bilgisayar, silinmiş): `dotnet user-secrets list -p src/Asistan.Api` ile bak (değerler görünür, ekranı paylaşma). Eksikleri `dotnet user-secrets set "Mongo:Url" "<değer>" -p src/Asistan.Api` ile ekle (değerler `.env`'den; bkz. `04-komut-rehberi.md`).
**Sunucuda / Docker'da:** Kasa kullanılmaz, **ortam değişkeni** ver: `:` yerine `__` yaz (`Mongo__Url`, `LiteLlm__Key`, `ConnectionStrings__Sql`).
**Koddaki iyileştirme:** `AddInfrastructure` artık başlarken zorunlu ayarları kontrol eder ve hangisinin eksik olduğunu, hangi ortamda çalıştığını ve nasıl düzeltileceğini yazan açık bir hata verir. Testi: `AyarDogrulamaTests`.
**Ders:** "Bende çalışıyor, sende çalışmıyor" sorunlarının çoğu, Git'e girmeyen ayarlardır (`.env`, `user-secrets`). Hata mesajı bunu söylemiyorsa, hata mesajını iyileştir.

### 14. Ortam `Development` ama kasa (user-secrets) boş geliyor
**Belirti:** `Eksik ayar(lar): ConnectionStrings:Sql, Mongo:Url, LiteLlm:Key. Çalışma ortamı: Development.` Yani ortam doğru, kasa kaynağı yüklü, ama değerler gelmiyor. `dotnet user-secrets list` ve komut satırında `dotnet run` ise çalışıyor.
**Neden:** .NET kasa dosyasını Windows'un "bilinen AppData klasöründen" **değil, `APPDATA` ORTAM DEĞİŞKENİNDEN** bulur (yoksa `HOME`'dan). Süreç farklı veya eksik bir `APPDATA` ile başlarsa dosya diskte dururken .NET onu "yok" görür. Bunu bilerek sahte bir `APPDATA` vererek birebir yeniden ürettik.
**Teşhis:** Hata mesajının altındaki **"Tanı bilgisi"** bölümü iki yeri yan yana gösterir:
```
APPDATA ortam değişkeni    : ...            <- .NET kasayı BURADAN arar
Kasa (.NET'in baktığı yer) : ...\secrets.json (YOK)
Kasa (Windows klasöründe)  : ...\secrets.json (VAR)
```
"baktığı yer: YOK" ama "Windows klasöründe: VAR" ise sorun budur. (Kasa dosyası hiç yoksa ikisi de YOK görünür.)
**Çözüm (hemen):** `src/Asistan.Api/appsettings.Local.json` yedek dosyası. Development'ta, varsa, otomatik okunur; `.gitignore`'da olduğu için Git'e girmez. İçeriği:
```json
{
  "ConnectionStrings": { "Sql": "<değer>" },
  "Mongo": { "Url": "<değer>" },
  "LiteLlm": { "Key": "<değer>" }
}
```
Değerler `.env` ve user-secrets'taki ile aynıdır. **Bu dosya gizli bilgi içerir:** paylaşma, ekran görüntüsünde gösterme.
**Kök neden:** Visual Studio sürecinin `APPDATA` değeri bu makinede doğrulanmadı. Tanı bilgisi bunu gösterir. Kök nedeni bulmadan da yedek dosya yeterlidir.
**Ders:** Aynı kod iki farklı başlatma yolunda farklı davranıyorsa fark, kodda değil **sürecin ortamındadır** (ortam değişkenleri, kullanıcı, çalışma klasörü). Hata mesajına ortamı yazdırmak teşhisi dakikalara indirir.

### 15. RabbitMQ kapalıyken API 70+ saniye asılı kalıyor, iş sonsuza dek `Bekliyor`
**Belirti:** RabbitMQ durmuşken `POST /api/sohbet` cevap vermiyor (istemci zaman aşımına uğruyor). Takip fişi `Bekliyor` kalıyor ve hiç işlenmiyor.
**Neden:** Kuyruk istemcisi (MassTransit) broker'a bağlanamayınca çok uzun bekler. Fiş zaten yazılmıştı ama hiç kuyruğa gidemedi ve kimse onu `Hata` yapmadı: sahipsiz kayıt.
**Çözüm:** `IsServisi` gönderime **5 sn sınır** koyar (`WaitAsync`), başarısız olursa fişi `Hata` yapar, API **503** döner. İki test eklendi. RabbitMQ geri gelince API ve Worker kendiliğinden yeniden bağlanır.
**Ders:** "Dış sisteme giden her çağrının bir zaman sınırı olmalı." Canlıda kapatıp denemeden bu kusur görünmezdi (unit testler geçiyordu).

### 16. Panelde/sayaçlarda sayı "yanlış" görünüyor (örn. mesaj gönderdim ama Ready 0)
**Neden:** RabbitMQ yönetim paneli ve API'si istatistikleri **birkaç saniyede bir** yeniler. Hemen baktığında eski değeri görürsün (Aşama 9 testinde kuyruk önce `HAZIR 0`, 6 sn sonra `HAZIR 1` gösterdi).
**Çözüm:** Sayfayı yenile / 5-10 sn bekle ya da sunucudan sor: `docker compose exec rabbitmq rabbitmqctl list_queues name messages_ready consumers`.

### 17. Sorular `Bekliyor` kalıyor, cevap gelmiyor
**Neden (çoğu zaman):** **Worker çalışmıyor.** API sadece soruyu kuyruğa bırakır; cevabı Worker üretir.
**Çözüm:** `dotnet run --project src/Asistan.Worker` (ayrı bir terminal) ya da Visual Studio'da *Multiple startup projects*. Kuyrukta `tüketici` sayısı 0 ise Worker kapalıdır (`rabbitmqctl list_queues`). Worker açılınca bekleyen sorular kendiliğinden işlenir, hiçbiri kaybolmaz.

### 18. LangFlow'da akışlar ve değişkenler container yenilenince kayboldu
**Belirti:** `docker compose up -d langflow` sonrası akış listesi boş, `LITELLM_KEY` yok, API anahtarı geçersiz (403).
**Neden:** LangFlow veritabanını volume'e bağladığımız `/app/langflow`'a değil, kütüphane klasörüne yazıyordu.
**Çözüm:** `LANGFLOW_SAVE_DB_IN_CONFIG_DIR: "true"`. Kalıcılık yeniden oluşturarak **kanıtlandı**. Akışın kopyası Git'te (`langflow/`), `scripts/langflow-akisi-yukle.ps1` ile yüklenir. Ayrıntı: `dersler/01-langflow.md` bölüm 8.

### 19. `Directory path escapes the allowed root.`
**Neden:** Directory bileşeni yalnızca izinli klasörleri okur. Ayarı JSON listesi (`'["/projedocs"]'`) yazınca LangFlow onu **metin** olarak okudu (`['["/projedocs"]']`).
**Çözüm:** `LANGFLOW_DIRECTORY_COMPONENT_ALLOWED_ROOTS: /projedocs` (düz yazı). **Teşhis:** ayarın gerçekte okunan değerini container içinde Python ile yazdır.

### 20. `Access to local file paths ... is disabled (LANGFLOW_RESTRICT_LOCAL_FILE_ACCESS=true)`
**Neden:** Varsayılan olarak akışlar sunucudaki yerel dosyaları okuyamaz.
**Çözüm (yerel geliştirme):** `LANGFLOW_RESTRICT_LOCAL_FILE_ACCESS: "false"` (Directory yine sadece `/projedocs`'a kısıtlı). **Üretimde `true` bırak**, dokümanları LangFlow'a yükle.

### 21. `SSRF Protection: Hostname litellm resolves to blocked IP address(es)`
**Neden:** LangFlow, akışların iç ağdaki (özel IP) adreslere istek atmasını engeller (SSRF koruması).
**Çözüm:** `LANGFLOW_SSRF_ALLOWED_HOSTS: litellm` (sadece bilerek bağlanılan servis).

### 22. `anthropic does not support parameters: ['temperature', 'seed']` (LiteLLM)
**Neden:** LangFlow'un OpenAI bileşeni Claude'un desteklemediği parametreler gönderir.
**Çözüm:** `litellm/config.yaml` → `litellm_settings: drop_params: true`, sonra `docker compose restart litellm`.

### 23. LangFlow `403 Invalid or missing API key`
**Neden (çoğu zaman):** Veritabanı sıfırlandı (bkz. #18), eski API anahtarı artık yok. `.env`'deki `LANGFLOW_API_KEY` geçersiz.
**Çözüm:** `.env`'de `LANGFLOW_API_KEY=` satırını boşalt, `scripts\langflow-akisi-yukle.ps1` çalıştır (yenisini üretir), sonra `dotnet user-secrets set LangFlow:ApiKey <değer> -p src/Asistan.Api` ve `appsettings.Local.json`'ı güncelle.

---

## Olası ama henüz yaşanmamış hatalar

| Belirti | Olası neden | Çözüm |
|---|---|---|
| `Unable to resolve service for type '...'` | Servis DI'a kaydedilmemiş | `Program.cs` veya `DependencyInjection.cs`'e kayıt ekle |
| Açılışta `Eksik ayar(lar): ...` hatası | Ortam Development değil veya kasa boş | Ortam Development değilse #13, Development ama değerler boşsa #14 (yedek yol: `appsettings.Local.json`); mesajdaki "Tanı bilgisi"ne bak |
| `Connection refused` (5432/27017/5672) | Docker servisi kapalı | `docker compose up -d`, `docker compose ps` |
| `password authentication failed` (Postgres) | user-secrets'taki şifre `.env`'dekiyle uyuşmuyor | İkisini eşitle. Not: volume varsa Postgres **ilk açılıştaki** şifreyi tutar, `.env`'yi sonradan değiştirmek yetmez (veriyi silmek istemezsen şifreyi Postgres içinden değiştir; test verisi önemsizse `docker compose down -v`) |
| `502` yanıtı `/api/sohbet`'ten | LiteLLM kapalı/yanlış anahtar/kredi yok | `docker compose logs litellm`, LiteLLM'e doğrudan istek |
| LiteLLM `401` | `LiteLlm:Key` ile `LITELLM_MASTER_KEY` farklı | İkisini eşitle, API'yi yeniden başlat |
| `Authentication failed` (MongoDB) | Bağlantı cümlesinde `?authSource=admin` yok veya şifre `.env` ile uyuşmuyor | `Mongo:Url`'i user-secrets'ta kontrol et; kullanıcı `admin` veritabanında tanımlı |
| `/api/sohbet` hata verir, MongoDB loglarında uyarı | MongoDB kapalı | `docker compose ps`; index servisi uyarıyı loglar ama API düşmez |
| Mesaj geçmişi boş geliyor | Yanlış `oturumId` ya da LLM hata verdiği için kayıt yapılmadı | `oturumId`'yi cevaptan aynen al; `docker compose logs litellm` |
| Asistan beni hatırlamıyor (hafıza boş) | Hafıza çıkarma hata verdi (örn. `hizli` modeli kredi/ad sorunu) veya model "YOK" dedi | API loglarında `Hafıza çıkarılamadı` uyarısına bak; `GET /api/hafiza/{kullaniciId}` ile içeriği kontrol et; LiteLLM'e `"model":"hizli"` ile doğrudan istek at |
| Özet oluşmuyor | Henüz eşik aşılmadı (varsayılan 20 özetlenmemiş mesaj) | Denemek için eşiği küçült (`04-komut-rehberi.md`, "Hafıza ayarlarını geçici değiştirmek") |
| `hizli` model hatası | `config.yaml`'daki `claude-haiku-5-5` adı hesabında yok | Model adını Anthropic dokümantasyonundan kontrol et, `docker compose restart litellm` |
| Fiş `Isleniyor` kalıyor, `denemeSayisi` artıyor | Worker model/ağ hatası alıyor ve yeniden deniyor | Worker logu, `docker compose ps` (litellm), LiteLLM'e doğrudan istek; ~2 dk sonra `Hata` olur |
| Fiş `Hata` | 5 deneme de başarısız (model kapalı vb.) | Worker logunda gerçek neden; mesaj `ai-istegi_error` kuyruğunda (panelden incelenebilir) |
| Worker açılışta `Eksik ayar(lar): RabbitMq:...` | Worker'ın kasası/yedek dosyası boş | #13/#14: Worker API ile **aynı kasayı** kullanır; yoksa `src/Asistan.Worker/appsettings.Local.json` |
| `PRECONDITION_FAILED` (RabbitMQ) | Aynı adlı kuyruk farklı ayarlarla önceden oluşturulmuş | Kuyruğu panelden sil, uygulamayı yeniden başlat |
| Doküman modunda `LangFlow:...` eksik ayar hatası | LangFlow adresi/anahtarı kasada yok | #13/#14; `LangFlow:ApiKey` kasada ve `appsettings.Local.json`'da olmalı |
| Doküman cevabı "yok" diyor ama dokümanda var | Bilgi `docs/` üst klasöründe değil ya da akışın Directory ayarı `types`/`path` yanlış | Akışı LangFlow arayüzünde Playground'da dene; `docs/dersler/` bilerek okunmaz |
| `Address already in use` (5254) | Önceki `dotnet run` hâlâ çalışıyor | Eski pencereyi kapat veya portu kullanan işlemi bul |
| `Database ... does not exist` / tablo yok | Migration uygulanmamış | `dotnet ef database update ...` |
| Migration `Build failed` | Kodda derleme hatası var | Önce `dotnet build` ile düzelt |
| CI kırmızı ama yerelde yeşil | Ortam farkı veya push edilmemiş dosya | Actions logunu oku; `git status` ile eksik dosya var mı bak |
| Test kırmızı | Kod ya da test bozuldu | Hangi testin, hangi satırda başarısız olduğunu oku; son değişikliği `git diff` ile incele |

## Hiçbir şey çalışmıyorsa sıfırlama (veri silinir!)

```powershell
docker compose down -v          # tüm container ve verileri siler (kullanıcı tablosu dahil)
docker compose up -d
dotnet ef database update -p src/Asistan.Infrastructure -s src/Asistan.Api   # tabloları yeniden kur
```
Bu, test verilerini (ör. `emre@test.com`) siler; önemli verin varsa yapma.
