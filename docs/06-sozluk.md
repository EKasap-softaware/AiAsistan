# 06 · Sözlük

Alfabetik. Soldaki terim, sağda kısa ve sade açıklama.

| Terim | Anlamı |
|---|---|
| **Ack** | RabbitMQ'da consumer'ın "mesajı işledim, silebilirsin" onayı. |
| **Action (GitHub)** | Workflow'un bir adımında kullanılan hazır araç (`uses: actions/checkout@v4`). |
| **Aggregation** | MongoDB'de verileri aşama aşama işleyen sorgu (SQL'deki GROUP BY'ın karşılığı). |
| **API** | Programların birbiriyle konuşma arayüzü. Bizimki HTTP üzerinden JSON konuşur. |
| **appsettings.json** | Ayar dosyası. Gizli bilgi **konmaz**. |
| **Arayüz (interface)** | "Bu sınıf şu metotları sunar" sözleşmesi; içi yoktur. |
| **ASP.NET Core** | .NET'in web tarafı: API ve web sitesi yapar. |
| **async / await** | Beklerken thread'i serbest bırakan yazım biçimi. |
| **Backoff (exponential)** | Hata sonrası 1 sn, 2 sn, 4 sn... diye artan bekleyişle yeniden deneme. |
| **Bağlam penceresi (context window)** | Modelin tek istekte okuyabildiği en fazla token miktarı. Hafıza yönetimi bu pencereyi verimli doldurmaktır. |
| **Binding (RabbitMQ)** | Exchange ile kuyruk arasındaki yönlendirme kuralı. |
| **Branch** | Ana koddan ayrılmış paralel çalışma dalı. |
| **Build** | Kodu derleyip çalıştırılabilir hale getirme. |
| **BSON** | MongoDB'nin belge saklama biçimi (ikili JSON). |
| **camelCase** | İlk harfi küçük, sonraki kelimeler büyük yazım (`oturumId`). MongoDB alan adlarında yaygın. |
| **CD** | Continuous Delivery/Deployment: testten sonra yayını otomatikleştirme. |
| **CI** | Continuous Integration: her push'ta otomatik derleme ve test. |
| **Clean Architecture** | Bağımlılıkların içe doğru aktığı katmanlı yapı. |
| **Class map (MongoDB)** | Bir C# sınıfının MongoDB'de nasıl saklanacağını (Id türü, alan adları) kodla tarif etmek; sınıfa attribute koymaktan temizdir. |
| **Collection (Koleksiyon)** | MongoDB'de tablonun karşılığı (`mesajlar`). |
| **Commit** | Git'te kalıcı kayıt noktası. |
| **Compose** | Birden çok Docker container'ını tek dosyada tarif etme aracı. |
| **Conflict (Git)** | İki kişi aynı satırı değiştirdiğinde Git'in sorması. HTTP'de `409`. |
| **Consumer** | Kuyruktan mesaj alıp işleyen taraf (bizde Worker). |
| **Container** | Image'ın çalışan kopyası. |
| **Controller** | HTTP isteğini alan sınıf; iş kuralını servise yaptırır. |
| **Conventional Commits** | `feat:`, `fix:`, `test:` ile başlayan commit mesajı kuralı. |
| **Coverage** | Kodun yüzde kaçının testle çalıştırıldığı. |
| **DbContext** | EF Core'da veritabanının C# karşılığı. |
| **DbSet** | DbContext içinde bir tabloyu temsil eden koleksiyon. |
| **Dead-letter queue (DLQ)** | Defalarca başarısız olan mesajların gittiği kuyruk. MassTransit'te `<kuyruk>_error` (bizde `ai-istegi_error`). |
| **202 Accepted** | "İsteği aldım, işlenecek, henüz bitmedi." Sonucu başka bir adresten sorarsın (bizde `/api/isler/{isId}`). |
| **At-least-once** | Mesaj en az bir kez teslim edilir, bazen iki kez. Bu yüzden consumer idempotent olmalı. |
| **Fault (MassTransit)** | Bir mesaj tüm denemelere rağmen işlenemeyince yayınlanan "hata olayı" mesajı. |
| **Outbox (transactional outbox)** | "Veritabanına yaz + kuyruğa gönder" işini tek bütün yapan desen. Bizde yok (bilinen sınır). |
| **Polling** | İstemcinin sonucu hazır olana kadar belirli aralıklarla sormasıdır. |
| **Retry (yeniden deneme)** | Geçici hatada işi tekrar denemek. Bizde 5 kez, bekleme süresi artarak. |
| **Send / Publish** | Send: doğrudan bir kuyruğa gönder (kuyruk yoksa oluşur). Publish: bir exchange'e bırak, ilgilenen kuyruklara dağılır (kimse yoksa mesaj kaybolur). |
| **Takip fişi** | `IsKaydi`: kuyruğa atılan sorunun durumunu (Bekliyor, Isleniyor, Tamamlandi, Hata) ve cevabını tutan kayıt. |
| **Zaman aşımı (timeout)** | Bir işlemin en fazla ne kadar bekleyeceğinin sınırı. Dış sisteme giden her çağrıda olmalı. |
| **Dependency Injection (DI)** | Nesneyi kendin yaratmak yerine dışarıdan almak. |
| **Document (Belge)** | MongoDB'de satırın karşılığı: JSON benzeri bir kayıt. |
| **Docker** | Uygulamayı bağımlılıklarıyla paketleyip her yerde aynı çalıştıran araç. |
| **Dockerfile** | Image'ın nasıl inşa edileceğini anlatan dosya. |
| **Multi-stage build** | Dockerfile'da iki aşama: büyük imajda derle, küçük imajda sadece çıktıyı çalıştır. İmajı küçültür ve güvenli yapar. |
| **Katman önbelleği (layer cache)** | Docker her Dockerfile satırını bir katman olarak saklar; değişmeyen katman yeniden üretilmez. Sıra önemli: seyrek değişen (paketler) önce, sık değişen (kod) sonra. |
| **Profil (compose profile)** | Bir servisi isteğe bağlı yapar: `profiles: ["uygulama"]` olan servis sadece `--profile uygulama` ile başlar. |
| **YAML çapası (`&` / `*`)** | YAML'da bir bloğa ad verip (`&ad`) başka yerde aynen yeniden kullanmak (`*ad`). |
| **ghcr.io** | GitHub Container Registry: GitHub'ın imaj deposu. |
| **Matrix (GitHub Actions)** | Aynı job'u farklı değerlerle (api, worker) paralel çalıştırmak. |
| **workflow_run** | Bir workflow başka bir workflow bitince çalışsın diye kullanılan tetikleyici (CD, CI'dan sonra). |
| **Migration bundle** | Veritabanı şemasını güncelleyen tek bir çalıştırılabilir dosya. Üretimde uygulama açılışında migrate etmekten güvenlidir. |
| **Design-time factory** | EF araçlarına `DbContext`'in nasıl üretileceğini söyleyen sınıf (`IDesignTimeDbContextFactory`); uygulamanın `Program.cs`'inden bağımsız. |
| **actionlint** | GitHub Actions workflow dosyalarının hatalarını (yanlış alan, eksik sürüm) bulan denetleyici. |
| **Domain** | Saf iş kavramları ve veri sınıfları katmanı. |
| **dotnet CLI** | `dotnet build`, `dotnet test` gibi komut satırı aracı. |
| **Embed (MongoDB)** | İlişkili veriyi belgenin içine gömmek. |
| **Embedding** | Metni anlamını temsil eden sayı dizisine (vektöre) çevirmek. |
| **Entity** | Tabloya karşılık gelen sınıf (`Kullanici`). |
| **.env** | Gizli ortam değişkenlerini tutan dosya; Git'e girmez. |
| **EF Core** | Entity Framework Core: .NET'in ORM'i. |
| **Exchange** | RabbitMQ'da mesajı kurallara göre kuyruklara dağıtan yer. |
| **Fallback** | Asıl model çalışmazsa otomatik yedek modele geçme. |
| **Foreign key** | Bir tablonun başka bir tabloya bağlandığı kolon (`KullaniciId`). |
| **Healthcheck** | Servisin gerçekten hazır olup olmadığını denetleyen küçük komut. |
| **Hosted service** | Uygulama açılırken/kapanırken çalışan arka plan bileşeni (`IHostedService`); biz index kurmak için kullandık. |
| **HTTP kodları** | `200` tamam, `201` oluşturuldu, `202` kabul edildi, `400` kötü istek, `401` yetkisiz, `404` yok, `409` çakışma, `500` sunucu hatası, `502` arkadaki servise ulaşılamadı. |
| **Idempotent** | Aynı işlem iki kez yapılsa da sonucu değişmeyen. |
| **Image (Docker)** | Uygulama + bağımlılıklarının salt-okunur paketi. |
| **Index** | Aramayı hızlandıran veri yapısı; `unique` index tekrarı engeller. |
| **Infrastructure** | Veritabanı, HTTP, dosya gibi dış dünyayla konuşan katman. |
| **Integration test** | Gerçek DB/HTTP ile birkaç parçayı birlikte sınayan test. |
| **JSON** | Veriyi `{ "ad": "Emre" }` biçiminde yazan metin formatı. |
| **Kayan pencere (sliding window)** | Modele sadece son N mesajı göndermek; eskiler özetlenir ya da unutulur. |
| **Kısa süreli hafıza** | Bir oturumun konuşması: özet + ham son mesajlar. |
| **LangFlow** | AI akışlarını sürükle-bırak kuran araç; her akış bir REST adresi olur (bkz. `dersler/01-langflow.md`). |
| **Bileşen (LangFlow)** | Akıştaki tek işlik hazır kutu (Directory, Parser, Prompt Template, OpenAI...). |
| **Chunk** | RAG'de dokümanın küçük parçalara bölünmüş her bir parçası. |
| **Context stuffing (bağlam doldurma)** | Tüm dokümanı her soruda modele vermek. Basit ve küçük dokümanda iyi, büyüyünce pahalı ve sınırlı; çözümü gerçek RAG. |
| **Global değişken (LangFlow)** | Akışın dışında saklanan, adıyla çağrılan değer; "credential" olursa şifreli. Anahtarı akış dosyasına gömmemek için kullanılır. |
| **SSRF** | Sunucuyu, saldırganın yerine iç ağdaki adreslere istek attırma saldırısı. LangFlow özel IP'lere isteği varsayılan engeller. |
| **Vektör veritabanı** | Embedding vektörlerini saklayıp "en benzer parçaları bul" araması yapan veritabanı. |
| **LINQ** | C#'ta koleksiyon ve veritabanı sorgulama sözdizimi (`Where`, `Select`). |
| **LiteLLM** | Birçok LLM'e tek OpenAI uyumlu kapıdan erişim sağlayan araç. |
| **LLM** | Large Language Model: Claude, GPT gibi dil modelleri. |
| **Lifetime (DI)** | Singleton / Scoped / Transient: nesnenin ne kadar yaşayacağı. |
| **MCP** | Model Context Protocol: AI uygulamalarını dış araç ve veri kaynaklarına bağlayan açık standart. Sunucu araç sunar, istemci (Claude Code) çağırır. `.mcp.json` ile tanımlanır (bkz. `dersler/10-claude-cli-skills-mcp.md`). |
| **Skill (Claude)** | Tekrarlayan bir işin tarifi: `.claude/skills/<ad>/SKILL.md`. Sadece ilgili iş geldiğinde yüklenir (CLAUDE.md ise her zaman). |
| **CLAUDE.md** | Claude Code'un her oturum başında okuduğu proje notu (mimari, komutlar, kurallar). Kısa tutulur. |
| **stdio (MCP)** | MCP sunucusuyla istemcinin standart giriş/çıkış üzerinden satır satır JSON konuşması. |
| **Merge** | İki branch'i birleştirmek. |
| **Mutasyon kontrolü** | Kodu bilerek bozup testlerin bunu yakalayıp yakalamadığına bakmak. |
| **Middleware** | İstek controller'a ulaşmadan geçtiği ara katmanlar (sıra önemli). |
| **Migration** | Şema değişikliğini tarif eden, Git'e girebilen C# dosyası. |
| **Mock** | Testte gerçek bağımlılığın yerine geçen sahte nesne (Moq). |
| **MongoDB** | Belge (JSON benzeri) saklayan esnek şemalı veritabanı. |
| **Namespace** | Kodu mantıksal gruplara ayıran isim alanı (`Asistan.Application.Sohbet`). |
| **Navigation property** | Entity'de ilişkili nesneye giden özellik (`Abonelik.Kullanici`). |
| **N+1** | Döngüde her satır için ayrı sorgu atma hatası. |
| **NuGet** | .NET'in paket deposu. |
| **Nullable** | `string` ile `string?` ayrımını derleyicinin denetlemesi. |
| **ObjectId** | MongoDB'nin otomatik ürettiği 12 baytlık benzersiz `_id` türü; zamana göre artar. |
| **ORM** | Tabloları nesne gibi kullanmayı sağlayan katman (EF Core). |
| **Oturum (sohbet)** | Aynı konuşmaya ait mesajları birbirine bağlayan kimlik (`oturumId`). |
| **Özetleme (summarization)** | Eski mesajları kısa bir metne dönüştürüp token tasarrufu yapmak (ucuz modelle). |
| **OpenAPI** | API'nin makine-okur tarifi (`/openapi/v1.json`). |
| **Port mapping** | `5432:5432`: bilgisayar portu : container portu. |
| **PostgreSQL** | Güçlü, ilişkisel (SQL) veritabanı. |
| **Producer** | Mesajı kuyruğa bırakan taraf (bizde API). |
| **Prompt** | Modele gönderilen metin/talimat. |
| **Prompt injection** | Kullanıcı/dış kaynak metninin içine modele talimat gizleyip davranışını değiştirmeye çalışmak. Hafıza gibi prompt'a giren her metin sınırlanmalı. |
| **Pull Request (PR)** | Branch'i `main`'e birleştirme isteği; inceleme ve CI burada olur. |
| **Queue (kuyruk)** | Mesajların sırayla beklediği yer. |
| **RabbitMQ** | Mesaj aracısı (message broker). |
| **RAG** | Retrieval-Augmented Generation: dokümanlardan ilgili parçayı bulup modele vererek cevap ürettirme. |
| **Record (C#)** | Kısa, değişmez veri sınıfı (`record LlmMesaj(string Rol, string Icerik)`). |
| **Registry (Docker)** | Image deposu (Docker Hub, ghcr.io). |
| **Repository (depo)** | Veriye erişimi arayüz arkasına saklayan sınıf. |
| **Routing key** | Mesajın yönlendirme etiketi. |
| **Runner** | GitHub Actions'ta işi çalıştıran makine. |
| **Scalar** | API'yi tarayıcıdan denemeni sağlayan arayüz. |
| **SDK / Runtime** | SDK kodu derlemek, runtime derlenmiş kodu çalıştırmak içindir. |
| **Secret** | Sızarsa zarar verecek değer: şifre, API anahtarı. |
| **Servis (sınıf)** | İş kurallarını taşıyan sınıf (`KullaniciServisi`). |
| **Solution** | Birden çok projeyi toplayan `.sln` çatısı. |
| **Stateless** | Önceki isteği hatırlamayan; LLM'ler böyledir. |
| **Staging area** | `git add` sonrası commit'e hazır dosyalar alanı. |
| **System prompt** | Modelin kimliğini ve kurallarını belirleyen `system` rolündeki mesaj. |
| **Token** | Modelin metni böldüğü birim; maliyet token sayısıyla ölçülür. |
| **Transaction** | Ya hep birlikte ya hiç yazılan işlem grubu. |
| **TTL index** | Belirli süre sonra kayıtları otomatik silen MongoDB index'i. |
| **Upsert** | Kayıt varsa güncelle, yoksa oluştur (update + insert). |
| **Unit test** | Tek parçayı yalıtıp sınayan hızlı test. |
| **user-secrets** | .NET'te gizli ayarları makineye özel saklayan kasa. |
| **Uzun süreli hafıza** | Kullanıcı hakkında oturumlar arası kalıcı bilgiler (ad, seviye, hedef). |
| **Volume** | Container silinse de kalan kalıcı veri alanı. |
| **Worker** | Arka planda iş işleyen uygulama (bizde RabbitMQ consumer'ı olacak). |
| **Workflow** | GitHub Actions'ta `.github/workflows/*.yml` ile tarif edilen otomasyon. |
| **xUnit** | .NET test çerçevesi (`[Fact]`, `[Theory]`). |
| **YAML** | Girintiyle yazılan ayar formatı (`docker-compose.yml`, `ci.yml`, `config.yaml`). |
