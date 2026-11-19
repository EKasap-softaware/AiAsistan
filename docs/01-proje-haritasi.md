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

### `Entities/Hafiza.cs`
**Uzun süreli hafıza** kaydı: `Id`, `KullaniciId`, `Icerik` (tek kısa cümle, ör. "Kullanıcının adı Emre."), `Olusturulma`. MongoDB'de `hafizalar` koleksiyonu. Hafıza **kullanıcıya** bağlıdır (oturuma değil): kullanıcı yeni bir sohbet başlatsa da asistan onu hatırlar.

### `Entities/OturumOzeti.cs`
**Kısa süreli hafızanın** özet kısmı: `OturumId`, `Ozet` (en çok 5 cümle), `OzetlenenMesajSayisi` (oturumun ilk kaç mesajı özete girdi), `Guncellenme`. MongoDB'de `oturumOzetleri` koleksiyonu; oturum başına tek kayıt.

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

### `Bellek/HafizaArayuzleri.cs`
İki arayüz: `IHafizaDeposu` (hafıza ekle, kullanıcının en yeni N hafızasını getir, tek/tüm hafızayı sil; silmede `kullaniciId` de kontrol edilir ki biri başkasınınkini silemesin) ve `IOturumOzetDeposu` (özeti getir, özeti kaydet/güncelle).

### `Bellek/HafizaAyarlari.cs`
Hafızanın ayar kaydı. `appsettings.json > Hafiza` bölümünden okunur:

| Ayar | Varsayılan | Anlamı |
|---|---|---|
| `PencereBoyutu` | 10 | Özetlemeden sonra prompt'ta ham kalacak mesaj sayısı |
| `OzetEsigi` | 20 | Özetlenmemiş mesaj sayısı bunu aşınca özetleme başlar |
| `MaksHafiza` | 20 | Prompt'a en fazla kaç uzun süreli hafıza girer |
| `HizliModel` | `hizli` | Özetleme ve hafıza çıkarma için kullanılan ucuz model (`config.yaml`'daki takma ad) |

### `Bellek/HafizaServisi.cs`
Projenin "beyni" (AI Memory). `IHafizaServisi` + `HafizaServisi`:
- **`BaglamKurAsync`:** modele gidecek mesaj listesini kurar: `system` (temel prompt + "Kullanıcı hakkında bildiklerin" + "Konuşmanın önceki kısmının özeti") → özete girmemiş **ham** mesajlar → yeni soru. Özet + ham mesajlar oturumun tamamını kapsar, arada hiçbir mesaj kaybolmaz.
- **`SohbetSonrasiAsync`:** cevap kaydedildikten sonra iki iş yapar, biri patlasa diğeri denenir ve **asla hata fırlatmaz** (kullanıcının cevabı bozulmasın): (1) özetlenmemiş mesaj eşiği aştıysa eskileri `hizli` modelle özetleyip kaydeder; (2) `hizli` modele "kullanıcı hakkında kalıcı yeni bilgi var mı?" diye sorar, varsa `hafizalar`'a yazar.
- **Hafıza çıktısı temizlenir:** `YOK` (büyük/küçük harf, noktalı) ise yazılmaz; 200 karakterden uzunsa yazılmaz; satır sonları tek satıra çevrilir (hafıza sonradan system prompt'a gireceği için satır enjeksiyonu engellenir); zaten bilinen bilgi tekrar yazılmaz.
- `HafizalariGetirAsync` / `HafizaSilAsync` / `TumunuSilAsync`: kullanıcının hafızasını görme ve silme hakkı.
- Paket: `Microsoft.Extensions.Logging.Abstractions` (uyarıları loglamak için).

### `Sohbet/IMesajDeposu.cs`
Arayüz: `KaydetAsync(mesajlar)`, `SonMesajlarAsync(oturumId, adet)` (eskiden yeniye), `SayAsync(oturumId)` (toplam mesaj sayısı) ve `AralikGetirAsync(oturumId, atla, adet)` (eskiden yeniye, ilk `atla` mesajı geçerek). MongoDB'yi bilmez; gerçek hali `MongoMesajDeposu`.

### `Sohbet/SohbetServisi.cs`
`SohbetSonucu(Cevap, OturumId)` + `ISohbetServisi` + `SohbetServisi` (artık bir **orkestra şefi**: işi `IHafizaServisi`, `IMesajDeposu` ve `ILlmIstemcisi`'ne dağıtır):
- **`SorAsync(kullaniciId, soru, oturumId?)`:** (1) boş soruyu reddeder; (2) `oturumId` yoksa yeni üretir; (3) `hafiza.BaglamKurAsync` ile bağlamı kurar; (4) modele sorar; (5) **model cevap verdikten sonra** soru ve cevabı kaydeder (LLM hata verirse hiçbir şey kaydedilmez ve hafıza işi çalışmaz); (6) `hafiza.SohbetSonrasiAsync` ile özetleme ve kalıcı bilgi çıkarmayı yapar.
- **`MesajlariGetirAsync(oturumId, adet)`:** oturumun son mesajlarını döner (adet 1-100 arasına sıkıştırılır).
- `SistemPrompt` sabiti burada; `HafizaServisi` bunun üstüne hafıza ve özet ekler.

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
`IMesajDeposu`'nun MongoDB ile gerçek hali. `KaydetAsync` = `InsertManyAsync` (soru + cevap tek çağrıda). `SonMesajlarAsync` = oturuma göre filtrele, `olusturulma` ve `_id`'ye göre ters sırala, `Limit(adet)`, sonra listeyi ters çevir (eskiden yeniye). `SayAsync` = `CountDocumentsAsync`. `AralikGetirAsync` = eskiden yeniye sırala + `Skip(atla)` + `Limit(adet)`. İkinci sıralama anahtarı `_id`, çünkü soru ve cevap aynı milisaniyede yazılabilir.

### `Mongo/MongoHafizaDeposu.cs`
`IHafizaDeposu`'nun MongoDB hali (`hafizalar` koleksiyonu). `SilAsync` filtrede hem `_id` hem `kullaniciId` kullanır (başkasının hafızası silinemez) ve geçersiz bir id gelirse hata yerine `false` döner.

### `Mongo/MongoOturumOzetDeposu.cs`
`IOturumOzetDeposu`'nun MongoDB hali (`oturumOzetleri` koleksiyonu). Kaydetme bir **upsert**: kayıt varsa alanları günceller, yoksa oluşturur. Belgeyi komple değiştirmek yerine alan alan güncellenir ki yeni kayıtta `_id`'yi MongoDB üretsin.

### `Mongo/MongoAyarlari.cs`
İki sınıf:
- `MongoSinifHaritalari.Kaydet()`: `Mesaj`, `Hafiza` ve `OturumOzeti` sınıflarının `Id` string'ini MongoDB'de **ObjectId** olarak saklar, alan adlarını **camelCase** yapar (`OturumId` → `oturumId`). Uygulama başında bir kez çalışır.
- `MongoIndexServisi` (`IHostedService`): uygulama açılırken index'leri kurar: `mesajlar` → `{ oturumId: 1, olusturulma: -1 }`; `hafizalar` → `{ kullaniciId: 1, olusturulma: -1 }`; `oturumOzetleri` → `{ oturumId: 1 }` **unique** (oturum başına tek özet). MongoDB kapalıysa uygulamayı düşürmez, sadece uyarı loglar.

### `Llm/LiteLlmIstemcisi.cs`
`ILlmIstemcisi`'nin gerçek hali. `POST /v1/chat/completions` ile LiteLLM'e konuşur (OpenAI formatı). Cevabın `choices[0].message.content` alanını okur. Hata kodu gelirse `EnsureSuccessStatusCode` ile `HttpRequestException` fırlatır.

### `DependencyInjection.cs`
**Tüm kayıtlar tek yerde** (`AddInfrastructure`):
- `AddDbContext<AppDbContext>` → Postgres bağlantısı (`ConnectionStrings:Sql`).
- `AddScoped<IKullaniciDeposu, KullaniciDeposu>`.
- `AddSingleton<IMongoClient>` (bağlantı havuzu tutar, o yüzden Singleton) + `IMongoDatabase` (veritabanı adı `Mongo:Database`, varsayılan `asistan`) + `AddScoped<IMesajDeposu, MongoMesajDeposu>` + `AddScoped<IHafizaDeposu, MongoHafizaDeposu>` + `AddScoped<IOturumOzetDeposu, MongoOturumOzetDeposu>` + `AddHostedService<MongoIndexServisi>`.
- `AddHttpClient<ILlmIstemcisi, LiteLlmIstemcisi>` → adres `LiteLlm:BaseUrl`, anahtar `LiteLlm:Key`, zaman aşımı 120 sn.

Paketler: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` (Postgres sürücüsü), `Microsoft.Extensions.Http` (`AddHttpClient` için), `MongoDB.Driver` (MongoDB sürücüsü), `Microsoft.Extensions.Hosting.Abstractions` (`IHostedService` için).

---

## `src/Asistan.Api` (dışarıya açılan kapı)

### `Program.cs`
Uygulamanın giriş noktası. İki aşama:
1. **Servisleri kaydet:** controller'lar, OpenAPI, `AddInfrastructure`, `IKullaniciServisi`, `ISohbetServisi`, `HafizaAyarlari` (appsettings'ten okunur) ve `IHafizaServisi`.
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

### `Controllers/HafizaController.cs`
Kullanıcının **görme ve silme hakkı** (KVKK/GDPR): asistanın onun hakkında ne hatırladığını görür ve siler.
- `GET /api/hafiza/{kullaniciId}` → hafızaların listesi (`id`, `icerik`, `olusturulma`).
- `DELETE /api/hafiza/{kullaniciId}/{hafizaId}` → 204 (silindi) veya 404 (yok, geçersiz id ya da başkasının hafızası).
- `DELETE /api/hafiza/{kullaniciId}` → o kullanıcının tüm hafızaları silinir, `{ "silinen": N }`.
> **Uyarı:** Henüz kimlik doğrulama yok. `kullaniciId`'yi bilen herkes bu uçları kullanabilir.

### `appsettings.json`
Gizli olmayan ayarlar: log seviyesi, `Hafiza` bölümü (pencere, eşik, maksimum hafıza, hızlı model), `Mongo:Database = asistan` ve `LiteLlm:BaseUrl = http://localhost:4000`. Ortam değişkeniyle geçici olarak ezilebilir: `Hafiza__PencereBoyutu=2`.

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

### `SohbetServisiTests.cs` (10 test sayılır)
`HafizaServisi` sahte (mock). Boş soru: hiçbir yan etki yok (LLM, kayıt, hafıza çağrılmaz); modelin cevabı döner; bağlam **hafıza servisinden alınıp aynen** modele gider; başarılı soru-cevap aynı oturuma kaydedilir; **önce kayıt, sonra `SohbetSonrasi`** sırası; oturum verilmezse yeni id; LLM hata verirse kayıt ve hafıza işi yok; `MesajlariGetirAsync` adedi 100'e sıkıştırır; boş oturum id hata verir.

### `HafizaServisiTests.cs` (18 test sayılır)
Küçük sayılarla (pencere 2, eşik 4) test edilir. **Bağlam:** hafıza/özet yoksa sistem prompt'u sadece temel metindir; varsa hafıza ve özet eklenir; özetlenmiş mesajlar atlanıp ham mesajlar sırayla gelir. **Özetleme:** eşik aşılmadıysa özetlemez; aşıldıysa doğru aralığı (`yeniSinir`) özetleyip kaydeder; önceki özet varsa yeni özet isteğine de girer. **Hafıza çıkarma:** yeni bilgiyi kaydeder; `YOK` / `yok.` / boş çıktıyı kaydetmez (5 girdi); zaten bilineni büyük/küçük harf farkıyla tekrarlamaz; 200 karakterden uzunu kaydetmez; çok satırlı çıktıyı tek satıra çevirir. **Dayanıklılık:** LLM hata verirse `SohbetSonrasiAsync` fırlatmaz; özetleme patlasa bile hafıza çıkarma denenir. **Yönetim:** silme depoya doğru iletilir.

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
