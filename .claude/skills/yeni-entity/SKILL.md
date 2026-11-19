---
name: yeni-entity
description: AiAsistan projesine PostgreSQL/EF Core ile yeni bir entity (tablo) eklerken kullan. Entity sınıfı, DbContext ayarı, depo arayüzü ve uygulaması, iş servisi, DI kaydı, migration ve unit testleri doğru katmanlara ve sırayla ekler. "yeni entity ekle", "yeni tablo ekle", "X sınıfını veritabanına ekle" gibi isteklerde kullan. MongoDB koleksiyonları için DEĞİL (onlar için Mesaj/Hafiza örneklerine bak).
---

# Yeni entity ekleme (AiAsistan, Clean Architecture)

Bağımlılık kuralı: oklar hep **içe** akar. Domain hiçbir şeyi bilmez; Application sadece Domain'i bilir; Infrastructure ikisini de bilir.
**Application, Infrastructure'a asla referans vermez.** Domain'e EF Core veya MongoDB'ye özel attribute koyma.

Aşağıda `<Ad>` yerine entity adını yaz (Türkçe, PascalCase, ör. `Not`). Mevcut örnek: `Kullanici` (Domain/Entities, Application/Kullanicilar, Infrastructure/Persistence).

## Adımlar (sırayla)

1. **Domain:** `src/Asistan.Domain/Entities/<Ad>.cs`. Düz sınıf. `int Id`; zorunlu metin alanları `string Alan { get; set; } = null!;`. İlişki varsa foreign key (`int XId`) ve navigation property.

2. **Veritabanı ayarı:** `src/Asistan.Infrastructure/Persistence/AppDbContext.cs`
   - `public DbSet<<Ad>> <Ad>ler => Set<<Ad>>();`
   - `OnModelCreating` içinde uzunluk sınırları (`HasMaxLength`) ve gerekiyorsa `HasIndex(...).IsUnique()`.

3. **Application arayüzü ve servis:** `src/Asistan.Application/<Ad>ler/`
   - `I<Ad>Deposu.cs`: ihtiyaç duyulan async metotlar (`GetirAsync`, `EkleAsync`...). Hepsi `CancellationToken ct = default` alır.
   - `<Ad>Servisi.cs`: iş kuralları (boş alan, tekrar kontrolü). Geçersiz girişte `ArgumentException`; iş kuralı ihlalinde kendi özel exception'ın (`KullaniciServisi`'ndeki `EmailZatenVarException` gibi).
   - Servis **sadece arayüzlere** bağımlı olsun (test edilebilir).

4. **Depo uygulaması:** `src/Asistan.Infrastructure/Persistence/<Ad>Deposu.cs`. Sadece okuyan sorgularda `AsNoTracking()`. İlişkili veri lazımsa `Include`. Her yerde `async`/`await`, `.Result` ve `.Wait()` yasak. Filtre her zaman `ToList`'ten ÖNCE.

5. **DI kaydı (iki yer):**
   - `src/Asistan.Infrastructure/DependencyInjection.cs` → `services.AddScoped<I<Ad>Deposu, <Ad>Deposu>();`
   - `src/Asistan.Application/DependencyInjection.cs` → `services.AddScoped<I<Ad>Servisi, <Ad>Servisi>();`
   (`DbContext` ve depolar `Scoped`; asla `Singleton` yapma.)

6. **Migration** (dotnet-ef projeye özeldir: `dotnet tool restore` gerekir; uygulamanın sırlarına ihtiyaç yoktur, `AppDbContextFabrikasi` kullanılır):
   ```
   dotnet tool restore
   dotnet ef migrations add Add<Ad> -p src/Asistan.Infrastructure -s src/Asistan.Api -o Persistence/Migrations
   ```
   Üretilen `Up()` metnini **oku**: beklediğin tablo/kolon/index var mı? Uygulanmış bir migration'ı asla düzenleme; hata varsa yenisini ekle (uygulanmamışsa `dotnet ef migrations remove ...`).
   Yerel veritabanına uygulamak için (Docker'daki Postgres açıkken): `dotnet ef database update -p src/Asistan.Infrastructure -s src/Asistan.Api`.

7. **API ucu (sadece istenirse):** `src/Asistan.Api/Controllers/<Ad>Controller.cs`. Controller sadece çevirmendir: servisi çağırır, hataları HTTP koduna çevirir (`ArgumentException` → 400, çakışma → 409, yok → 404). İş kuralı controller'a yazılmaz.

8. **Testler:** `tests/Asistan.Tests/<Ad>ServisiTests.cs`. xUnit + Moq + FluentAssertions, **en az 3 test** (başarılı yol, geçersiz giriş, iş kuralı ihlali). AAA kalıbı; isim: `Metot_Durum_BeklenenSonuc`. Depoyu `Mock<I<Ad>Deposu>` ile taklit et; `Times.Never` ile "kaydetmemeli" durumlarını doğrula. Örnek: `KullaniciServisiTests.cs`.

9. **Doğrula (hepsi yeşil olmadan bitirme):**
   ```
   dotnet build        # uyarılar HATA sayılır (TreatWarningsAsErrors), 0 uyarı olmalı
   dotnet test
   ```

10. **Rehber:** `docs/01-proje-haritasi.md` içine yeni dosyaları ekle (hangi dosya ne işe yarar).

11. **Commit:** Conventional Commits: `feat: <Ad> entity'si ve servisi`. Migration dosyaları commit'e girer. `.env`, `appsettings.Local.json` ve gizli değerler ASLA.

## Sık hatalar
- Application'dan `Asistan.Infrastructure` namespace'ini kullanmak (derleyici zaten reddeder: proje referansı yok).
- Migration üretip testi unutmak.
- `AsNoTracking()` olmadan okuma sorgusu; `Include` olmadan ilişkili veriye erişip N+1 sorgu üretmek.
- Servise `DbContext` enjekte etmek (servis arayüze bağlı olmalı).
