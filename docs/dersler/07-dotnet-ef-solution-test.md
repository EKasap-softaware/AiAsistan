# Ders 7 · .NET, Entity Framework Core, Solution yapısı ve Unit test (ayrıntılı)

Dosyalar: `AiAsistan.sln`, `Directory.Build.props`, `src/` (5 proje), `tests/Asistan.Tests`, `src/Asistan.Infrastructure/Persistence/`, `src/Asistan.Application/Kullanicilar/`. Sayılar bu projeden **ölçülmüştür**.

---

# Bölüm A · .NET ve ASP.NET Core

## A1. Temel kavramlar

| Kavram | Anlamı |
|---|---|
| **.NET** | Microsoft'un ücretsiz, açık kaynaklı platformu; C# ile yazılır; Windows/Linux/macOS'ta çalışır |
| **SDK** | Kod **derlemek** için gereken araç seti (`dotnet build`) |
| **Runtime** | Derlenmiş kodu **çalıştıran** motor. Docker'da bu yüzden SDK ile runtime ayrı imajlardır |
| **Proje (`.csproj`)** | Bir uygulama ya da kütüphane: hedef sürüm, paketler, diğer projelere referanslar |
| **NuGet** | .NET'in paket deposu (npm/pip gibi): `dotnet add package <ad>` |
| **Namespace** | Kodu mantıksal gruplara ayıran isim alanı (`Asistan.Application.Sohbet`) |
| **ASP.NET Core** | .NET'in web tarafı: API ve web sitesi |

Bu projede: **.NET 9** (SDK 9.0.x), EF Core **9.0.x**.

## A2. `Directory.Build.props`: tek yerden ortak ayarlar

Kök klasördeki bu dosya **altındaki tüm projelere** uygulanır; her `.csproj`'u tek tek düzenlemezsin:
```xml
<TargetFramework>net9.0</TargetFramework>
<Nullable>enable</Nullable>               <!-- string ile string? farkını derleyici denetler (null hataları) -->
<ImplicitUsings>enable</ImplicitUsings>   <!-- System, System.Linq... her dosyaya yazılmaz -->
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>   <!-- UYARILAR HATA: kod baştan temiz kalır -->
```
Dikkat: `TreatWarningsAsErrors` bazen can sıkar (ör. yeni bir paketin uyarısı derlemeyi kırar), ama "uyarıları görmezden gelme" alışkanlığını baştan engeller.

## A3. `dotnet` komutları

| Komut | Ne yapar |
|---|---|
| `dotnet build` / `dotnet build -c Release` | Derler (CI Release ile derler) |
| `dotnet test` | Testleri çalıştırır |
| `dotnet run --project src/Asistan.Api --launch-profile http` | API'yi çalıştırır |
| `dotnet add <proje> package <ad>` | Paket ekler |
| `dotnet add <proje> reference <diğer proje>` | Proje referansı ekler |
| `dotnet sln list` | Solution'daki projeler |
| `dotnet tool restore` | `.config/dotnet-tools.json`'daki araçları (dotnet-ef) kurar |
| `dotnet user-secrets set "Anahtar" "değer" -p src/Asistan.Api` | Gizli ayar (makineye özel kasa) |

## A4. Bir Web API'nin anatomisi: `Program.cs`

İki aşama: önce **servisler kaydedilir**, sonra **istek hattı (middleware) kurulur**.
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddApplication(builder.Configuration);     // 1) servisleri kaydet
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKuyruk(builder.Configuration);
var app = builder.Build();
app.UseHttpsRedirection();   // 2) middleware hattı: SIRA ÖNEMLİ
app.UseAuthorization();
app.MapControllers();
app.Run();
```
**Middleware:** İstek controller'a ulaşmadan önce bir dizi ara katmandan geçer (loglama, hata yakalama, kimlik doğrulama); her biri isteği işleyip bir sonrakine iletir. Sıra önemlidir: `UseAuthentication` her zaman `UseAuthorization`'dan önce gelir.

## A5. Dependency Injection (DI) ve ömürler

Bir sınıf ihtiyaç duyduğu nesneleri kendisi `new` ile yaratmaz, **dışarıdan (constructor'dan) alır**:
```csharp
public class SohbetServisi(ILlmIstemcisi llm, IMesajDeposu mesajlar, IHafizaServisi hafiza) : ISohbetServisi
```
Bu yüzden testte gerçek Claude yerine **sahtesi** (Moq) verilir. Kayıtlar `DependencyInjection.cs` dosyalarında:

| Ömür | Ne zaman yaratılır | Projede |
|---|---|---|
| `AddSingleton` | Uygulama boyunca bir kez | `IMongoClient` (içinde bağlantı havuzu var), ayar kayıtları |
| `AddScoped` | Her HTTP isteğinde bir kez | `DbContext`, depolar, iş servisleri |
| `AddTransient` | Her istendiğinde yeni | (şu an yok) |

**`DbContext` asla Singleton olmamalı** (thread-safe değil). Bir DI hatası: *"Unable to resolve service for type X"* = X'i kaydetmeyi unuttun.

## A6. Ayarlar ve öncelik sırası

`appsettings.json` (gizli olmayan) → `appsettings.Development.json` → **user-secrets (sadece Development)** → ortam değişkenleri → komut satırı. Bizde `Program.cs` en sona `appsettings.Local.json` ekler (yedek dosya): **sonra eklenen kaynak kazanır**, yani Development'ta o dosya hepsinin üstünde. Ortam değişkeninde iç içe anahtar `__` ile yazılır: `Mongo__Url` = `Mongo:Url`. Kasa **sadece `Development` ortamında yüklenir**: Production'da (container'da) değerler ortam değişkeniyle verilmelidir.

## A7. `async` / `await`

Veritabanı ve HTTP çağrıları **bekleme** gerektirir. `await` beklerken thread'i **serbest bırakır**; sunucu aynı anda çok daha fazla isteği karşılar (garson siparişi mutfağa verip masada beklemez, başka masaya gider). Kurallar: I/O yapan her metot `async Task` döner ve `Async` ile biter; **`.Result` ve `.Wait()` kullanma**; `CancellationToken ct` al (istemci vazgeçerse işlem iptal edilebilsin).

---

# Bölüm B · Solution ve Clean Architecture

**Solution (`.sln`)** birden çok projeyi tek çatı altında tutar. Kodu **sorumluluğuna** göre projelere böleriz.

```
Domain ◄── Application ◄── Infrastructure ◄── Api
                 ▲                 ▲
                 └──── Tests       └── Worker
```

| Proje | Ne var | Referans verdiği | Doğrudan paketler |
|---|---|---|---|
| **Domain** | Veri sınıfları (`Kullanici`, `Mesaj`, `IsKaydi`) | hiçbiri | (hiç) |
| **Application** | İş kuralları + arayüzler (`ISohbetServisi`, `IMesajDeposu`) | Domain | Logging/DI abstractions, Configuration.Binder |
| **Infrastructure** | EF Core, MongoDB, RabbitMQ, LiteLLM, LangFlow ile gerçek konuşma | Application, Domain | EF Core, Npgsql, MongoDB.Driver, MassTransit.RabbitMQ, Extensions.Http |
| **Api** | Controller'lar, giriş noktası | Application, Infrastructure | OpenApi, EF Design, Scalar |
| **Worker** | Kuyruk işçisi (consumer'lar) | Application, Infrastructure | Extensions.Hosting |
| **Tests** | xUnit testleri | Application, Domain, Infrastructure, Worker | xunit, Moq, FluentAssertions, coverlet |

**Ölçülen büyüklük:** `src` ≈ **1 489 satır C#** (39 dosya, migration hariç), `tests` ≈ **1 009 satır** (7 dosya). Test kodu, uygulama kodunun yaklaşık %68'i kadar.

**Bağımlılık kuralı ve neden:** Oklar hep içe doğru. Domain hiçbir şeyi bilmez. Application "mesajı bir yere kaydet" der (`IMesajDeposu`) ama nereye olduğunu bilmez; bugün MongoDB, yarın başka bir şey olsa sadece Infrastructure değişir. Testte sahte depo verilir. Kural `ProjectReference` satırlarıyla **zorlanır**: Application'a Infrastructure'ı eklemeye kalksan derleyici izin vermez (kural kafada değil, yapıda).

**Nereye yazılır?**

| Kod ne yapıyor? | Nereye |
|---|---|
| Veri sınıfı, saf iş kavramı | Domain |
| İş kuralı ("boş soruyu reddet", "aynı e-posta iki kez olmasın") | Application |
| Veritabanı, HTTP, kuyruk, dış servis | Infrastructure |
| HTTP isteği alma, yanıt şekli, HTTP kodu | Api (controller sadece **çevirmendir**) |

---

# Bölüm C · Entity Framework Core

## C1. Nedir?

EF Core bir **ORM**'dir (Object-Relational Mapper): veritabanı tablolarını C# sınıfı olarak görürsün, SQL yerine **LINQ** yazarsın, EF onu SQL'e çevirir.

```csharp
// Entity: tabloya karşılık gelen sınıf
public class Kullanici { public int Id; public string Ad; public string Email; public List<Abonelik> Abonelikler; }

// DbContext: veritabanının kendisi
public class AppDbContext(DbContextOptions<AppDbContext> o) : DbContext(o)
{
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    protected override void OnModelCreating(ModelBuilder mb)
        => mb.Entity<Kullanici>(e => { e.HasIndex(k => k.Email).IsUnique(); e.Property(k => k.Email).HasMaxLength(200); });
}
```

## C2. EF'in ürettiği SQL (gerçek)

`dotnet ef migrations script` ile `IlkSema` migration'ının SQL'i:
```sql
CREATE TABLE "Kullanicilar" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "Ad" character varying(100) NOT NULL,
    "Email" character varying(200) NOT NULL,
    "KayitTarihi" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Kullanicilar" PRIMARY KEY ("Id")
);
CREATE TABLE "Abonelikler" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "Plan" character varying(50) NOT NULL,
    "KullaniciId" integer NOT NULL,
    CONSTRAINT "PK_Abonelikler" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Abonelikler_Kullanicilar_KullaniciId" FOREIGN KEY ("KullaniciId")
        REFERENCES "Kullanicilar" ("Id") ON DELETE CASCADE
);
CREATE INDEX "IX_Abonelikler_KullaniciId" ON "Abonelikler" ("KullaniciId");
CREATE UNIQUE INDEX "IX_Kullanicilar_Email" ON "Kullanicilar" ("Email");
```
Ne okuyoruz? C# `string` + `HasMaxLength(100)` → `varchar(100) NOT NULL` (nullable kapalı olduğu için); `HasIndex().IsUnique()` → `UNIQUE INDEX`; ilişki → **foreign key + `ON DELETE CASCADE`** (kullanıcı silinirse abonelikleri de silinir: zorunlu ilişkide EF'in varsayılanı); foreign key'e otomatik index.

## C3. Önemli davranışlar

- **`SaveChangesAsync()`'e kadar hiçbir şey veritabanına gitmez.** Hepsi tek transaction'da yazılır; EF **değişiklik takibi (change tracking)** ile ne değiştiğini kendisi bulur.
- **Sorgu ertelenir:** `ToListAsync()` / `FirstOrDefaultAsync()` çağrılana kadar sorgu çalışmaz, sadece tarif edilir.
- **`AsNoTracking()`:** sadece okuyacaksan takibi kapat (hızlanır) — `KullaniciDeposu.GetirAsync` böyle.
- **`Include(k => k.Abonelikler)`:** ilişkili veriyi aynı sorguda getirir (eager loading).
- Filtre her zaman `ToList`'ten **önce**: sonra yazarsan tüm tablo belleğe gelir.

**Sık hata: N+1.** Döngüde her satır için ilişkili veriye erişmek her satır için ayrı sorgu atar. Çözüm `Include` ya da `Select` ile projeksiyon.

## C4. Migration

Entity'yi değiştirdiğinde veritabanı bunu kendiliğinden bilmez. **Migration**, değişikliği tarif eden ve Git'e giren bir C# dosyasıdır.
```
dotnet ef migrations add Ad -p src/Asistan.Infrastructure -s src/Asistan.Api -o Persistence/Migrations
dotnet ef database update -p src/Asistan.Infrastructure -s src/Asistan.Api
```
`-p` = migration'ların duracağı proje, `-s` = başlangıç projesi. Postgres'te `__EFMigrationsHistory` tablosu hangilerinin uygulandığını tutar. **Uygulanmış migration'ı düzenleme, yenisini ekle.** Üretimde uygulama açılışında `Migrate()` yerine **migration bundle** kullanılır (Ders 9) ve araçlar için **`AppDbContextFabrikasi`** (tasarım zamanı fabrikası) vardır: EF araçları uygulamanın `Program.cs`'ine ve sırlarına ihtiyaç duymaz.

---

# Bölüm D · Unit test

## D1. Nedir?

Kodun en küçük parçasını (genelde tek bir metodu) dış dünyadan **yalıtıp** otomatik sınayan koddur. Veritabanına, ağa, dosyaya dokunmaz; milisaniyede çalışır. Bir değişiklik yaptığında eski davranışın bozulmadığını saniyeler içinde görürsün.

**Test piramidi:** çok sayıda **unit** test (hızlı, ucuz), orta sayıda **integration** (gerçek DB/HTTP), az sayıda **end-to-end**. Bizde unit testler var; integration yok (bkz. D5).

## D2. Araçlar

| Araç | Görevi | Örnek |
|---|---|---|
| **xUnit** | Test çerçevesi | `[Fact]` tek test; `[Theory]` + `[InlineData]` aynı test farklı girdilerle |
| **Moq** | Bağımlılığın sahtesini (mock) üretir | `Setup(...)` "şunu sorarsan şunu cevapla", `Verify(...)` "çağrıldı mı, kaç kez?" |
| **FluentAssertions** (7.x) | Okunaklı doğrulama | `sonuc.Should().Be("emre@test.com")`, `await islem.Should().ThrowAsync<...>()` |
| **coverlet** | Kod kapsama ölçer | CI'da `--collect:"XPlat Code Coverage"` |

FluentAssertions'ın **7.x** sürümünü kullanıyoruz (8. sürüm ticari lisansa geçti).

## D3. AAA kalıbı ve gerçek bir test

Her test üç bölümdür: **Arrange** (hazırla), **Act** (çalıştır), **Assert** (doğrula). İsim: `Metot_Durum_BeklenenSonuc`.
```csharp
[Fact]
public async Task OlusturAsync_EmailZatenVar_HataFirlatirVeKaydetmez()
{
    _depo.Setup(d => d.EmailVarMiAsync("emre@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);   // Arrange

    var islem = () => _servis.OlusturAsync(new KullaniciOlusturIstegi("Emre", "emre@test.com"));                // Act

    await islem.Should().ThrowAsync<EmailZatenVarException>();                                                  // Assert
    _depo.Verify(d => d.EkleAsync(It.IsAny<Kullanici>(), It.IsAny<CancellationToken>()), Times.Never);         // kaydetmemeli
}
```
Bu test çalışabiliyor çünkü `KullaniciServisi` somut sınıfı değil **arayüzü** (`IKullaniciDeposu`) bekliyor: DI + arayüz, test edilebilirliğin temelidir. Gerçek PostgreSQL ya da internet yok, test milisaniyede çalışır.

**Test yazma kuralları:** Bir test **tek şeyi** doğrular. Testler **birbirinden bağımsız** olur. **Davranışı** test et, iç detayı değil. Bir bug bulunca önce onu yakalayan testi yaz, sonra düzelt.

## D4. Gerçek sayılar: kaç test, ne kadar kapsama?

**59 test metodu → 74 test** (`[Theory]` her `[InlineData]` satırı için ayrı test sayılır).

| Dosya | Test metodu |
|---|---|
| `HafizaServisiTests` | 14 |
| `IsServisiTests` | 9 |
| `SohbetServisiTests` | 9 |
| `DokumanVeLangFlowTests` | 8 |
| `AyarDogrulamaTests` | 7 |
| `KullaniciServisiTests` | 6 |
| `WorkerConsumerTests` | 6 |

**Kod kapsama (satır):**

| Proje | Kapsama | Yorum |
|---|---|---|
| **Application** | **%94.2** | İş mantığı (asıl test edilmesi gereken yer) iyi korunuyor |
| Domain | %80.0 | Veri sınıfları; `Abonelik` hiç kullanılmıyor (%0) |
| Worker | %46.8 | Consumer'lar test edilmiş; `Program.cs` (kablolama) edilmemiş |
| **Infrastructure** | **%23.6** | Dış sistemlerle konuşan kod (MongoDB, RabbitMQ, LiteLLM, EF): **unit testle sınanmaz** |
| **Toplam** | **%45.1** | |

**Yorum (dürüst):** %45 "düşük" görünür ama yanıltıcıdır: asıl önemli olan iş mantığı %94. Düşük kalan kısım, gerçek bir veritabanı ve kuyruk **gerektiren** kod: ona unit test yazmak yerine **integration test** gerekir (Testcontainers ile gerçek PostgreSQL/MongoDB container'ı başlatıp sınamak, `WebApplicationFactory` ile API'yi uçtan uca denemek). Bu projede yapılmadı; bilinen eksik olarak duruyor. Kapsama bir **hedef değil, bir gösterge**dir: yüksek rakam kaliteyi garanti etmez.

## D5. Testlerin gerçekten koruma sağladığını nasıl biliriz? (mutasyon kontrolü)

Yeşil test tek başına yetmez; testlerin hatayı **yakaladığını** görmek gerekir. Kodu bilerek bozup testlerin kırmızıya döndüğüne baktık:
1. `ToLowerInvariant()` satırını sildik → e-posta küçük harf testi kırıldı.
2. Özetleme sınırını (`yeniSinir`) bozduk → **3 test** kırıldı.
3. Aynı mantıkla `actionlint` denetleyicisini bilerek bozuk bir workflow dosyasıyla sınadık (sessiz geçmesine güvenmedik).
Kod değişikliklerini sonra geri aldık; testler yine yeşil.

## E. Bilmen gerekenler

1. **SDK derler, runtime çalıştırır.** `Directory.Build.props` tüm projelere ortak ayar verir; `TreatWarningsAsErrors` kodu temiz tutar.
2. Solution = projeler çatısı; **bağımlılıklar içe akar** ve `ProjectReference`'larla zorlanır. Domain hiçbir şeyi bilmez.
3. Kod nereye? Veri → Domain, iş kuralı → Application, dış dünya → Infrastructure, HTTP → Api.
4. DI: nesneyi dışarıdan al. Ömürler: Singleton (bir kez), Scoped (istek başına), Transient (her seferinde). `DbContext` Scoped.
5. `async/await`: I/O'da thread'i serbest bırakır; `.Result` yasak.
6. EF: tablo = sınıf, SQL yerine LINQ; `SaveChanges`'e kadar DB'ye gitmez; `AsNoTracking`, `Include`; N+1'den kaçın.
7. Migration şema değişikliğinin Git'e giren hâlidir; uygulanmış olanı düzenleme, yenisini ekle.
8. Unit test = yalıtılmış, hızlı, AAA; Moq ile sahte bağımlılık, FluentAssertions ile okunaklı doğrulama.
9. Kapsama bir göstergedir: iş mantığı %94, dış dünya %24; eksik olan integration testtir.
10. Testin koruduğunu **kodu bozup** doğrula.
