# Lab 01: .NET + EF Core + Solution + Unit Test

**Hedef:** Projeye yeni bir `Etiket` tablosu ekle. Entity'den veritabanına, servisten teste kadar
bir özelliğin bütün yolunu kendin yürü. İşte her gün yapacağın iş bu.

**Süre:** ~45 dk. **Başla:** `git checkout -b lab-01-etiket`

Katman kuralı: Domain → Application → Infrastructure. Her görevi bu sırayla yap. Application hiçbir zaman
`Microsoft.EntityFrameworkCore`'u kullanmaz.

## Görevler

**G1. Entity (Domain).** `src/Asistan.Domain/Entities/Etiket.cs`
- `Id` (int), `Ad` (string, boş olamaz), `Olusturulma` (DateTime, varsayılan UTC şimdi).
- Örnek al: `Kullanici.cs`.

**G2. Arayüz ve servis (Application).** `src/Asistan.Application/Etiketler/` klasörü
- `IEtiketDeposu`: `EkleAsync(Etiket, ct)` ve `ListeleAsync(ct)`.
- `IEtiketServisi` + `EtiketServisi(IEtiketDeposu depo)`: `OlusturAsync(string ad, ct)`.
- İş kuralı: ad boş/boşluk olamaz, kırpıldıktan sonra en fazla **50** karakter; ihlalde `ArgumentException`.
- `AddApplication` içinde `IEtiketServisi` kaydını (Scoped) ekle.

**G3. DbContext ve depo (Infrastructure).**
- `AppDbContext`'e `DbSet<Etiket> Etiketler` ekle, `OnModelCreating`'de `Ad` için `HasMaxLength(50)`.
- `Persistence/EtiketDeposu.cs`: `IEtiketDeposu`'yu uygula (`Add` + `SaveChangesAsync`; liste için `AsNoTracking`).
- `Infrastructure/DependencyInjection.cs`'de `IEtiketDeposu` kaydını ekle.

**G4. Migration.**
- `dotnet ef migrations add EtiketEkle -p src/Asistan.Infrastructure -s src/Asistan.Api -o Persistence/Migrations`
- `dotnet ef migrations script ...` ile üretilen SQL'i oku: `Ad` sütunu `varchar(50) NOT NULL` oldu mu?

**G5. Unit test.** `tests/Asistan.Tests/EtiketServisiTests.cs` (en az 3 test)
- Geçerli ad: kırpılıp kaydedilir (`Verify ... Times.Once`).
- Boş ad: `ArgumentException` ve **kaydedilmez** (`Times.Never`).
- 51 karakterlik ad: `ArgumentException`.
- Örnek al: `KullaniciServisiTests.cs`.

**G6. Hepsi yeşil.** `dotnet build` (uyarı = hata) ve `dotnet test`.

Sonra: `./atolye/atolye.ps1 dogrula 01`

## Mülakat soruları (bitince cevapla)
1. Application neden EF Core'u bilmemeli? Bilseydi testte ne değişirdi?
2. `Add` ile `SaveChangesAsync` farkı nedir? SQL ne zaman çalışır?
3. `migrations add` veritabanına dokunur mu? `-p` ile `-s` farkı?
4. Neden `AsNoTracking`? Ne zaman kullanmazsın?
5. Scoped / Singleton / Transient: DbContext hangisi, neden?
6. Boş ad testinde neden `Times.Never` ile "kaydedilmedi" diye de doğruladık?

## Sık hatalar
- `ef` komutu bulunamıyor: `dotnet tool restore`.
- Migration'da tablo yok: `DbSet` eklemeyi unuttun.
- Test derlenmiyor: test projesi Application'a zaten referanslı; `using` satırlarını kontrol et.

## Örnek (benzer ama cevabın kendisi değil: `Urun` için aynı yol)
```csharp
// Domain/Entities/Urun.cs
public class Urun { public int Id { get; set; } public string Ad { get; set; } = null!; public decimal Fiyat { get; set; } }

// Application: servis kuralı + test (Moq ile sahte depo)
if (fiyat < 0) throw new ArgumentException("Fiyat negatif olamaz.", nameof(fiyat));
_depo.Verify(d => d.EkleAsync(It.IsAny<Urun>(), It.IsAny<CancellationToken>()), Times.Never);   // hata durumunda kaydedilmedi

// Infrastructure: DbSet + kural
public DbSet<Urun> Urunler => Set<Urun>();
mb.Entity<Urun>().Property(u => u.Fiyat).HasPrecision(18, 2);
```
Migration komutu aynı: `dotnet ef migrations add UrunEkle -p src/Asistan.Infrastructure -s src/Asistan.Api -o Persistence/Migrations`
Sohbette anlatılan NotDefteri rehberi de aynı kalıptır.
