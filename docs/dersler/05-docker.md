# Ders 5 · Docker (ayrıntılı)

Bu ders projedeki gerçek dosyalarla anlatılır: `docker-compose.yml`, `src/Asistan.Api/Dockerfile`, `src/Asistan.Worker/Dockerfile`, `.dockerignore`. Verilen sayılar bu projede **ölçülmüş** değerlerdir.

---

## 1. Docker nedir?

Docker, bir uygulamayı **ihtiyaç duyduğu her şeyle birlikte** (çalışma ortamı, kütüphaneler, ayarlar) paketleyip her bilgisayarda aynı şekilde çalıştırır. "Benim bilgisayarımda çalışıyordu" sorununun çözümü.

**Container sanal makine değildir.** Sanal makine ayrı bir işletim sistemi taşır (GB'larca, dakikalarca açılır). Container, bilgisayarın çekirdeğini paylaşır; sadece uygulama + kütüphaneleri taşır (yüzlerce MB, saniyelerde açılır). Bu yüzden bu projede 7 servisi aynı anda bilgisayarda çalıştırabiliyoruz.

## 2. Kavramlar ve projedeki karşılığı

| Kavram | Ne | Projede |
|---|---|---|
| **Image (imaj)** | Salt okunur paket: uygulama + bağımlılıkları. "Tarif/kalıp" | `postgres:17`, `langflowai/langflow:1.13.3`, `aiasistan-api:yerel` |
| **Container** | İmajın çalışan kopyası. "Kalıptan çıkan kek" | `aiasistan-api-1`, `aiasistan-mongo-1` |
| **Dockerfile** | İmajın nasıl inşa edileceğini anlatan dosya | `src/Asistan.Api/Dockerfile` |
| **Registry** | İmaj deposu (Docker Hub, ghcr.io) | `ghcr.io` (CD ile imajlarımız oraya gider) |
| **Volume** | Container silinse de kalan kalıcı veri | `postgres_data`, `mongo_data`, `langflow_data` |
| **Network** | Container'ların birbirini **adla** bulması | Compose otomatik kurar |
| **Port mapping** | `5255:8080` = bilgisayarın portu : container'ın portu | API: 5255 → 8080 |
| **Compose** | Birden çok container'ı tek dosyada tarif eder | `docker-compose.yml` |
| **Healthcheck** | "Servis gerçekten hazır mı?" kontrolü | postgres, mongo, rabbitmq |

## 3. `docker-compose.yml` satır satır

Projedeki 8 servis:

| Servis | İmaj | Ne için | Varsayılan başlar mı? |
|---|---|---|---|
| `postgres` | `postgres:17` | Kullanıcılar | Evet |
| `mongo` | `mongo:8` | Sohbet, hafıza, iş fişleri | Evet |
| `rabbitmq` | `rabbitmq:4-management` | Kuyruk | Evet |
| `litellm` | `ghcr.io/berriai/litellm:main-stable` | Modele tek kapı | Evet |
| `langflow` | `langflowai/langflow:1.13.3` | Doküman akışı | Evet |
| `api` | `aiasistan-api:yerel` (kendi imajımız) | Web API | **Hayır** (profil) |
| `worker` | `aiasistan-worker:yerel` | Kuyruk işçisi | **Hayır** (profil) |

Önemli sözdizimi:

```yaml
environment:
  POSTGRES_USER: ${POSTGRES_USER}        # ${...} .env dosyasından okunur: şifre compose dosyasına gömülmez
healthcheck:
  test: ["CMD-SHELL", "pg_isready ..."]   # "başladı" ile "hazır" farklıdır
depends_on:
  postgres: { condition: service_healthy }   # sadece "başladı" değil "sağlıklı" olana kadar bekle
profiles: ["uygulama"]                    # bu servisler sadece --profile uygulama ile başlar
environment: &uygulama-ortami             # YAML "çapa": bu bloğa bir ad ver
environment: *uygulama-ortami             # ... ve başka yerde AYNEN yeniden kullan (api ve worker aynı ayarı paylaşır)
restart: unless-stopped                   # çökerse yeniden başlat
```

**`profiles` neden?** Geliştirirken API ve Worker'ı IDE'den çalıştırıyoruz (hata ayıklama kolay). Varsayılan `docker compose up` sadece altyapıyı kaldırır; uygulamayı container olarak denemek için `docker compose --profile uygulama up -d --build`. İki ayrı kullanım, tek dosya.

## 4. En büyük tuzak: `localhost` container'ın kendisidir

Bilgisayarında API `localhost:5432`'deki Postgres'e bağlanır. **Container içindeki API için `localhost` kendi container'ıdır.** Diğer servislere **compose servis adıyla** ulaşılır:

| IDE'den çalışan API | Container'daki API |
|---|---|
| `Host=localhost` | `Host=postgres` |
| `localhost:27017` | `mongo:27017` |
| `http://localhost:4000` | `http://litellm:4000` |
| `http://localhost:7860` | `http://langflow:7860` |

Aynı ayarları compose'da **ortam değişkeni** olarak veriyoruz. .NET'te `__` (çift alt çizgi) `appsettings.json`'daki `:` karşılığıdır: `Mongo__Url` = `Mongo:Url`. Container'da `user-secrets` yoktur (ortam `Production`); ayarlar ortam değişkeninden gelir. Eksik bir ayar olursa uygulama açılırken `AyarlariDogrula` ile neyin eksik olduğunu açıkça söyler (bkz. `05-sorun-giderme.md` #13).

**Port çakışması:** IDE'deki API `5254`'te, container'daki API `5255`'te (`5255:8080`). İkisi aynı anda çalışabilir.

## 5. Dockerfile: multi-stage build

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build         # 1) DERLEME aşaması: büyük SDK imajı (~800 MB)
COPY Directory.Build.props ./
COPY src/Asistan.Domain/Asistan.Domain.csproj src/Asistan.Domain/   # ... sadece .csproj dosyaları
RUN dotnet restore src/Asistan.Api/Asistan.Api.csproj    # paketleri indir
COPY src/ src/                                           # ŞİMDİ kaynak kod
RUN dotnet publish ... -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final        # 2) ÇALIŞMA aşaması: küçük imaj, SDK YOK
COPY --from=build /app .                                  # sadece derlenmiş çıktıyı al
USER app                                                  # root DEĞİL
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "Asistan.Api.dll"]
```

**Neden iki aşama?** Derlemek için SDK gerekir, çalıştırmak için gerekmez. Son imaja sadece çıktı girer: daha küçük (aşağıdaki sayılar), daha güvenli (içinde derleyici yok).

**Neden önce sadece `.csproj`, sonra kod?** Docker her satırı bir **katman** olarak önbelleğe alır. Satır ve ondan önceki her şey değişmediyse katman yeniden kullanılır. Paket listesi nadir, kod her commit'te değişir. `restore`'u koddan önce koyarsak kod değişince paketler yeniden indirilmez. **Ölçtük:**

| Ne değişti | API imajı derleme süresi |
|---|---|
| Hiçbir şey | **1.3 sn** (tüm katmanlar önbellekte) |
| Sadece kod | **11.9 sn** (paketler önbellekte, sadece derleme) |
| `.csproj` (paket listesi) | **26.5 sn** (restore yeniden çalışır) |
| Sıfırdan (ilk sefer, SDK imajı dahil) | ~2 dakika |

**Boyutlar (ölçülen):** API imajı **362 MB**, Worker **325 MB** (Worker'da web sunucusu yok, `runtime` imajı daha küçük). Karşılaştırma: SDK imajı ~800 MB'dan büyük.

## 6. `.dockerignore`: ne imaja girmez

`.dockerignore`, `docker build` komutuna gönderilen dosya setinden neleri hariç tutacağını söyler:
- `bin/`, `obj/`: container içinde zaten yeniden derlenir.
- **`.env`, `appsettings.Local.json`: gizli dosyalar imaja girmemeli.** Doğruladık: imajın içinde `.env` veya `Local.json` yok.
- `.git/`, `tests/`, `docs/`: imajın ihtiyacı yok, build bağlamını şişirir.

## 7. Güvenlik: root olmayan kullanıcı

`USER app` ile uygulama `uid=1654(app)` olarak çalışır (kontrol ettik: `id` komutu). İmaj ele geçirilirse saldırgan root olmaz. Gizli değerler imaja gömülmez, çalışma anında ortam değişkeniyle verilir.

## 8. Volume'ler: veri nerede?

Volume olmasa container silinince veri de gider. Projede:
- `postgres_data`, `mongo_data`, `rabbitmq_data`, `langflow_data`
- `docker compose stop` / `down`: container'lar gider, **volume kalır**.
- **`docker compose down -v`: volume'ler de silinir, TÜM VERİ GİDER.** Dikkatli kullan.

**Gerçek hata (LangFlow dersi):** LangFlow'un veritabanını `/app/langflow` volume'üne bağladık ama LangFlow varsayılan olarak başka yere yazıyordu; container yenilenince akışlar kayboldu. Ders: *volume'ü bağlamak yetmez, uygulamanın verisini gerçekten oraya yazdığından emin ol.*

**Başka bir tuzak:** `POSTGRES_PASSWORD` yalnızca veritabanı **ilk oluşturulurken** okunur. Volume varken `.env`'deki şifreyi değiştirmek etki etmez.

## 9. Gerçek deney: tüm sistem container'da

`docker compose --profile uygulama up -d` ile API ve Worker container oldu. Sonuç:
- API: `localhost:5255`, PostgreSQL'e `postgres` adıyla bağlandı (kullanıcı sorgusu 200).
- Worker: `rabbitmq` adıyla bağlandı (`Bus started: rabbitmq://rabbitmq/`).
- Normal sohbet ve doküman modu uçtan uca çalıştı (202 → Tamamlandi).
- İmaj içinde gizli dosya yok, kullanıcı `app`.

## 10. Komutlar

| Komut | Ne yapar |
|---|---|
| `docker compose up -d` | Altyapıyı başlatır |
| `docker compose --profile uygulama up -d --build` | Uygulamayı (API+Worker) imajı derleyerek başlatır |
| `docker compose ps` | Durum ve sağlık |
| `docker compose logs api --tail 50` | Günlük |
| `docker compose exec mongo mongosh ...` | Çalışan container'ın içinde komut |
| `docker compose stop` / `down` / `down -v` | Durdur / sil (veri kalır) / sil (veri de gider) |
| `docker build -f src/Asistan.Api/Dockerfile -t aiasistan-api:yerel .` | Sadece imajı derle |
| `docker image ls` | İmajlar ve boyutları |
| `docker run --rm --entrypoint sh <imaj> -c 'id'` | İmajın içine bakmak |
| `docker system prune` | Kullanılmayan imaj/container temizliği (dikkat) |

## 11. Hata ayıklama

| Belirti | Neden / bak |
|---|---|
| `Connection refused` container'dan | `localhost` yazılmış, servis adı olmalı |
| Container hemen kapanıyor | `docker compose logs <servis>`; çoğu zaman eksik ayar (`Eksik ayar(lar)` hatası) |
| Ayar değişti ama etki yok | Ortam değişkenleri container **yaratılırken** okunur: `docker compose up -d` (restart yetmez) |
| `port is already allocated` | O port başka süreçte; `ports:` sol tarafı değiştir |
| Build çok yavaş | `.dockerignore` eksik mi, `COPY` sırası kodu paketlerden önce mi koyuyor |
| İmaj çok büyük | Multi-stage kullan, `.dockerignore`'u kontrol et |

## 12. Bilmen gerekenler

1. Container = paylaşılan çekirdek + paketlenmiş uygulama; sanal makine değil.
2. Image tarif, container çalışan kopya, volume kalıcı veri.
3. Compose çok servisi tek dosyada tarif eder; `profiles` ile isteğe bağlı servisler.
4. Container içinde `localhost` kendisidir: servis **adıyla** bağlan.
5. Ayarlar ortam değişkeniyle gelir (`__` = `:`); gizli değer imaja ve Git'e girmez.
6. Multi-stage: derle büyük imajda, çalıştır küçük imajda. `.csproj` önce, kod sonra (katman önbelleği).
7. `healthcheck` + `depends_on: service_healthy` ile başlatma sırasını güvenceye al.
8. `down -v` veriyi siler. Volume'ü bağlamak yetmez, verinin oraya yazıldığını doğrula.
9. Root olmayan kullanıcıyla çalıştır.
