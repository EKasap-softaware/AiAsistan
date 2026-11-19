# 03 · Bir isteğin yolculuğu ve aşama günlüğü

## A. Yolculuk 1: Kullanıcı oluşturma (`POST /api/kullanicilar`)

İstek:
```json
{ "ad": "Emre", "email": "Emre@Test.com" }
```

```
1. İstemci ──► Kestrel (ASP.NET'in web sunucusu) :5254
2. Middleware hattı (HTTPS yönlendirme, yetkilendirme)
3. Yönlendirme: POST /api/kullanicilar ──► KullanicilarController.Olustur
4. DI: controller'ın istediği IKullaniciServisi ──► KullaniciServisi (zincir otomatik kurulur)
5. KullaniciServisi.OlusturAsync
      a. Ad boş mu? E-posta geçerli mi?          ──► değilse ArgumentException
      b. E-postayı  Trim + küçük harf yap         ──► "emre@test.com"
      c. depo.EmailVarMiAsync(email)              ──► varsa EmailZatenVarException
      d. new Kullanici { Ad, Email }
      e. depo.EkleAsync(kullanici)
6. KullaniciDeposu.EkleAsync
      db.Kullanicilar.Add(...) + SaveChangesAsync ──► Npgsql ──► PostgreSQL (INSERT)
7. Controller sonucu HTTP koduna çevirir:
      başarı            ──► 201 Created  (+ Location başlığı)
      ArgumentException ──► 400 Bad Request
      EmailZatenVar     ──► 409 Conflict
```

Yanıt (201):
```json
{ "id": 1, "ad": "Emre", "email": "emre@test.com", "kayitTarihi": "2026-11-19T08:49:05Z" }
```

**Dikkat edilecek yer:** 5b'de e-posta küçük harfe çevriliyor ve 5c bu hâliyle kontrol ediliyor. Yani `EMRE@test.com` ile `emre@test.com` aynı kişi sayılır. Veritabanındaki unique index de ikinci güvence.

---

## B. Yolculuk 2: Claude'a soru sorma (`POST /api/sohbet`, Aşama 9'dan beri **asenkron**)

İstek:
```json
{ "kullaniciId": "1", "soru": "Dependency Injection nedir?", "oturumId": "(isteğe bağlı)" }
```

Bu yolculuk artık **iki program** arasında geçer: API (garson) ve Worker (mutfak). Aralarında RabbitMQ (askı).

**Bölüm 1: API (<0.3 sn, cevap beklemez)**
```
1. İstemci ──► API :5254 ──► SohbetController.Sor
2. IsServisi.KuyrugaAlAsync
      a. Kullanıcı/soru boş mu?  ──► evet: 400 (hiçbir şey yazılmaz)
      b. isId üret (Guid), oturumId yoksa üret
      c. MongoDB "isler": takip fişi yaz  { durum: "Bekliyor" }          ◄── ÖNCE fiş
      d. RabbitMQ "ai-istegi" kuyruğuna AiIstegi mesajını gönder (Send)    ◄── SONRA kuyruk
         (5 sn içinde olmazsa: fişi "Hata" yap ──► 503)
3. Controller: 202 Accepted  { isId, oturumId, durum: "Bekliyor", durumUrl }
```

**Bölüm 2: Worker (kendi hızında)**
```
4. RabbitMQ ──► AiIstegiConsumer (kuyruk: ai-istegi)
      a. IslemeAlAsync(isId): atomik "al"  { durum: "Isleniyor", deneme+1 }
           iş zaten Tamamlandi/Hata/yok ise ──► ATLA, mesajı onayla (idempotent)
      b. SohbetServisi.SorAsync:
           ├─ HafizaServisi.BaglamKurAsync   (MongoDB: hafızalar + özet + ham mesajlar)
           ├─ LiteLlmIstemcisi ──► LiteLLM ──► Claude        (10-60 sn sürebilir)
           └─ MongoDB "mesajlar": soru ve cevabı kaydet
      c. TamamlaAsync: fiş { durum: "Tamamlandi", cevap: "..." }       ◄── kullanıcı artık görebilir
      d. RabbitMQ "sohbet-tamamlandi" kuyruğuna SohbetTamamlandi gönder
      (hata fırlarsa: yeniden dene, bkz. aşağıdaki "Hata yolu")
5. SohbetTamamlandiConsumer (kuyruk: sohbet-tamamlandi)  ── arka plan işi
      HafizaServisi.SohbetSonrasiAsync:
         ├─ eşik aşıldıysa eski mesajları "hizli" modelle özetle ──► "oturumOzetleri"
         └─ kullanıcı hakkında kalıcı bilgi var mı? ──► "hafizalar"
```

**Bölüm 3: kullanıcı cevabı alır (polling)**
```
6. İstemci belirli aralıklarla:  GET /api/isler/{isId}
      { durum: "Bekliyor" }  ──►  { durum: "Isleniyor" }  ──►  { durum: "Tamamlandi", cevap: "..." }
```

**Doküman modu (Aşama 10): aynı yolculuk, Worker'daki adım 4b farklı**
```
POST /api/sohbet  { ..., "mod": "dokuman" }   (mod yoksa "sohbet")
   API: mod doğrulanır (bilinmeyen ──► 400), fiş ve kuyruk mesajı "mod" taşır
Worker, AiIstegiConsumer:
   mod == "dokuman" ──► DokumanServisi.SorAsync
        ├─ LangFlowIstemcisi ──► POST LangFlow /api/v1/run/dokuman-asistani
        │      LangFlow akışı: /projedocs/*.md oku ─► birleştir ─► prompt ─► LiteLLM ─► Claude
        └─ soru + cevap MongoDB "mesajlar"
   (hafıza/özet adımı YOK: SohbetTamamlandi mesajı gönderilmez)
```
Cevap, dokümanda yoksa "Bu bilgi dokümanlarda yok." olur (prompt'taki kural).

**Hata yolu (model/ağ çökerse):**
```
Worker denemesi başarısız ──► 5 kez yeniden dene (bekleme süresi artar: ~11, 17, 22, 34 sn)
   hâlâ başarısızsa:
     ├─ asıl mesaj "ai-istegi_error" kuyruğuna TAŞINIR (silinmez, sonra incelenir)
     ├─ Fault<AiIstegi> yayınlanır ──► AiIstegiHataConsumer: fiş { durum: "Hata" }
     └─ gerçek neden SADECE loga yazılır; kullanıcı "Soru işlenemedi, lütfen tekrar dene." görür
```

**Üç gerçek senaryo (hepsi canlı sistemde denendi):**
| Senaryo | Ne oldu |
|---|---|
| Worker kapalıyken soru | API yine 202 (0.25 sn). Soru kuyrukta bekledi. Worker açılınca ~5 sn'de cevap geldi |
| LiteLLM kapalıyken soru | 6 deneme, ~2 dk, sonunda `Hata`, mesaj `_error` kuyruğunda |
| RabbitMQ kapalıyken soru | API **503** (5 sn). RabbitMQ açılınca her şey kendiliğinden toparlandı |

**Bu akışın zayıf noktaları (bilerek, ileriki aşamalarda çözülecek):**
- **Polling:** kullanıcı cevabı kendisi sormak zorunda (anlık itme için SignalR/webhook gerekir).
- **Outbox yok:** fişi yazıp kuyruğa göndermek iki ayrı iş; ikisi arasında uygulama çökerse fiş `Bekliyor` kalır.
- **Uzun süreli hafıza anlamsal değil:** soruyla ilgili olanlar seçilmiyor, kullanıcının en yeni 20 hafızası hep gidiyor (embedding yok).
- **`kullaniciId` sadece kayıt için saklanıyor**; kimlik doğrulama yok (herkes her oturumu ve hafızayı okuyabilir).
- **Aynı oturuma aynı anda iki soru:** sıra garantisi ve özetleme yarışı olabilir.

## C. Aşama günlüğü: neyi, neden, hangi sırayla yaptık

### Aşama 1: İskelet · commit `chore: solution iskeleti`
- `dotnet new sln` + 5 proje + 1 test projesi, referanslar, `Directory.Build.props`, `.gitignore`, `CLAUDE.md`, git repo (`main`).
- Şablonlardan gelen `Class1.cs`, `WeatherForecast` dosyaları silindi.
- **Sonuç:** `dotnet build` yeşil.

### Aşama 2: Docker altyapısı · `feat: docker compose ile postgres, mongo, rabbitmq`
- `docker-compose.yml`, `.env`, `.env.example`. Portlar önceden boş mu diye kontrol edildi.
- Sürümler sabitlendi (`latest` yok), healthcheck eklendi.
- **Doğrulama:** Postgres `select 1`, Mongo `ping`, RabbitMQ API cevap verdi.

### Aşama 3: EF Core + ilk API · `feat: kullanici entity, EF Core, ilk migration ve /api/kullanicilar`
- Domain → Application → Infrastructure → Api sırasıyla yazıldı (içten dışa).
- Bağlantı cümlesi `user-secrets`'a konuldu (dosyaya değil).
- `dotnet-ef` proje-yerel araç olarak kuruldu (global kurulum "tool restore" hatası verdi).
- Scalar'ın son sürümü .NET 9 ile uyumsuz çıktı → 2.12.* sabitlendi.
- **Doğrulama:** 201 / 409 / 400 / 200 / 404 gerçek isteklerle denendi; veritabanında satır görüldü.

### Aşama 4: Unit test · `test: KullaniciServisi icin unit testler`
- Moq + FluentAssertions 7.x (8. sürüm ticari).
- **Mutasyon kontrolü:** `ToLowerInvariant()` silindi → test kırıldı → geri yüklendi.

### Aşama 5: CI · `ci: github actions ile build ve test workflow'u`
- Workflow yazıldı; adımlar yerelde birebir çalıştırıldı (Release build 0 uyarı, testler yeşil).
- Repo `https://github.com/EKasap-softaware/Caricep_yeni` (boştu) → `main` push edildi. Push öncesi `.env`'nin Git'e girmediği doğrulandı.
- **Açık iş:** Actions sekmesinden ilk çalışmanın yeşil olduğunu görmek; branch protection.

### Aşama 6: LiteLLM · `feat: LiteLLM istemcisi, SohbetServisi ve POST /api/sohbet`
- `litellm/config.yaml`, compose'a `litellm` servisi, `.env`'ye iki yeni değişken.
- `ILlmIstemcisi` / `SohbetServisi` / `LiteLlmIstemcisi` / `SohbetController`, 4 yeni test.
- `Microsoft.Extensions.Http` paketi eksikti (`AddHttpClient` bulunamadı) → eklendi.
- **Doğrulama:** Önce kredi yokken `credit balance is too low` hatası alındı (zincir çalışıyordu). 5 dolar kredi yüklenince hem LiteLLM'e doğrudan istek hem `POST /api/sohbet` (200 + Claude cevabı) ve boş soru (400) denendi.

### Aşama 7: MongoDB sohbet geçmişi · `feat: MongoDB ile sohbet gecmisi`
- `MongoDB.Driver` ve `Microsoft.Extensions.Hosting.Abstractions` paketleri; bağlantı cümlesi `user-secrets`'a (`Mongo:Url`).
- Domain: `Mesaj`. Application: `IMesajDeposu`, `SohbetServisi` güncellendi (oturum, kayıt, `SohbetSonucu`). Infrastructure: `MongoMesajDeposu`, `MongoSinifHaritalari`, `MongoIndexServisi`, DI kayıtları. Api: `oturumId` desteği + `GET /api/sohbet/{oturumId}/mesajlar`.
- Domain'i MongoDB'ye bağlamamak için ObjectId/camelCase kuralları attribute yerine **class map + convention** ile Infrastructure'da.
- İlk denemede alan adları `OturumId` (PascalCase) çıktı; MongoDB camelCase beklenir ve sorgular büyük/küçük harf duyarlıdır → `CamelCaseElementNameConvention` eklendi, deneme kayıtları silinip yeniden denendi.
- **Doğrulama:** İki soru aynı oturumda soruldu; `GET .../mesajlar` 4 mesajı doğru sırada döndü; `mongosh` ile `_id` gerçek `ObjectId`, index `oturumId_1_olusturulma_-1`; boş soru 400 ve kayıt yazılmadı. Testler 13 → 18.

### Aşama 8: AI Memory · `feat: AI Memory (ozetleme + kalici hafiza)`
- Domain: `Hafiza`, `OturumOzeti`. Application: `Bellek/` klasörü (`IHafizaDeposu`, `IOturumOzetDeposu`, `HafizaAyarlari`, `HafizaServisi`); `IMesajDeposu`'na `SayAsync` ve `AralikGetirAsync`; `SohbetServisi` artık bağlamı `HafizaServisi`'nden alıyor. Infrastructure: `MongoHafizaDeposu`, `MongoOturumOzetDeposu`, class map'ler ve 2 yeni index. Api: `HafizaController` (görme ve silme hakkı), `Hafiza` ayar bölümü.
- Namespace'e `Hafiza` dersek `Hafiza` sınıfıyla çakışırdı → `Asistan.Application.Bellek` oldu.
- **Test yaklaşımı:** `HafizaAyarlari`'yı küçük değerlerle (pencere 2, eşik 4) test ettik. Mutasyon kontrolü: özetleme sınırını bilerek bozunca 3 test kırıldı. Testler 18 → 37.
- **Gerçek Claude ile doğrulama** (geçici olarak pencere 2, eşik 4 ile): (1) "Ben Emre, C# öğreniyorum, hedefim web API" → hafızaya "Kullanıcının adı Emre; C# öğrenmeye yeni başladı ve hedefi web API yazmak." yazıldı. (2) Dört soru sonra özet oluştu (`ozetlenenMesajSayisi: 8`); "ilk mesajımda ne söylemiştim?" sorusuna doğru cevap geldi. (3) Aynı kullanıcı **yeni oturumda** "adımı hatırlıyor musun?" dedi, cevap "adın Emre, C# öğreniyorsun". (4) **Başka kullanıcı** aynı soruyu sorunca bilgi sızmadı ("hatırlayamıyorum"). (5) Hafıza silme çalıştı; başka kullanıcının silme denemesi 404 döndü ve hafıza yerinde kaldı. Test verileri sonra temizlendi.

### Aşama 9: RabbitMQ + Worker · `feat: RabbitMQ + Worker (asenkron sohbet, 202 + takip fisi)`
- **Lisans kontrolü:** MassTransit 8.5.12 paket künyesinde `Apache-2.0` (ücretsiz), `net9.0` destekli. 9+ ticari olduğu için `8.*` seçildi.
- Domain: `IsKaydi` + `IsDurumu`. Application: `Kuyruk/` (sözleşmeler, `IsServisi`), `AddApplication`. Infrastructure: `MongoIsDeposu` (atomik `IslemeAlAsync`, TTL index), `MassTransitIsYayinci`, `AddKuyruk`. Worker: 3 consumer + kuyruk/retry tanımı (şablon `Worker.cs` silindi). Api: `SohbetController` artık 202 döner, yeni `IslerController`.
- `SohbetServisi`'nden özetleme/hafıza çıkarma işi çıkarıldı (Worker'da ayrı adım).
- API ve Worker aynı kasayı kullanır (aynı `UserSecretsId`); Worker için de yedek `appsettings.Local.json` oluşturuldu.
- **Test:** 45 → 60 (`IsServisiTests`, `WorkerConsumerTests`). Consumer'lar RabbitMQ olmadan, sahte `ConsumeContext` ile test edildi.
- **Canlı doğrulama (3 senaryo, bkz. Yolculuk 2):** Worker kapalı, LiteLLM kapalı, RabbitMQ kapalı. Üçüncüsü **gerçek bir kusur buldu**: API 70+ sn asılı kalıyor ve fiş sonsuza dek `Bekliyor` kalıyordu. Düzeltme: gönderime 5 sn sınır (`WaitAsync`), başarısızsa fiş `Hata`, API 503; iki test eklendi. Düzeltilmiş kodla yeniden denendi: 503 (5.1 sn), RabbitMQ geri gelince her şey toparlandı.
- Test verileri ve `_error` kuyruğu temizlendi.

### Aşama 10: LangFlow · `feat: LangFlow dokuman modu (akis, istemci, guvenlik ayarlari)`
- **Servis:** `docker-compose.yml`'a LangFlow 1.13.3 (sürüm sabit, giriş zorunlu, `.env`'de süper kullanıcı/şifre/gizli anahtar). `./docs` → container'da `/projedocs` (salt-okunur).
- **Akış:** API'den kodla kuruldu: `Chat Input, Directory, Parser, Prompt Template, OpenAI (LiteLLM'e bağlı), Chat Output`. Anahtar akışa gömülmedi: `LITELLM_KEY` gizli değişkeni. Akış `langflow/dokuman-asistani.json` olarak export edildi (içinde hiçbir sır olmadığı **taranarak** doğrulandı) ve `scripts/langflow-akisi-yukle.ps1` ile geri yüklenebildiği **canlı test edildi**.
- **.NET:** `ILangFlowIstemcisi`/`LangFlowIstemcisi`, `DokumanServisi`, `SohbetModlari`; `AiIstegi`/`IsKaydi`'ne `Mod`; `AiIstegiConsumer` mod'a göre yönlendirir; API `mod` kabul eder (bilinmeyen → 400).
- **Yolda yaşanan 6 gerçek sorun** (hepsi `05-sorun-giderme.md` #18-#23 ve `dersler/01-langflow.md`'de): LangFlow veritabanı volume'de değildi, container yenilenince akışlar silindi; Directory izinli klasör ayarı JSON listesi olarak metin okundu; yerel dosya erişimi kısıtı; SSRF koruması `litellm` adını engelledi; LiteLLM `seed` parametresi için `drop_params` istedi; LangFlow veritabanı sıfırlanınca API anahtarı geçersizleşti.
- **Test:** 60 → 74. **Canlı doğrulama:** dokümanda olan soru doğru ve kaynak dosyalarla cevaplandı; dokümanda **olmayan** soru ("2027 pazarlama bütçesi") "Bu bilgi dokümanlarda yok." döndü (uydurmadı); normal sohbet etkilenmedi; bilinmeyen mod 400. Dersler klasörünün asistan tarafından **okunmadığı** da soruyla doğrulandı.
- **Ders:** `docs/dersler/` klasörü başladı (`00-ders-plani.md`, `01-langflow.md`).

### Aşama 11: Docker, CD, Skill, MCP · `feat: docker imajlari, CD, yeni-entity skill ve MCP`
- **Docker:** `Dockerfile` (Api, Worker, multi-stage), `.dockerignore`, compose'a `api`/`worker` servisleri (`profiles: ["uygulama"]`, YAML çapası ile ortak ortam bloğu). İmajlar 362 MB / 325 MB; katman önbelleği ölçüldü (1.3 / 11.9 / 26.5 sn); imajda gizli dosya yok, `uid=app`.
- **Canlı doğrulama (container'larda):** PostgreSQL'e `postgres` adıyla bağlandı, Worker `rabbitmq` adıyla bağlandı, normal sohbet ve doküman modu uçtan uca çalıştı. Doküman modunda "Docker neden multi-stage" sorusuna asistan **"Bu bilgi dokümanlarda yok."** dedi (Docker henüz dokümanda yoktu; uydurmadı).
- **CI/CD:** `ci.yml`'a `docker` job'u; yeni `cd.yml` (imaj → `ghcr.io`, migration bundle, güvenlik düğmeli yayın). `actionlint` ile denetlendi ve **bilerek bozuk bir workflow'la sınandı**; ayrıca GitHub'da çalıştığı **doğrulanamadı** (Actions sonuçları bu oturumdan görünmüyor, bkz. `dersler/09-ci-cd.md`).
- **Gerçek bulgu:** Migration bundle ilk denemede, `Program.cs`'teki zorunlu ayar denetimi yüzünden (Mongo/RabbitMQ/LangFlow sırları olmadan) hata verdi. Çözüm: `AppDbContextFabrikasi` (tasarım zamanı fabrikası). Sonuç: ortamda sır yokken bile dolu veritabanında "güncel", boş veritabanında şemayı sıfırdan kurdu.
- **Claude CLI tarafı:** `CLAUDE.md` yeniden yazıldı, `.claude/skills/yeni-entity/SKILL.md` (komutları gerçekten çalıştırılarak sınandı), `.mcp.json`. MongoDB için **salt-okunur kullanıcı** (`mcp_okuyucu`) yaratıldı: yazma denemesi veritabanı düzeyinde `Unauthorized`. MCP sunucusu protokolle konuşturuldu (initialize → 20 araç → list-collections → count; `insert-one` yok). GitHub MCP denenmedi (token yok).
- **Dersler:** `dersler/05-docker.md`, `09-ci-cd.md`, `10-claude-cli-skills-mcp.md`.

### Temizlik · `docs: README ve gercek .http ornekleri`, `docs: ogrenme rehberi`
- Şablondan kalan hatalı `.http` dosyası gerçek isteklerle değiştirildi, README ve bu rehber eklendi.

---

## D. Sırada ne var

Rehberin 11 aşaması tamam. Yapılabilecek sonraki işler (hepsi bilinen sınırlardan):

| İş | Neden |
|---|---|
| Sunucuya gerçek yayın | CD hazır ama sunucu yok; `DEPLOY_ENABLED` kapalı |
| Integration test (Testcontainers) | Gerçek Postgres/Mongo/RabbitMQ ile otomatik test |
| Kimlik doğrulama | Şu an `kullaniciId`'yi bilen herkes o kullanıcının verisini görür |
| Gerçek RAG | Dokümanlar büyüyünce embedding + vektör arama |
| Kendi MCP sunucumuz | Hafızayı Claude'a araç olarak açmak |
| Transactional outbox | Fiş yazma ile kuyruğa gönderme arasındaki çökme boşluğu |

**Her aşamadaki çalışma ritmi:** yeni branch → rehberdeki bölümü oku → kodu yaz → test yaz → `dotnet test` → commit → push → PR → CI yeşil → merge → "ne öğrendim?" sorusunu 3 cümleyle yanıtla.
