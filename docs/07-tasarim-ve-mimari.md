# 07 · Tasarım ve mimari: bu proje ne, nereye bağlı?

Bu sayfadaki diyagramlar [Mermaid](https://mermaid.js.org) ile yazıldı: **GitHub'da ve Visual Studio Code'da kod olarak değil, çizim olarak görünür.**

## 1. Bu proje ne?

**AiAsistan, bir yapay zekâ sohbet servisidir (arka uç / API).** Bir kullanıcı soru sorar; servis soruyu Claude'a iletir, cevabı getirir, **konuşmayı ve kullanıcıyı hatırlar**. Ekranı (arayüzü) yoktur; başka bir program (mobil uygulama, web sitesi) ona HTTP ile bağlanır. Kendi başına denemek için tarayıcıda küçük bir test sayfası vardır (Scalar, `/scalar/v1`).

**Neden var?** Gerçek bir ürün değil, bir **ders projesi**. "Yazılım Geliştirme Rehberi: .NET'ten AI'ya" rehberindeki on konuyu (.NET, EF Core, Docker, MongoDB, RabbitMQ, LiteLLM, LangFlow, AI hafızası, Git/GitHub, CI/CD) **tek bir çalışan projede** birleştirerek öğretir.

## 2. Nereye bağlı? Başka projelerle ilişkisi

AiAsistan **bağımsız bir projedir**: kendi klasörü (`source/repos/AiAsistan`), kendi Git reposu, kendi çözüm dosyası (`.sln`), kendi veritabanları ve kendi GitHub deposu vardır. CariCep (`ksp1`) ve diğer projelerle hiçbir kod, klasör veya depo bağı yoktur.

```mermaid
flowchart LR
    CARI["CariCep (ksp1)<br/>ayrı proje, ayrı depo"]
    AI["AiAsistan<br/>ders projesi"]
    CARI -. "bağ YOK" .- AI

    AI --> DOCKER["Docker servisleri<br/>PostgreSQL, MongoDB, RabbitMQ,<br/>LiteLLM, LangFlow"]
    AI --> CLAUDE["Claude (Anthropic)<br/>cevabı üreten model"]
    AI --> GH["GitHub<br/>kendi AiAsistan deposu"]
```

| Soru | Cevap |
|---|---|
| CariCep koduna dokunuyor mu? | Hayır |
| CariCep'in veritabanını kullanıyor mu? | Hayır, kendi PostgreSQL/MongoDB'si var (Docker'da) |
| Git deposu aynı mı? | Hayır. AiAsistan'ın kendi deposu var |
| İleride CariCep ile bağlanabilir mi? | Evet: CariCep, AiAsistan API'sini HTTP ile çağırarak bir "yapay zekâ asistanı" özelliği alabilir. Bu henüz yapılmadı ve senin kararın |

## 3. İçinde ne çalışır? (kapsayıcı görünümü)

```mermaid
flowchart TB
    KULLANICI["İstemci<br/>(Scalar, mobil, web)"]

    subgraph UYGULAMA["Senin yazdığın .NET programları"]
        API["Asistan.Api<br/>kapı: soruyu alır, 202 döner"]
        WORKER["Asistan.Worker<br/>mutfak: soruyu işler"]
    end

    subgraph ALTYAPI["Docker'da hazır servisler"]
        RMQ[("RabbitMQ<br/>sipariş askısı")]
        PG[("PostgreSQL<br/>kullanıcılar")]
        MONGO[("MongoDB<br/>sohbet, hafıza, iş fişleri")]
        LITELLM["LiteLLM<br/>modele tek kapı"]
        LANGFLOW["LangFlow<br/>doküman akışı"]
    end

    CLAUDE["Claude (Anthropic)"]

    KULLANICI -->|"POST /api/sohbet"| API
    API -->|"fiş yaz"| MONGO
    API -->|"soruyu bırak"| RMQ
    API --> PG
    RMQ --> WORKER
    WORKER --> MONGO
    WORKER --> LITELLM
    WORKER -.->|"doküman modu (Aşama 10)"| LANGFLOW
    LANGFLOW --> LITELLM
    LITELLM --> CLAUDE
    KULLANICI -->|"GET /api/isler/{isId}"| API
```

## 4. Bir soru nasıl cevaplanır? (sıra diyagramı)

```mermaid
sequenceDiagram
    actor K as Kullanıcı
    participant A as API
    participant M as MongoDB
    participant Q as RabbitMQ
    participant W as Worker
    participant L as LiteLLM / Claude

    K->>A: POST /api/sohbet (soru)
    A->>M: takip fişi yaz (Bekliyor)
    A->>Q: soruyu kuyruğa bırak
    A-->>K: 202 Accepted + isId (0.3 sn)
    Q->>W: soruyu ver
    W->>M: fişi Isleniyor yap, bağlamı kur (hafıza, özet, geçmiş)
    W->>L: soruyu sor
    L-->>W: cevap
    W->>M: soru+cevabı kaydet, fişi Tamamlandi yap
    W->>Q: arka plan işi (özetleme, hafıza çıkarma)
    K->>A: GET /api/isler/{isId}
    A-->>K: Tamamlandi + cevap
```

## 5. Veri nerede saklanır? (veri modeli)

İki farklı veritabanı var, çünkü iki farklı türde veri var: **ilişkili ve titiz** veri PostgreSQL'de, **çok ve esnek** veri MongoDB'de.

```mermaid
erDiagram
    KULLANICI ||--o{ ABONELIK : "sahip"
    KULLANICI {
        int Id PK
        string Ad
        string Email UK
        datetime KayitTarihi
    }
    ABONELIK {
        int Id PK
        string Plan
        int KullaniciId FK
    }
```

MongoDB koleksiyonları (şemasız belgeler):

| Koleksiyon | Ne tutar | Bağlantı anahtarı |
|---|---|---|
| `mesajlar` | Her soru ve her cevap | `oturumId`, `kullaniciId` |
| `oturumOzetleri` | Bir sohbetin eski kısmının özeti | `oturumId` (tek) |
| `hafizalar` | Kullanıcı hakkında kalıcı bilgiler | `kullaniciId` |
| `isler` | Sorunun takip fişi (Bekliyor → Tamamlandi) | `_id` = `isId`, 7 gün sonra silinir |

## 6. Katmanlar (kod içindeki düzen)

```mermaid
flowchart LR
    DOMAIN["Domain<br/>veri sınıfları"] --> APPLICATION["Application<br/>iş kuralları"]
    APPLICATION --> INFRA["Infrastructure<br/>veritabanı, kuyruk, LLM"]
    INFRA --> API2["Api"]
    INFRA --> WORKER2["Worker"]
```

Oklar "bilir" demektir: Domain hiçbir şeyi bilmez, Application sadece Domain'i bilir. Ayrıntı: `01-proje-haritasi.md`.

## 7. Bu tasarımda bilerek yapılmayanlar

- **Arayüz yok.** Ekran tasarımı (Figma, Windows Forms, Razor sayfası) bu projede yoktur; sadece API'dir.
- **Kimlik doğrulama yok.** Kullanıcı kimliğini bilen herkes o kullanıcının oturumunu ve hafızasını görebilir.
- **Tek makine.** Her şey bilgisayarında Docker ile çalışır. API ve Worker imajları ve CD hazır (`.github/workflows/cd.yml`), ama gerçek bir sunucu yok; yayın adımı `DEPLOY_ENABLED` düğmesiyle kapalı.
