# 02 · Kavramlar: ne, neden, projede nerede?

Her başlık aynı kalıpta: **Nedir → Neden → Projede nerede → Dikkat.**

---

## 1. Solution ve proje

**Nedir:** Solution (`.sln`) bir klasör gibi, içinde projeler (`.csproj`) var. Her proje ya bir uygulama (çalışır) ya bir kütüphane (başkası kullanır).

**Neden:** Kodu sorumluluğuna göre böleriz. Bir şeyi değiştirdiğinde etkilediği yer sınırlı olur.

**Projede:** 6 proje (bkz. `01-proje-haritasi.md`).

**Dikkat:** Bir proje başka projeyi kullanmak için `ProjectReference` ister. Bu referanslar mimarinin kurallarını **zorlar**.

---

## 2. Katmanlı mimari (Clean Architecture)

**Nedir:** Bağımlılıklar hep içe doğru akar:

```
Domain ◄── Application ◄── Infrastructure ◄── Api
```

**Neden:** Application "mesajı bir yere kaydet" der ama nereye olduğunu bilmez. Bugün Postgres, yarın başka bir veritabanı olsa **sadece Infrastructure** değişir. İş kuralları (Application) dokunulmaz kalır ve testte sahte veritabanıyla denenebilir.

**Projede:** İş kuralı `KullaniciServisi`'nde. Veritabanı konuşması `KullaniciDeposu`'nda. Aralarında `IKullaniciDeposu` arayüzü var.

**Dikkat:** "Bu kod nereye yazılmalı?" sorusunun cevabı:

| Kod ne yapıyor? | Nereye? |
|---|---|
| Veri sınıfı, saf iş kavramı | Domain |
| İş kuralı, "şu olursa şunu reddet" | Application |
| Veritabanı, HTTP, dosya, dış servis | Infrastructure |
| HTTP isteği alma, yanıt şekli | Api |

---

## 3. Arayüz (interface) ve Dependency Injection (DI)

**Nedir:**
- **Arayüz:** "Bu sınıf şu metotları sunar" sözleşmesi. İçi yok, sadece imza.
- **DI:** Bir sınıf ihtiyaç duyduğu nesneyi kendisi yaratmaz, **dışarıdan alır**.

```csharp
// Doğru: dışarıdan geliyor
public class SohbetServisi(ILlmIstemcisi llm) { ... }

// Yanlış: kendisi yaratıyor, gerçek Claude'a kilitlenir
var llm = new LiteLlmIstemcisi(...);
```

**Neden:**
1. **Test edilebilirlik:** Testte gerçek Claude yerine sahte verilir (parasız, internetsiz, milisaniyede).
2. **Değiştirilebilirlik:** Yarın başka bir LLM istemcisi yazarsan servis aynı kalır.

**Projede:** Kayıtlar iki yerde:
- `Asistan.Infrastructure/DependencyInjection.cs` → `AddDbContext`, `AddScoped<IKullaniciDeposu, ...>`, `AddHttpClient<ILlmIstemcisi, ...>`.
- `Asistan.Api/Program.cs` → `AddScoped<IKullaniciServisi, ...>`, `AddScoped<ISohbetServisi, ...>`.

Sonra ASP.NET, bir controller'a ihtiyaç olduğunda zinciri otomatik kurar:
`SohbetController` → `ISohbetServisi` → `ILlmIstemcisi`.

**Üç ömür (lifetime):**

| Kayıt | Ne zaman yaratılır | Projede |
|---|---|---|
| `AddSingleton` | Uygulama boyunca bir kez | (Aşama 7: `MongoClient`) |
| `AddScoped` | Her HTTP isteğinde bir kez | `DbContext`, depo, servisler |
| `AddTransient` | Her istendiğinde yeni | (şu an yok) |

**Dikkat:** `DbContext` **asla Singleton olmamalı** (thread-safe değil). Bir DI hatası: "Unable to resolve service for type X" = X'i `Program.cs`/`DependencyInjection.cs`'de kaydetmeyi unuttun.

---

## 4. async / await

**Nedir:** Veritabanı ve HTTP "bekleme" işleridir. `await` beklerken thread'i **boşa çıkarır**.

```csharp
var cevap = await servis.SorAsync(...);   // beklerken sunucu başka istekleri işler
```

**Neden:** Garson siparişi mutfağa verip masada beklemez, başka masaya gider. Bu sayede sunucu aynı anda çok daha fazla isteği karşılar.

**Projede:** Her I/O metodu `async Task` döner ve `Async` ile biter (`SorAsync`, `EkleAsync`...).

**Dikkat:** `.Result` ve `.Wait()` **kullanma**: thread'i kilitler. `CancellationToken ct` parametresi: istemci bağlantıyı keserse işlemi iptal edebilmek için.

---

## 5. Entity Framework Core (EF Core)

**Nedir:** ORM (Object-Relational Mapper). Tabloları C# sınıfı olarak görürsün, SQL yerine LINQ yazarsın, EF onu SQL'e çevirir.

**Neden:** SQL'i elle yazmak hata eğilimlidir; şemanın tek doğruluk kaynağı kod olur.

**Projede:** `AppDbContext` + `Kullanici`/`Abonelik` entity'leri + `KullaniciDeposu`.

**Önemli davranışlar:**
- `SaveChangesAsync()` çağrılana kadar **hiçbir şey veritabanına gitmez**. Hepsi tek transaction'da yazılır.
- `ToListAsync()`/`FirstOrDefaultAsync()` çağrılana kadar sorgu **çalışmaz**, sadece tarif edilir.
- `AsNoTracking()`: sadece okuyacaksan hızlandırır (EF değişiklik takibi yapmaz).
- `Include(...)`: ilişkili veriyi aynı sorguda getirir.
- Bir kullanıcı silinirse abonelikleri de silinir (zorunlu ilişkide EF'in varsayılan davranışı).

**Sık hatalar:**
- **N+1:** Döngü içinde ilişkili veriye erişmek her satır için ayrı sorgu atar. Çözüm: `Include` veya `Select`.
- **Singleton DbContext:** yukarıda.
- **`ToList()` sonrası `Where`:** tüm tabloyu belleğe çeker. Filtre her zaman `ToList`'ten **önce**.

---

## 6. Migration

**Nedir:** Şema değişikliğini tarif eden, Git'e girebilen C# dosyası. Entity'yi değiştirdiğinde veritabanı bunu kendiliğinden bilmez.

**Neden:** "Veritabanı şu an nasıl?" sorusunun cevabı kodda durur. Herkes aynı şemaya sahip olur, geri alınabilir.

**İş akışı:**
1. Entity/DbContext'i değiştir.
2. Migration üret: `dotnet ef migrations add Ad -p src/Asistan.Infrastructure -s src/Asistan.Api -o Persistence/Migrations`
3. Veritabanına uygula: `dotnet ef database update -p src/Asistan.Infrastructure -s src/Asistan.Api`
4. Migration dosyasını **commit et**.

**`-p` ve `-s` nedir:** `-p` = migration'ların duracağı proje (Infrastructure). `-s` = ayarların okunacağı başlangıç projesi (Api, çünkü bağlantı bilgisi orada).

**Projede:** `20261119084848_IlkSema` var. Postgres'te `__EFMigrationsHistory` tablosu hangi migration'ların uygulandığını tutar.

**Dikkat:** Uygulanmış bir migration'ı düzenleme, yenisini ekle. Yanlış eklediysen ve henüz uygulamadıysan: `dotnet ef migrations remove`.

---

## 7. Unit test, Mock, AAA

**Nedir:** Kodun en küçük parçasını dış dünyadan yalıtıp otomatik sınayan kod.

**Test piramidi:**

| Tür | Neyi sınar | Hız | Projede |
|---|---|---|---|
| Unit | Tek sınıf, bağımlılıklar sahte | ms | 13 test var |
| Integration | Gerçek DB/HTTP ile birkaç parça | sn | Yok (ileride) |
| End-to-end | Tüm sistem | dk | Yok |

**AAA kalıbı:** Arrange (hazırla), Act (çalıştır), Assert (doğrula). İsim: `Metot_Durum_BeklenenSonuc`.

**Araçlar:**
- **xUnit:** `[Fact]` = tek test, `[Theory]` + `[InlineData]` = aynı test farklı girdilerle.
- **Moq:** `Setup` = "şu çağrılırsa şunu döndür", `Verify` = "bu metot kaç kez çağrıldı?"
- **FluentAssertions:** `x.Should().Be(...)`, `await islem.Should().ThrowAsync<...>()`.
- **coverlet:** kodun yüzde kaçı testle kapandığını ölçer.

**Neden Mock:** Gerçek veritabanı/Claude'a ihtiyaç kalmaz.

**Test kuralları:** Bir test tek şeyi doğrular. Testler bağımsızdır. Davranışı test et, iç detayı değil. Bug bulunca önce onu yakalayan testi yaz.

**Testlerin gerçekten çalıştığını nasıl biliriz?** Kodu bilerek boz, test kırmızıya dönmeli. (Aşama 4'te `ToLowerInvariant()` satırını silip denedik, test yakaladı.)

---

## 8. Docker

| Kavram | Ne | Projede |
|---|---|---|
| **Image** | Salt okunur paket (tarif) | `postgres:17` |
| **Container** | Image'ın çalışan kopyası (kek) | `aiasistan-postgres-1` |
| **Volume** | Container silinse de kalan kalıcı veri | `postgres_data` |
| **Port mapping** | `5432:5432` = bilgisayarın:container'ın | Bilgisayardan `localhost:5432` |
| **Network** | Container'ların birbirini adla bulması | Compose otomatik kurar |
| **Compose** | Birden çok container'ı tek dosyada tarif eder | `docker-compose.yml` |
| **Healthcheck** | "Servis gerçekten hazır mı?" kontrolü | `healthy` durumu |

**Container sanal makine değildir:** ayrı işletim sistemi taşımaz, bu yüzden saniyeler içinde açılır.

**Önemli ayrım:** Bilgisayarından `localhost:5432` ile bağlanırsın. Başka bir **container'ın içinden** ise servis adıyla (`postgres:5432`) bağlanılır. API şu an Docker dışında (Visual Studio'dan) çalıştığı için `localhost` kullanıyor.

**Volume'ün önemi:** `docker compose down` container'ı siler **ama veri kalır**. `docker compose down -v` volume'leri de siler: **tüm veri gider**.

---

## 9. PostgreSQL ve MongoDB: neden ikisi?

| | PostgreSQL (SQL) | MongoDB |
|---|---|---|
| Veri şekli | Tablo, satır, sabit şema | Koleksiyon, belge (JSON benzeri), esnek şema |
| İlişkiler | Güçlü (JOIN, foreign key) | Gömme (embed) veya referans |
| İyi olduğu veri | Kullanıcı, ödeme, abonelik | Sohbet, log, hafıza, katalog |
| Projede | **Kullanılıyor** (Aşama 3) | Henüz yok (Aşama 7) |

**Embed mi referans mı:** Birlikte okunan ve sınırlı veriyi iç içe koy. Sınırsız büyüyen veriyi (bir sohbetin binlerce mesajı) ayrı koleksiyona koy. Bir MongoDB belgesi en fazla 16 MB olabilir.

---

## 10. LiteLLM

**Nedir:** 100+ LLM sağlayıcısını tek, OpenAI uyumlu API arkasında toplayan açık kaynak araç. Biz **Proxy** (sunucu) olarak kullanıyoruz.

**Neden:**
- **Tek arayüz:** Kod hep `POST /v1/chat/completions` çağırır. Model değiştirmek = config değişikliği.
- **Fallback:** Claude yanıt vermezse otomatik GPT'ye geç.
- **Maliyet/bütçe takibi, sanal anahtarlar, rate limit.**

**Projede:** `litellm/config.yaml` takma adları (`akilli`, `hizli`) tanımlar. Kod `model = "akilli"` gönderir.

**İki anahtar karışmasın:**

| Anahtar | Kimin için | Nerede |
|---|---|---|
| `LITELLM_MASTER_KEY` | API → LiteLLM'e giriş | `.env` + user-secrets (`LiteLlm:Key`) |
| `ANTHROPIC_API_KEY` | LiteLLM → Anthropic'e giriş | `.env` |

**Mesaj rolleri:** `system` (modelin kimliği/kuralları), `user` (kullanıcı), `assistant` (modelin önceki cevabı; sohbet geçmişini böyle verirsin).

---

## 11. Gizli bilgiler (secrets)

**Kural:** Bir değer sızarsa zarar verir mi? Verirse **Git dışında**.

| Yer | Ne için | Git'e girer mi? |
|---|---|---|
| `appsettings.json` | Gizli olmayan ayarlar | Evet |
| `.env.example` | Şablon, sahte değerler | Evet |
| `.env` | Docker şifreleri, `ANTHROPIC_API_KEY` | **Hayır** |
| `user-secrets` | API'nin bağlantı cümlesi, LiteLLM anahtarı | **Hayır** (makineye özel) |
| Ortam değişkeni | Sunucuda (üretimde) | Hayır |

**.NET'te ortam değişkeni haritalama:** `Mongo__Url` (çift alt çizgi) = `appsettings`'teki `Mongo:Url`.

**Sızarsa:** Silmek yetmez, Git geçmişinde kalır. **Anahtarı hemen iptal et ve yenisini üret.**

**Yapma:** Anahtarı sohbete, e-postaya, ekran görüntüsüne yazma.

---

## 12. Git

**Üç alan:** `working directory` (düzenlediğin dosyalar) → `git add` → `staging area` → `git commit` → `repository` (kalıcı geçmiş). `git push` yerel geçmişi GitHub'a gönderir.

**Conventional Commits:** `tür: kısa açıklama`: `feat:` yeni özellik, `fix:` hata düzeltme, `test:`, `docs:`, `ci:`, `chore:`, `refactor:`. Geçmiş okunur olur.

**Bu projenin commit geçmişi:**
```
chore: solution iskeleti
feat: docker compose ile postgres, mongo, rabbitmq
feat: kullanici entity, EF Core, ilk migration ve /api/kullanicilar
test: KullaniciServisi icin unit testler
ci: github actions ile build ve test workflow'u
feat: LiteLLM istemcisi, SohbetServisi ve POST /api/sohbet
docs: README ve gercek .http ornekleri
docs: ogrenme rehberi (docs klasoru)
```

**Branch / PR akışı (GitHub Flow):** `main` her zaman çalışır durumda. Her iş için branch aç → küçük commit'ler → Pull Request → CI yeşil + inceleme → merge. Şimdilik doğrudan `main`'e commit atıyoruz (tek kişi, öğrenme aşaması); ekip çalışmasında bu yapılmaz.

**Satır sonu uyarısı:** `LF will be replaced by CRLF` Windows'ta zararsız bir uyarıdır.

---

## 13. CI (Continuous Integration)

**Nedir:** Her push/PR'da kodu otomatik derleyip test etmek.

**Neden:** Kırık kod `main`'e girmesin. İnsan unutur, makine unutmaz.

**Projede:** `.github/workflows/ci.yml`. Sonucu GitHub'da repo → **Actions** sekmesinde görürsün (yeşil tik = geçti).

**GitHub Actions sözlüğü:** Workflow (yml dosyası), Trigger (`on:`), Job (aynı makinede adımlar), Step (`run:` komut veya `uses:` hazır action), Runner (makine), Secret (gizli değer).

**Henüz yapılmayanlar:** Branch protection (testler geçmeden merge'ü engelleme), CD (otomatik yayın), integration test.

---

## 14. Henüz yazılmamış ama planda olanlar

| Konu | Neden gerekli | Aşama |
|---|---|---|
| **MongoDB sohbet geçmişi** | Mesajları saklamak, modele geçmişi vermek | 7 |
| **AI Memory** | Modeller durumsuz: hafıza = her istekte gönderdiğin metin. Kısa süreli (son N mesaj + özet) ve uzun süreli (kalıcı bilgiler) | 8 |
| **RabbitMQ + Worker** | Claude 10-60 sn sürebilir; API kuyruğa atıp hemen 202 döner, Worker işler | 9 |
| **LangFlow** | Karmaşık AI akışlarını (RAG, agent) görsel kurmak | 10 |
| **CD, Skills, MCP** | Otomatik yayın; Claude'a tekrarlayan işleri öğretmek; Claude'u dış araçlara bağlamak | 11 |

**RabbitMQ ana fikri:** Producer (API) mesajı bırakır, Consumer (Worker) alıp işler, `Ack` = "işledim". Mesaj en az bir kez işlenir, bu yüzden consumer **idempotent** yazılmalı (aynı mesaj iki kez gelse sonuç değişmemeli). Hata olursa üstel bekleme ile yeniden denenir, olmazsa `_error` kuyruğuna düşer.

**Hafıza ana fikri:** Neyi, ne kadar göndereceğini yönetmek = bağlam penceresine en faydalı bilgiyi en az token'la sığdırma sanatı (maliyet + kalite).
