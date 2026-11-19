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

## B. Yolculuk 2: Claude'a soru sorma (`POST /api/sohbet`)

İstek:
```json
{ "kullaniciId": "1", "soru": "Dependency Injection nedir?", "oturumId": "(isteğe bağlı)" }
```

```
1. İstemci ──► API :5254 ──► SohbetController.Sor
2. DI: ISohbetServisi ──► SohbetServisi (ILlmIstemcisi, IMesajDeposu, IHafizaServisi takılır)
3. SohbetServisi.SorAsync
      a. Soru boş mu?                              ──► evet: ArgumentException (LLM çağrılmaz)
      a2. oturumId yoksa yeni bir tane üret (Guid)
      b. IHafizaServisi.BaglamKurAsync(kullaniciId, oturumId, soru):
           ├─ MongoDB "hafizalar"      : kullanıcının kalıcı bilgileri (en yeni 20)
           ├─ MongoDB "oturumOzetleri" : bu oturumun özeti (varsa)
           └─ MongoDB "mesajlar"       : özete girmemiş ham mesajlar
         ──► [ system: temel prompt + "Kullanıcı hakkında bildiklerin: ..." + "Önceki kısmın özeti: ..."
               (user/assistant): ham mesajlar ...
               user: yeni soru ]
      c. llm.TamamlaAsync(baglam)  (model varsayılan: "akilli")
4. LiteLlmIstemcisi
      POST http://localhost:4000/v1/chat/completions
      Authorization: Bearer <LiteLlm:Key>
      { "model": "akilli", "messages": [ {role, content}, ... ] }
5. LiteLLM (Docker)
      a. Master key'i doğrular
      b. config.yaml: "akilli" ──► anthropic/claude-sonnet-5-5
      c. ANTHROPIC_API_KEY ile Anthropic'e iletir
6. Claude cevaplar ──► LiteLLM ──► OpenAI formatında JSON:
      { "choices": [ { "message": { "content": "..." } } ] }
7. LiteLlmIstemcisi: choices[0].message.content'i okur
7b. SohbetServisi: soru ve cevabı IMesajDeposu ile kaydeder
      MongoMesajDeposu ──► InsertMany ──► MongoDB "mesajlar" koleksiyonu
      (LLM hata verdiyse buraya hiç gelinmez, kayıt yapılmaz)
7c. IHafizaServisi.SohbetSonrasiAsync (hata verse de kullanıcının cevabını bozmaz):
      a. Özetlenmemiş mesaj sayısı eşiği (varsayılan 20) aştı mı?
           evet ──► en yeni 10 mesaj hariç eskileri "hizli" modelle özetle
                    ──► MongoDB "oturumOzetleri" (upsert)
      b. "hizli" modele sor: "kullanıcı hakkında kalıcı yeni bilgi var mı?"
           varsa (ve YOK değilse, kısaysa, yeniyse) ──► MongoDB "hafizalar"
8. Controller: 200 OK  { "cevap": "...", "oturumId": "..." }
      HttpRequestException ──► 502 Bad Gateway
```

**Durum:** Zincir uçtan uca çalışıyor. Kredi yüklendikten sonra `/api/sohbet`'e "Dependency Injection nedir?" sorusu gönderildi, Claude'dan Türkçe cevap geldi (HTTP 200). Boş soru ise 400 döndü.

**Bu akışın zayıf noktaları (bilerek, ileriki aşamalarda çözülecek):**
- **Senkron:** Cevap 10-60 sn sürebilir, bağlantı o sürece bekler → Aşama 9'da kuyruk.
- **Özetleme ve hafıza çıkarma senkron:** her soruda 1-2 ekstra `hizli` model çağrısı (gecikme + maliyet) → Aşama 9'da Worker'a taşınacak.
- **Uzun süreli hafıza anlamsal değil:** soruyla ilgili olanlar seçilmiyor, kullanıcının en yeni 20 hafızası hep gidiyor (embedding/vektör arama yok). Çelişen bilgi eskisini güncellemez; kullanıcı `DELETE /api/hafiza/...` ile siler.
- **Aynı oturuma aynı anda iki istek** gelirse özetleme yarışabilir.
- **`kullaniciId` sadece kayıt için saklanıyor**; kimlik doğrulama/yetkilendirme yok (herkes her oturumu okuyabilir).
- **Kayıt başarısız olursa** (MongoDB kapalı) kullanıcı cevabı değil hata alır; cevap boşa gitmiş olur (kuyruk + yeniden deneme ile Aşama 9'da düzelecek).
- LiteLLM hataları 5xx olunca controller hepsini 502 yapar; ayrım yok.

---

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

### Temizlik · `docs: README ve gercek .http ornekleri`, `docs: ogrenme rehberi`
- Şablondan kalan hatalı `.http` dosyası gerçek isteklerle değiştirildi, README ve bu rehber eklendi.

---

## D. Sırada ne var

| Aşama | Yapılacak | Çıktı |
|---|---|---|
| 9 | RabbitMQ + MassTransit + Worker | API anında 202, cevap Worker'dan |
| 10 | LangFlow RAG akışı | Asistan dokümanlardan cevap veriyor |
| 11 | Dockerfile, CD, skill, MCP | `main`'e merge = otomatik yayın |

**Her aşamadaki çalışma ritmi:** yeni branch → rehberdeki bölümü oku → kodu yaz → test yaz → `dotnet test` → commit → push → PR → CI yeşil → merge → "ne öğrendim?" sorusunu 3 cümleyle yanıtla.
