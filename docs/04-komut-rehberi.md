# 04 · Komut rehberi (kopyala-yapıştır)

Komutlar **PowerShell** için yazıldı. Hepsini `AiAsistan` klasöründe çalıştır:

```powershell
cd C:\Users\emref\source\repos\KSP2\AiAsistan
```

> PowerShell'de `curl` aslında `Invoke-WebRequest`'in takma adıdır ve sürpriz yapar. API denemek için aşağıdaki `Invoke-RestMethod` örneklerini veya `Asistan.Api.http` dosyasını kullan.

---

## Günlük döngü (en çok kullanacakların)

```powershell
docker compose up -d                 # altyapıyı başlat
dotnet build                         # derle
dotnet test                          # testleri çalıştır
dotnet run --project src/Asistan.Api --launch-profile http   # API'yi çalıştır (1. terminal)
dotnet run --project src/Asistan.Worker                       # Worker'ı çalıştır (2. terminal). ONSUZ SORULAR İŞLENMEZ
```

Bitirince:
```powershell
docker compose stop                  # durdur (veri kalır)
```

---

## .NET

| Komut | Ne yapar |
|---|---|
| `dotnet --version` | SDK sürümü |
| `dotnet build` | Tüm solution'ı derler (uyarı = hata!) |
| `dotnet build -c Release` | Release modunda derler (CI böyle yapar) |
| `dotnet test` | Tüm testleri çalıştırır |
| `dotnet test --filter "FullyQualifiedName~Kullanici"` | Adında "Kullanici" geçen testleri çalıştırır |
| `dotnet test --collect:"XPlat Code Coverage"` | Kod kapsama raporu da üretir |
| `dotnet run --project src/Asistan.Api` | API'yi çalıştırır |
| `dotnet restore` | NuGet paketlerini indirir |
| `dotnet clean` | Derleme çıktılarını siler |
| `dotnet add src/Asistan.Api package <Ad>` | Projeye paket ekler |
| `dotnet add <proje> reference <diğer proje>` | Proje referansı ekler |
| `dotnet sln list` | Solution'daki projeleri listeler |
| `dotnet tool restore` | `.config/dotnet-tools.json`'daki araçları (dotnet-ef) kurar |

### user-secrets (gizli ayarlar)
```powershell
dotnet user-secrets list -p src/Asistan.Api                       # neler kayıtlı (değerler görünür, dikkat)
dotnet user-secrets set "LiteLlm:Key" "<deger>" -p src/Asistan.Api
dotnet user-secrets remove "LiteLlm:Key" -p src/Asistan.Api
```

### EF Core migration
```powershell
dotnet tool restore                                                   # (bir kez) dotnet-ef'i kur

# Yeni migration üret
dotnet ef migrations add MigrationAdi -p src/Asistan.Infrastructure -s src/Asistan.Api -o Persistence/Migrations

# Veritabanına uygula
dotnet ef database update -p src/Asistan.Infrastructure -s src/Asistan.Api

# Uygulanmamış son migration'ı sil
dotnet ef migrations remove -p src/Asistan.Infrastructure -s src/Asistan.Api

# Hangi migration'lar var / uygulandı mı
dotnet ef migrations list -p src/Asistan.Infrastructure -s src/Asistan.Api
```

---

## Docker

| Komut | Ne yapar |
|---|---|
| `docker compose up -d` | Tüm servisleri arka planda başlatır |
| `docker compose up -d litellm` | Sadece LiteLLM'i başlatır |
| `docker compose ps` | Servislerin durumu (healthy mi?) |
| `docker compose logs litellm --tail 50` | LiteLLM'in son 50 log satırı |
| `docker compose logs -f postgres` | Postgres loglarını canlı izler (Ctrl+C çıkar) |
| `docker compose restart litellm` | LiteLLM'i yeniden başlatır |
| `docker compose up -d --force-recreate litellm` | Container'ı baştan yaratır (`.env` değişince bunu kullan) |
| `docker compose stop` | Hepsini durdurur, veri kalır |
| `docker compose down` | Container'ları siler, **volume'ler (veri) kalır** |
| `docker compose down -v` | Container'ları **ve tüm veriyi** siler (dikkat!) |
| `docker ps` | Çalışan tüm container'lar |

> `.env` değişince `restart` yetmez; ortam değişkenleri container yaratılırken okunur. `--force-recreate` kullan.

### Veritabanına bakmak
```powershell
# Postgres: kullanıcı tablosunu oku
docker compose exec postgres psql -U asistan -d asistan -c 'select "Id","Ad","Email" from "Kullanicilar"'

# Postgres: tabloları listele
docker compose exec postgres psql -U asistan -d asistan -c '\dt'

# Mongo'ya gir (şifreyi .env'deki MONGO_PASSWORD'den al)
docker compose exec mongo mongosh -u asistan -p <MONGO_PASSWORD> --authenticationDatabase admin asistan

# mongosh içinde (alan adları camelCase ve büyük/küçük harf duyarlı!):
#   db.mesajlar.countDocuments({})
#   db.mesajlar.find().sort({olusturulma: -1}).limit(5)
#   db.mesajlar.find({oturumId: "<OTURUM_ID>"}).sort({olusturulma: 1, _id: 1})
#   db.mesajlar.getIndexes()
#   db.hafizalar.find({kullaniciId: "1"})      # uzun süreli hafıza
#   db.oturumOzetleri.find()                  # oturum özetleri
#   db.mesajlar.drop()          # DİKKAT: tüm sohbet geçmişini siler
```
Postgres tablo/kolon adları **büyük harfli** olduğu için çift tırnak şart (`"Kullanicilar"`).

Grafik arayüz: MongoDB Compass (ücretsiz) ile `mongodb://localhost:27017` (kullanıcı/şifre ile).

---

## API'yi komut satırından denemek (PowerShell)

```powershell
# Kullanıcı oluştur
Invoke-RestMethod -Method Post -Uri http://localhost:5254/api/kullanicilar `
  -ContentType 'application/json' `
  -Body '{"ad":"Ayse","email":"ayse@test.com"}'

# Kullanıcıyı getir
Invoke-RestMethod http://localhost:5254/api/kullanicilar/1

# Claude'a sor. ARTIK ASENKRON: API hemen 202 döner, cevap Worker'dan gelir (Worker çalışıyor olmalı!)
$r = Invoke-RestMethod -Method Post -Uri http://localhost:5254/api/sohbet `
  -ContentType 'application/json' `
  -Body '{"kullaniciId":"1","soru":"DI nedir?"}'
$r                                   # isId, oturumId, durum=Bekliyor, durumUrl

# Cevabı almak için durumu sor (hazır olana kadar tekrarla)
do { Start-Sleep 2; $i = Invoke-RestMethod "http://localhost:5254/api/isler/$($r.isId)"; $i.durum }
until ($i.durum -in "Tamamlandi","Hata")
$i.cevap

# Aynı sohbete devam et (oturumId'yi geri gönder) ve geçmişi oku
$r2 = Invoke-RestMethod -Method Post -Uri http://localhost:5254/api/sohbet `
  -ContentType 'application/json' `
  -Body (@{ kullaniciId = "1"; soru = "Peki Scoped?"; oturumId = $r.oturumId } | ConvertTo-Json)
Invoke-RestMethod "http://localhost:5254/api/sohbet/$($r.oturumId)/mesajlar"

# Asistanın kullanıcı hakkında hatırladıkları (görme ve silme hakkı)
Invoke-RestMethod http://localhost:5254/api/hafiza/1
Invoke-RestMethod -Method Delete http://localhost:5254/api/hafiza/1/<HAFIZA_ID>   # tek hafızayı sil
Invoke-RestMethod -Method Delete http://localhost:5254/api/hafiza/1               # kullanıcının TÜM hafızasını sil
```

Daha kolayı: API çalışırken `http://localhost:5254/scalar/v1` adresini tarayıcıda aç.

### Kuyruğu izlemek ve sistemi bilerek bozup davranışını görmek
```powershell
# Kuyruklar, bekleyen mesaj ve tüketici sayısı (panel: http://localhost:15672 → Queues and Streams)
docker compose exec rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged consumers

# İş takip fişleri (MongoDB):
docker compose exec mongo mongosh -u asistan -p <MONGO_PASSWORD> --authenticationDatabase admin asistan --eval "db.isler.find().sort({olusturulma:-1}).limit(5)"
```
Deneyler (Worker + API çalışırken; her biri bir sonuç öğretir):
| Yap | Sonra bir soru gönder, ne görürsün? |
|---|---|
| Worker'ı kapat (Ctrl+C) | API yine 202; fiş `Bekliyor`, kuyrukta 1 mesaj, tüketici 0. Worker'ı açınca işlenir |
| `docker compose stop litellm` | Fiş `Isleniyor`, `denemeSayisi` 1→6 artar (~2 dk), sonra `Hata`; `ai-istegi_error` kuyruğunda 1 mesaj. `docker compose start litellm` ile geri aç |
| `docker compose stop rabbitmq` | API 5 sn sonra **503**; fiş `Hata`. `docker compose start rabbitmq` ile geri aç, birkaç sn sonra her şey toparlanır |
> **Panel ve sayaçlar birkaç sn gecikmeli yenilenir.** "0" gördüğünde sayfayı yenile veya birkaç sn bekle, ya da `rabbitmqctl` ile sunucuya sor.

### Uygulamayı container olarak çalıştırmak (Aşama 11)
```powershell
docker compose --profile uygulama up -d --build      # API (localhost:5255) + Worker container olarak
docker compose --profile uygulama ps                  # durum
docker compose --profile uygulama logs api worker --tail 30
docker compose --profile uygulama stop api worker     # sadece uygulamayı durdur (altyapı çalışmaya devam)
docker build -f src/Asistan.Api/Dockerfile -t aiasistan-api:yerel .   # sadece imaj (çözüm klasöründe!)
docker image ls                                        # imajlar ve boyutları
```
> IDE'deki API (5254) ve container'daki API (5255) aynı anda çalışabilir, ama **iki Worker** aynı kuyruğu paylaşır (ikisi de soru alır). Birini kapat.

### Migration bundle (üretim için şema güncelleme aracı)
```powershell
dotnet ef migrations bundle -p src/Asistan.Infrastructure -s src/Asistan.Api --force -o efbundle.exe
.\efbundle.exe --connection "Host=...;Database=...;Username=...;Password=..."   # idempotent: güncelse "No migrations were applied"
```
Uygulamanın başka sırlarına (Mongo, RabbitMQ...) ihtiyaç duymaz (`AppDbContextFabrikasi`).

### MCP sunucusunu denemek (Claude Code dışında)
`.mcp.json` Claude Code içinde kullanılır; `/mcp` komutuyla sunucuların durumu görülür. `.env`'deki `MDB_MCP_CONNECTION_STRING` değerini bir **ortam değişkenine** koyman gerekir (Claude Code `.env` okumaz). `dersler/10-claude-cli-skills-mcp.md` bölüm 5.

### LangFlow (doküman asistanı)
```powershell
docker compose up -d langflow                      # başlat (ilk açılış ~1 dk, imaj 3.6 GB)
docker compose logs langflow --tail 50             # günlük
# Arayüz: http://localhost:7860  (kullanıcı/şifre: .env içindeki LANGFLOW_SUPERUSER / LANGFLOW_SUPERUSER_PASSWORD)

# Akışı (yeniden) yükle: LITELLM_KEY değişkenini oluşturur, langflow/dokuman-asistani.json'u yükler
powershell -ExecutionPolicy Bypass -File scripts\langflow-akisi-yukle.ps1
```
Doküman modunda soru (API ve Worker çalışırken):
```powershell
$r = Invoke-RestMethod -Method Post -Uri http://localhost:5254/api/sohbet -ContentType 'application/json' `
  -Body '{"kullaniciId":"1","soru":"Worker kapalıyken sorulara ne olur?","mod":"dokuman"}'
do { Start-Sleep 2; $i = Invoke-RestMethod "http://localhost:5254/api/isler/$($r.isId)" } until ($i.durum -in "Tamamlandi","Hata")
$i.cevap
```
> Compose ayarı (ortam değişkeni) değişince `docker compose up -d langflow` kullan; `restart` yetmez. `docker compose down -v` LangFlow'daki akışları da siler (akışın kopyası Git'te).

### Hafıza ayarlarını geçici değiştirmek (özetlemeyi hızlı görmek için)
Normalde özetleme 20 mesajdan sonra başlar. Denemek için eşiği ortam değişkeniyle küçültebilirsin (sadece o terminal oturumunda geçerli, dosyaya yazılmaz):
```powershell
$env:Hafiza__PencereBoyutu = "2"
$env:Hafiza__OzetEsigi = "4"
dotnet run --project src/Asistan.Api --launch-profile http
```
`__` (çift alt çizgi) = `appsettings.json`'daki `Hafiza:PencereBoyutu`. Bitince aynı terminalde `Remove-Item Env:Hafiza__PencereBoyutu, Env:Hafiza__OzetEsigi` yap. Her soruda ekstra ucuz model çağrıları olduğu için deneme birkaç kuruş tutar.

### LiteLLM'e doğrudan istek (API'yi atlayarak, sorun ayıklamak için)
```powershell
$h = @{ Authorization = "Bearer <LITELLM_MASTER_KEY>" }
$b = '{"model":"akilli","messages":[{"role":"user","content":"Merhaba"}]}'
Invoke-RestMethod -Method Post -Uri http://localhost:4000/v1/chat/completions -Headers $h -ContentType 'application/json' -Body $b
```
Anahtarı komutun içine yazmak yerine kendi terminalinde ortam değişkeninden okuyabilirsin.

---

## Git

| Komut | Ne yapar |
|---|---|
| `git status` | Neyin değiştiğini gösterir |
| `git diff` | Değişikliklerin satır satır farkı |
| `git add .` / `git add <dosya>` | Commit'e hazırlar |
| `git commit -m "feat: aciklama"` | Kalıcı kayıt oluşturur |
| `git log --oneline` | Kısa geçmiş |
| `git log --oneline --graph` | Dallı geçmiş |
| `git push` | GitHub'a gönderir |
| `git pull` | GitHub'dan çeker |
| `git switch -c asama-7-mongo` | Yeni branch açar ve geçer |
| `git switch main` | main'e döner |
| `git restore <dosya>` | Commit edilmemiş değişikliği geri alır |
| `git restore --staged <dosya>` | `git add`'i geri alır |
| `git revert <commit>` | Bir commit'i geri alan yeni commit üretir |
| `git stash` / `git stash pop` | Yarım işi kenara koyar / geri getirir |
| `git remote -v` | Bağlı uzak repo(lar) |

**Kullanma (tehlikeli):** `git reset --hard`, `git push --force`, `git checkout -- .`: yaptığın işi geri dönülmez silebilir. Emin değilsen sor.

**Commit öncesi kontrol (sır sızmasın diye):**
```powershell
git status          # .env listede OLMAMALI
git diff --staged   # eklenen satırlarda şifre/anahtar var mı?
```

---

## Kısayol: "Her şey çalışıyor mu?" kontrol listesi

```powershell
docker compose ps                                  # 4 servis Up (3'ü healthy)
dotnet build                                       # 0 hata, 0 uyarı
dotnet test                                        # 74 test geçti
Invoke-RestMethod http://localhost:5254/openapi/v1.json   # API çalışıyorsa JSON döner
```

---

## Visual Studio kısayolları
- **Test Explorer:** Test menüsü → Test Explorer; testleri tek tıkla çalıştırır.
- **API'yi başlat:** Başlangıç projesi `Asistan.Api`, profil `http`, F5.
- **`.http` dosyası:** `Asistan.Api.http`'yi aç, isteğin üstündeki "Send request" bağlantısına tıkla.
- **Bir sınıfın nerede kullanıldığını bul:** Sağ tık → *Find All References* (Shift+F12).
- **Arayüzün gerçek hali nerede:** Sağ tık → *Go to Implementation* (Ctrl+F12).
