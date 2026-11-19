# Ders 9 · CI/CD (ayrıntılı)

Dosyalar: `.github/workflows/ci.yml`, `.github/workflows/cd.yml`, `src/Asistan.Infrastructure/Persistence/AppDbContextFabrikasi.cs`, Dockerfile'lar.

---

## 1. Nedir?

**CI (Continuous Integration):** Kod her gönderildiğinde (push) ve her Pull Request'te **otomatik** derlenir ve test edilir. Kırmızıysa kod `main`'e girmemeli. İnsan unutur, makine unutmaz.

**CD** iki şey olabilir, karıştırma:
- **Continuous Delivery:** Testler geçince yayına çıkacak paket (imaj) hazırlanır ve bir onayla yayına alınır.
- **Continuous Deployment:** Onay da yok, `main`'e giren her şey otomatik yayına çıkar.

Bizimki **Delivery**: imajlar otomatik üretilir ve registry'ye gider; sunucuya yayınlama ayrı bir iş ve bir **güvenlik düğmesiyle** kapalı (aşağıda).

```
push ─► CI: derle + test + Dockerfile kontrolü ─ yeşil ─► CD: imaj üret + registry'ye gönder ─► (açıksa) sunucuya yayınla
                          └─ kırmızı ─► DUR, yayına hiçbir şey çıkmaz
```

## 2. GitHub Actions sözlüğü

| Kavram | Anlamı | Bizde |
|---|---|---|
| **Workflow** | `.github/workflows/*.yml` dosyası, bir pipeline | `ci.yml`, `cd.yml` |
| **Trigger (`on:`)** | Ne zaman çalışır: `push`, `pull_request`, `workflow_run`, `workflow_dispatch` (elle) | |
| **Job** | Aynı makinede çalışan adımlar grubu; job'lar paralel çalışır, `needs:` ile sıralanır | `test`, `docker`, `imaj`, `yayinla` |
| **Step** | Tek komut (`run:`) ya da hazır bir action (`uses:`) | |
| **Runner** | Job'u çalıştıran makine | `ubuntu-latest` |
| **Secret** | Gizli değer (günlükte `***` görünür) | `GITHUB_TOKEN`, `DEPLOY_SSH_KEY` |
| **Variable** | Gizli olmayan ayar | `DEPLOY_ENABLED` |
| **Environment** | Ortam (ör. `production`); onay kuralı ve ayrı secret'lar tanımlanabilir | `production` |
| **Matrix** | Aynı job'u farklı değerlerle (api, worker) paralel çalıştırır | CD'de `servis` |
| **Artifact** | Job'un ürettiği ve saklanan dosya | `efbundle`, `coverage` |

## 3. `ci.yml`: iki job

1. **`test`:** kodu çek → .NET 9 kur → `dotnet restore` → `dotnet build -c Release` (**uyarılar hata**) → `dotnet test` + kod kapsama → kapsama raporunu artifact olarak yükle (`if: always()`: test kırılsa da rapor kalır).
2. **`docker`** (`needs: test`): iki Dockerfile'ı **derler, göndermez**. Neden? Bozuk bir Dockerfile'ı ancak yayın sırasında fark etmek geçtir; PR'da yakalanmalı.

Unit testler veritabanı istemediği için CI'da Postgres/Mongo yok. (Integration test eklenirse `services:` ile container başlatılırdı.)

## 4. `cd.yml`: adım adım

**Tetikleyici:**
```yaml
on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
    branches: [main]
  workflow_dispatch:
```
CI **bitince** başlar. `if: ... workflow_run.conclusion == 'success'` ile **sadece yeşilse** devam eder. `workflow_dispatch` elle başlatma içindir.

**`permissions:`** `GITHUB_TOKEN`'a en az yetki: kodu oku, paket yaz. (Gereğinden fazla yetki vermemek güvenlik ilkesi.)

**`imaj` job'u (matrix: api, worker):**
1. `checkout` ile CI'ın test ettiği **aynı commit** (`head_sha`).
2. `docker/login-action` ile `ghcr.io`'ya `GITHUB_TOKEN` ile giriş (ayrı şifre yok).
3. `docker/metadata-action`: imaj **etiketlerini** üretir (aşağıda).
4. `docker/build-push-action`: derler ve **gönderir**; `cache-from/to: type=gha` ile katman önbelleği GitHub'da saklanır (Docker dersindeki hız kazancı CI'da da).

**Etiketleme:** her imaj iki etiketle gider: `…:<tam commit SHA>` ve `…:latest`. **Neden SHA?** `latest` hareketli bir etikettir ("şu an en yeni"); *"şu an üretimde HANGİ kod çalışıyor?"* sorusunun cevabı SHA'dır. SHA ile belirli bir sürüme geri dönmek de mümkün olur. İmaj adı **küçük harf** olmak zorunda; `metadata-action` bunu kendisi halleder (GitHub sahip adı büyük harf içerebilir).

**`migration-paketi` job'u:** `dotnet ef migrations bundle` ile veritabanı şemasını güncelleyen **tek bir çalıştırılabilir dosya** (`efbundle`, linux-x64, self-contained) üretir ve artifact olarak saklar. Neden? Aşağıda.

**`yayinla` job'u:**
```yaml
if: vars.DEPLOY_ENABLED == 'true'     # GÜVENLİK DÜĞMESİ
environment: production               # onay kuralı konabilir
```
`DEPLOY_ENABLED` değişkeni `true` yapılmadıkça bu job **hiç çalışmaz**. Henüz bir sunucumuz yok; yanlışlıkla yayınlamaya çalışıp hata yığını üretmesin. Açıldığında SSH ile sunucuya bağlanır, yeni imajları `pull` eder ve `docker compose up -d` ile yeniden başlatır.

## 5. Migration'lar üretimde nasıl uygulanır?

Uygulama açılırken `Database.Migrate()` çağırmak basittir ama **birden fazla kopya aynı anda açılırsa çakışır** (ikisi birden şemayı değiştirmeye çalışır). Daha güvenli yol: deploy'dan **hemen önce, bir kez** çalışan bir araç: **migration bundle**.

**Gerçek bulgu (bunu sınarken yaşadık):** Bundle'ı ilk çalıştırdığımızda hata verdi, çünkü EF araçları `DbContext`'i almak için **uygulamanın `Program.cs`'ini çalıştırıyor** ve oradaki zorunlu ayar denetimimiz (Mongo, RabbitMQ, LangFlow sırları) ilgisiz bu iş için de devreye girdi. Şemayı güncellemek için tüm sırlara ihtiyaç olmamalı.

**Çözüm:** EF'in resmî mekanizması, **tasarım zamanı fabrikası** (`AppDbContextFabrikasi : IDesignTimeDbContextFactory<AppDbContext>`). Araçlar önce onu bulur ve `Program.cs`'e hiç dokunmaz. Bağlantı cümlesini bundle'ın `--connection` seçeneği verir. **Kanıt (ortamda Mongo/RabbitMQ sırları YOKKEN):**
- Dolu veritabanında: *"No migrations were applied. The database is already up to date."* (güvenle tekrar çalıştırılabilir = **idempotent**)
- **Boş** veritabanında: `IlkSema` uygulandı; `Kullanicilar`, `Abonelikler` ve `__EFMigrationsHistory` tabloları oluştu.

**Ders:** "Hızlı hata ver" (fail-fast) doğrulaması, uygulamayı korurken yan araçları (migration) bozabilir. Araç başka bir giriş noktası olduğunda onu ayır.

## 6. Doğrulama: neyi gerçekten sınadık?

| Ne | Nasıl | Sonuç |
|---|---|---|
| Workflow dosyalarının sözdizimi | `actionlint` (resmî denetleyici, Docker'la). **Sessiz geçmesine güvenmedik:** bilerek bozuk bir workflow ile sınadık, iki hatayı yakaladı | İki dosya temiz |
| Dockerfile'lar | Yerelde `docker build` | İkisi derlendi, imaj sır içermiyor |
| Migration bundle | Yerelde dolu ve boş veritabanında çalıştırdık | Doğru çalıştı |
| CI/CD'nin GitHub'da çalışması | **Doğrulayamadım.** Bu oturumdan Actions sonuçlarını göremiyorum | Aşağıya bak |

**Dürüst sınır:** GitHub'a push ettik ama CI'ın yeşil/kırmızı sonucunu, CD'nin imajları gerçekten `ghcr.io`'ya gönderdiğini ve `yayinla` job'unu **görmedim ve test etmedim** (sunucu yok). Repo → **Actions** sekmesinde CI ve CD'nin sonucuna bakmak sana düşüyor. Hata varsa mesajı yapıştır.

## 7. Elle yapman gereken ayarlar (GitHub arayüzü)

| Ayar | Nerede | Ne için |
|---|---|---|
| **Branch protection** | Settings → Branches → `main` → "Require status checks to pass" (CI job'ları) | Testi kırık kod `main`'e girmesin (özel repoda plana bağlı olabilir) |
| `DEPLOY_ENABLED` | Settings → Secrets and variables → Actions → **Variables** | `true` yapana kadar yayın kapalı |
| `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY` | Aynı yer → **Secrets** | Sunucu bilgileri (sunucu kurulunca) |
| Environment `production` | Settings → Environments | İsteğe bağlı: yayın için **onaylayan kişi** kuralı |

## 8. Sık hatalar

| Belirti | Neden |
|---|---|
| `workflow_run` hiç tetiklenmiyor | O tetikleyici yalnızca **varsayılan dalda** (`main`) bulunan workflow dosyasından çalışır; `cd.yml` `main`'de olmalı |
| `denied: permission_denied` imajı gönderirken | `permissions: packages: write` eksik ya da paket başka bir repoya bağlı |
| `invalid reference format` | İmaj adı büyük harf içeriyor (bizde `metadata-action` önler) |
| CI yeşil ama CD çalışmadı | CD `main` için; PR'larda çalışmaz. `workflow_run.conclusion` `success` mi? |
| Yerelde geçiyor, CI'da kırmızı | Ortam farkı: .NET sürümü, eksik dosya (`.gitignore`'a girmiş), `Release` modunda uyarı=hata |
| `if:` hep atlanıyor | `vars.X` yalnızca **Variables**'tan okunur, Secrets'tan değil |

## 9. Bilmen gerekenler

1. CI = her değişiklikte otomatik build + test. CD = yeşil CI'dan sonra paket üretme/yayın.
2. Kırmızı CI asla yayına çıkmamalı (`workflow_run` + `conclusion == 'success'`).
3. İmajı **commit SHA** ile etiketle: üretimde ne çalıştığını ve geri dönüşü bilirsin; `latest` yetmez.
4. `permissions`'ı en aza indir; sırlar `secrets`'ta, gizli olmayanlar `vars`'ta.
5. Yayın adımına bir **güvenlik düğmesi** (`DEPLOY_ENABLED`) ve onay ortamı koy.
6. Migration'ı uygulama açılışında değil, deploy öncesi **tek seferlik bundle** ile uygula; araç için **tasarım zamanı fabrikası** yaz.
7. Workflow'u göndermeden **lint'le** (`actionlint`), linter'ın gerçekten çalıştığını bozuk bir örnekle sına.
8. Gönderdiğin pipeline'ın GitHub'da gerçekten çalıştığını **kendi gözünle** (Actions sekmesi) doğrula.
