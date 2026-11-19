# 01 · Proje haritası: hangi dosya nerede, ne işe yarar

Yol, `AiAsistan/` klasörüne göredir. Bu liste **Git'e giren her dosyayı** kapsar (migration'ın otomatik ürettikleri ve `bin/obj` hariç).

## Klasör ağacı

```
AiAsistan/
├─ AiAsistan.sln                  Solution: tüm projeleri toplayan çatı
├─ Directory.Build.props          Tüm projelere uygulanan ortak ayarlar
├─ CLAUDE.md                      Claude'a projeyi anlatan not
├─ README.md                      Projenin ön yüzü
├─ docker-compose.yml             Docker servisleri (postgres, mongo, rabbitmq, litellm)
├─ .env.example                   .env'nin şablonu (gerçek değer yok)
├─ .env                           GERÇEK sırlar (Git'e girmez!)
├─ .gitignore                     Git'in yok sayacağı şeyler
├─ .config/dotnet-tools.json      Projeye özel araç listesi (dotnet-ef)
├─ .github/workflows/ci.yml       GitHub Actions: otomatik build + test
├─ litellm/config.yaml            Hangi AI modeli kullanılacak
├─ docs/                          Bu rehber
├─ src/
│  ├─ Asistan.Domain/             Veri sınıfları
│  ├─ Asistan.Application/        İş kuralları + arayüzler
│  ├─ Asistan.Infrastructure/     Veritabanı + LiteLLM ile gerçek konuşma
│  ├─ Asistan.Api/                Web API (dışarıya açılan kapı)
│  └─ Asistan.Worker/             Arka plan işçisi (şimdilik boş iskelet)
└─ tests/Asistan.Tests/           Otomatik testler
```

## Bağımlılık okları (en önemli kural)

```
Domain ◄── Application ◄── Infrastructure ◄── Api
                 ▲                 ▲
                 └──── Tests       └── Worker (Application'ı da kullanır)
```

Ok "bilir" demek. **Domain hiçbir şeyi bilmez.** Application sadece Domain'i bilir. Bu kural `.csproj` içindeki `ProjectReference` satırlarıyla zorlanır: Application'a Infrastructure'ı eklemeye kalkarsan derleyici izin vermez.

| Proje | Referans verdiği |
|---|---|
| Domain | hiçbiri |
| Application | Domain |
| Infrastructure | Application, Domain |
| Api | Application, Infrastructure |
| Worker | Application, Infrastructure |
| Tests | Application, Domain |

---

## Kök klasördeki dosyalar

### `AiAsistan.sln`
Solution dosyası. Visual Studio'da bunu açarsın; içindeki 6 projeyi listeler. Kendin düzenlemezsin (`dotnet sln add` ile eklenir).

### `Directory.Build.props`
Kökteki bu dosya **altındaki tüm projelere** otomatik uygulanır:
- `TargetFramework = net9.0`: hepsi .NET 9.
- `Nullable = enable`: `string` ile `string?` farkını derleyici denetler (null hatalarını önler).
- `ImplicitUsings = enable`: `System`, `System.Linq` gibi using'leri her dosyaya yazmak zorunda kalmazsın.
- `TreatWarningsAsErrors = true`: **uyarılar hata sayılır**, kod baştan temiz kalır.

### `CLAUDE.md`
Claude Code her oturum başında bunu okur: mimari, komutlar, kurallar. Senin için de projenin kurallarının kısa özeti.

### `README.md`
Projenin kısa tanıtımı ve çalıştırma adımları.

### `docker-compose.yml`
4 servisi tarif eder. Ayrıntı aşağıda "Docker" bölümünde.

### `.env.example` ve `.env`
- `.env.example`: Hangi değişkenlerin gerektiğini gösteren şablon. **Git'e girer**, değerleri sahte.
- `.env`: Gerçek değerler. **Git'e girmez** (`.gitignore` 7. satır). Docker Compose bunu otomatik okur.

Değişkenler:

| Değişken | Ne için |
|---|---|
| `POSTGRES_USER`, `POSTGRES_PASSWORD` | Postgres kullanıcısı |
| `MONGO_USER`, `MONGO_PASSWORD` | MongoDB kullanıcısı |
| `RABBITMQ_USER`, `RABBITMQ_PASSWORD` | RabbitMQ kullanıcısı |
| `LITELLM_MASTER_KEY` | LiteLLM'in "ana anahtarı": API bununla LiteLLM'e girer |
| `ANTHROPIC_API_KEY` | Claude'a erişim anahtarı (Anthropic konsolundan) |

### `.gitignore`
Git'in takip etmeyeceği dosyalar: `bin/`, `obj/`, `.vs/`, `.env`, `coverage/` vb.

### `.config/dotnet-tools.json`
Projeye özel araç listesi. İçinde `dotnet-ef` (sürüm 9.0.23) var. Yeni bir bilgisayarda `dotnet tool restore` yazınca kurulur. CI da aynı sürümü kullanır.

---

## `litellm/config.yaml`
LiteLLM'in beyni. İki "takma ad" tanımlı:

| Takma ad | Gerçek model | Ne için |
|---|---|---|
| `akilli` | `anthropic/claude-sonnet-5-5` | Asıl cevaplar |
| `hizli` | `anthropic/claude-haiku-5-5` | Ucuz işler (Aşama 8'de özetleme) |

`api_key: os.environ/ANTHROPIC_API_KEY` = "anahtarı ortam değişkeninden oku" (Docker `.env`'den verir). `master_key` ise API'nin LiteLLM'e girişini korur.
**Model adı değiştirmek istersen sadece bu dosyayı düzenle**, kod değişmez. Sonra: `docker compose restart litellm`.

---

## `.github/workflows/ci.yml`
GitHub'ın makinesinde çalışan otomatik kontrol.
- **Ne zaman:** `main`'e her push'ta ve `main`'e açılan her Pull Request'te.
- **Nerede:** `ubuntu-latest` (GitHub'ın kiraladığı temiz bir Linux).
- **Adımlar:** kodu çek → .NET 9 kur → `dotnet restore` → `dotnet build -c Release` → `dotnet test` (coverage ile) → coverage raporunu yükle.
- Docker ve veritabanı **yok**: sadece unit testler koşar (onlar DB istemez).

---

## `src/Asistan.Domain` (hiçbir şeyi bilmeyen katman)

### `Entities/Kullanici.cs`
Kullanıcı sınıfı: `Id`, `Ad`, `Email`, `KayitTarihi` (UTC), `Abonelikler` (liste). Postgres'teki `Kullanicilar` tablosuna karşılık gelir.

### `Entities/Abonelik.cs`
Abonelik sınıfı: `Id`, `Plan`, `KullaniciId` (foreign key), `Kullanici` (navigation property). Bir kullanıcının birden çok aboneliği olabilir (1-N ilişki).

> Şu an Abonelik için hiç endpoint yok; tablo hazır ama doldurulmuyor.

### `Entities/Mesaj.cs`
Sohbet mesajı: `Id` (string; MongoDB'de ObjectId), `OturumId`, `KullaniciId`, `Rol` (`user` / `assistant`), `Icerik`, `Olusturulma` (UTC). MongoDB'deki `mesajlar` koleksiyonunun bir belgesi. Domain MongoDB'yi tanımaz: `Id`'nin ObjectId olması Infrastructure'daki `MongoSinifHaritalari`'nda ayarlanır.

---

## `src/Asistan.Application` (iş kuralları ve sözleşmeler)

### `Kullanicilar/IKullaniciDeposu.cs`
Arayüz: "kullanıcıyı getir, e-posta var mı, ekle". **Nasıl** yapıldığını bilmez. Gerçek hali Infrastructure'da.

### `Kullanicilar/KullaniciServisi.cs`
Bu dosyada üç şey var:
- `KullaniciOlusturIstegi`: gelen veri (Ad, Email) için küçük bir kayıt (record).
- `EmailZatenVarException`: aynı e-posta ikinci kez gelirse fırlatılan özel hata.
- `IKullaniciServisi` + `KullaniciServisi`: **iş kuralları**: ad boş olamaz, e-posta `@` içermeli, e-posta küçük harfe çevrilip boşlukları kırpılır, aynı e-posta iki kez kaydedilemez.

### `Sohbet/ILlmIstemcisi.cs`
- `LlmMesaj(Rol, Icerik)`: modele giden bir mesaj. `Rol`: `system` (modelin kuralları), `user` (kullanıcı), `assistant` (modelin önceki cevabı).
- `ILlmIstemcisi`: "modele sor" arayüzü. Claude'u, LiteLLM'i, HTTP'yi bilmez.

### `Sohbet/IMesajDeposu.cs`
Arayüz: `KaydetAsync(mesajlar)` ve `SonMesajlarAsync(oturumId, adet)` (eskiden yeniye). MongoDB'yi bilmez; gerçek hali `MongoMesajDeposu`.

### `Sohbet/SohbetServisi.cs`
`SohbetSonucu(Cevap, OturumId)` + `ISohbetServisi` + `SohbetServisi`:
- **`SorAsync(kullaniciId, soru, oturumId?)`:** boş soruyu reddeder; `oturumId` verilmediyse yeni bir tane üretir; sistem prompt'u + soruyu modele gönderir; **model cevap verdikten sonra** soru ve cevabı `IMesajDeposu` ile kaydeder. LLM hata verirse hiçbir şey kaydedilmez (cevapsız "yetim" soru kalmaz).
- **`MesajlariGetirAsync(oturumId, adet)`:** oturumun son mesajlarını döner (adet 1-100 arasına sıkıştırılır).
> Modele **geçmiş mesajlar henüz gönderilmiyor**: her soru modele sıfırdan gider. Geçmişi prompt'a katmak Aşama 8'in (AI Memory) işi.

---

## `src/Asistan.Infrastructure` (dış dünyayla gerçek konuşma)

### `Persistence/AppDbContext.cs`
Veritabanının C# karşılığı. `DbSet<Kullanici>` ve `DbSet<Abonelik>` tabloları temsil eder. `OnModelCreating` içinde kurallar:
- Email: **unique index** + en fazla 200 karakter.
- Ad: en fazla 100 karakter. Plan: en fazla 50 karakter.

### `Persistence/KullaniciDeposu.cs`
`IKullaniciDeposu`'nun EF Core ile gerçek hali:
- `GetirAsync`: `AsNoTracking` (sadece okuma, hızlı) + `Include(Abonelikler)` (ilişkili veriyi aynı sorguda getir).
- `EmailVarMiAsync`: `AnyAsync` ile var mı diye bakar.
- `EkleAsync`: `Add` + `SaveChangesAsync` (burada gerçekten Postgres'e yazılır).

### `Persistence/Migrations/` (otomatik üretilir, elle düzenleme)
- `20261119084848_IlkSema.cs`: "Kullanicilar ve Abonelikler tablolarını oluştur" (Up) ve "geri al" (Down).
- `..._IlkSema.Designer.cs`: EF'in iç bilgisi.
- `AppDbContextModelSnapshot.cs`: şemanın son hali; bir sonraki migration farkı buradan hesaplar.

### `Mongo/MongoMesajDeposu.cs`
`IMesajDeposu`'nun MongoDB ile gerçek hali. `KaydetAsync` = `InsertManyAsync` (soru + cevap tek çağrıda). `SonMesajlarAsync` = oturuma göre filtrele, `olusturulma` ve `_id`'ye göre ters sırala, `Limit(adet)`, sonra listeyi ters çevir (eskiden yeniye). İkinci sıralama anahtarı `_id`, çünkü soru ve cevap aynı milisaniyede yazılabilir.

### `Mongo/MongoAyarlari.cs`
İki sınıf:
- `MongoSinifHaritalari.Kaydet()`: `Mesaj.Id` string'ini MongoDB'de **ObjectId** olarak saklar, alan adlarını **camelCase** yapar (`OturumId` → `oturumId`). Uygulama başında bir kez çalışır.
- `MongoIndexServisi` (`IHostedService`): uygulama açılırken `mesajlar` koleksiyonuna `{ oturumId: 1, olusturulma: -1 }` index'ini kurar. MongoDB kapalıysa uygulamayı düşürmez, sadece uyarı loglar.

### `Llm/LiteLlmIstemcisi.cs`
`ILlmIstemcisi`'nin gerçek hali. `POST /v1/chat/completions` ile LiteLLM'e konuşur (OpenAI formatı). Cevabın `choices[0].message.content` alanını okur. Hata kodu gelirse `EnsureSuccessStatusCode` ile `HttpRequestException` fırlatır.

### `DependencyInjection.cs`
**Tüm kayıtlar tek yerde** (`AddInfrastructure`):
- `AddDbContext<AppDbContext>` → Postgres bağlantısı (`ConnectionStrings:Sql`).
- `AddScoped<IKullaniciDeposu, KullaniciDeposu>`.
- `AddSingleton<IMongoClient>` (bağlantı havuzu tutar, o yüzden Singleton) + `IMongoDatabase` (veritabanı adı `Mongo:Database`, varsayılan `asistan`) + `AddScoped<IMesajDeposu, MongoMesajDeposu>` + `AddHostedService<MongoIndexServisi>`.
- `AddHttpClient<ILlmIstemcisi, LiteLlmIstemcisi>` → adres `LiteLlm:BaseUrl`, anahtar `LiteLlm:Key`, zaman aşımı 120 sn.

Paketler: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` (Postgres sürücüsü), `Microsoft.Extensions.Http` (`AddHttpClient` için), `MongoDB.Driver` (MongoDB sürücüsü), `Microsoft.Extensions.Hosting.Abstractions` (`IHostedService` için).

---

## `src/Asistan.Api` (dışarıya açılan kapı)

### `Program.cs`
Uygulamanın giriş noktası. İki aşama:
1. **Servisleri kaydet:** controller'lar, OpenAPI, `AddInfrastructure`, `IKullaniciServisi`, `ISohbetServisi`.
2. **Middleware hattını kur:** Development'ta OpenAPI + Scalar, HTTPS yönlendirme, yetkilendirme, controller'ları haritala. **Sıra önemlidir.**

### `Controllers/KullanicilarController.cs`
- `POST /api/kullanicilar` → 201 Created (veya 400 / 409).
- `GET /api/kullanicilar/{id}` → 200 (veya 404).
Hataları HTTP koduna çevirir: `ArgumentException` → 400, `EmailZatenVarException` → 409.

### `Controllers/SohbetController.cs`
- `POST /api/sohbet` → gövde `{ "kullaniciId": "...", "soru": "...", "oturumId": "..." }` (`oturumId` isteğe bağlı) → `{ "cevap": "...", "oturumId": "..." }`. `oturumId` verilmezse yeni sohbet başlar; sonraki soruda aynı `oturumId`'yi geri gönderirsen aynı sohbete eklenir.
- `GET /api/sohbet/{oturumId}/mesajlar?adet=20` → oturumun son mesajları (`rol`, `icerik`, `olusturulma`), eskiden yeniye.
- Boş soru → 400. LiteLLM'e ulaşılamazsa → **502 Bad Gateway**.
- Şu an **senkron** (cevap gelene kadar bekler). Aşama 9'da kuyruğa taşınıp 202 dönecek.

### `appsettings.json`
Gizli olmayan ayarlar: log seviyesi, `Mongo:Database = asistan` ve `LiteLlm:BaseUrl = http://localhost:4000`.

### `appsettings.Development.json`
Sadece geliştirmede geçerli ayarlar (şimdilik sadece log seviyesi).

### `Properties/launchSettings.json`
Visual Studio / `dotnet run` profilleri: `http` → `http://localhost:5254`, `https` → `https://localhost:7080`.

### `Asistan.Api.http`
Visual Studio'da tek tıkla çalışan örnek istekler.

### `Asistan.Api.csproj`
Paketler: `Microsoft.AspNetCore.OpenApi`, `Microsoft.EntityFrameworkCore.Design` (migration aracı için), `Scalar.AspNetCore` (**2.12.\*** sürümü; yenisi .NET 9 ile uyumsuz). `UserSecretsId` = bu projenin user-secrets kasasının kimliği.

### Gizli ayarlar (Git'te yok): `user-secrets`
Makineye özel kasada duran üç değer:
- `ConnectionStrings:Sql` → Postgres bağlantı cümlesi.
- `Mongo:Url` → MongoDB bağlantı cümlesi (kullanıcı/şifre içerir; `?authSource=admin` sonu önemli, çünkü kullanıcı `admin` veritabanında tanımlı).
- `LiteLlm:Key` → LiteLLM'e girmek için anahtar (`.env`'deki `LITELLM_MASTER_KEY` ile aynı olmalı).

---

## `src/Asistan.Worker` (arka plan işçisi, şimdilik iskelet)

- `Program.cs`: Worker'ı ayağa kaldırır.
- `Worker.cs`: **şablondan kalan geçici kod**, her saniye log yazar. Aşama 9'da RabbitMQ consumer'ı olacak.
- `appsettings*.json`, `Properties/launchSettings.json`: standart ayarlar.

---

## `tests/Asistan.Tests`

### `KullaniciServisiTests.cs` (9 test sayılır)
Geçerli kayıt; e-posta küçük harfe çevirme; e-posta zaten var; geçersiz giriş (4 farklı girdi, `[Theory]`); kullanıcı var/yok.

### `SohbetServisiTests.cs` (9 test sayılır)
Boş soru (2 girdi): LLM çağrılmaz, kayıt yapılmaz; modelin cevabı döner; mesajlar doğru sırada gider; başarılı soru-cevap **aynı oturuma** kaydedilir; oturum verilmezse her seferinde yeni id üretilir; LLM hata verirse **hiçbir şey kaydedilmez**; `MesajlariGetirAsync` adedi 100'e sıkıştırır; boş oturum id hata verir.

### `Asistan.Tests.csproj`
Paketler: `xunit` (test çerçevesi), `Moq` (sahte nesne), `FluentAssertions` **7.\*** (okunaklı doğrulama; 8. sürüm ticari), `coverlet.collector` (kod kapsama), `Microsoft.NET.Test.Sdk`.

---

## Docker servisleri (`docker-compose.yml`)

| Servis | Image | Port | Volume | Ne için |
|---|---|---|---|---|
| `postgres` | `postgres:17` | 5432 | `postgres_data` | Kullanıcılar, abonelikler. Veritabanı adı: `asistan` |
| `mongo` | `mongo:8` | 27017 | `mongo_data` | Sohbet geçmişi (kullanılıyor, Aşama 7), ileride hafıza |
| `rabbitmq` | `rabbitmq:4-management` | 5672 (uygulama), 15672 (panel) | `rabbitmq_data` | Kuyruk (Aşama 9'dan itibaren) |
| `litellm` | `ghcr.io/berriai/litellm:main-stable` | 4000 | `config.yaml` (salt-okunur) | Claude'a tek kapı |

Postgres, Mongo ve RabbitMQ'da **healthcheck** var: "başladı" ile "hazır" farklıdır, `docker compose ps` `healthy` gösterir.

---

## Adresler ve portlar (hızlı bakış)

| Adres | Ne |
|---|---|
| `http://localhost:5254` | API |
| `http://localhost:5254/scalar/v1` | API'yi tarayıcıdan deneme ekranı (sadece Development) |
| `http://localhost:5254/openapi/v1.json` | API'nin makine-okur tarifi |
| `http://localhost:4000` | LiteLLM |
| `http://localhost:15672` | RabbitMQ yönetim paneli |
| `localhost:5432` | PostgreSQL |
| `localhost:27017` | MongoDB |
