# AI Asistan API

Öğrenme projesi: .NET ile yazılmış, Claude'a LiteLLM üzerinden soru soran bir API.

> **Öğrenme rehberi:** her dosyanın ne işe yaradığı, kavramlar, komutlar ve sorun giderme için [docs/00-INDEKS.md](docs/00-INDEKS.md) dosyasından başla.

## Şu an ne var? (Aşama 11/11)

| Aşama | Konu | Durum |
|---|---|---|
| 1 | Solution iskeleti | ✅ |
| 2 | Docker: Postgres, MongoDB, RabbitMQ | ✅ |
| 3 | EF Core + `/api/kullanicilar` | ✅ |
| 4 | Unit testler | ✅ (74 test) |
| 5 | CI (GitHub Actions) | ✅ dosya hazır, Actions sekmesinden doğrulanacak |
| 6 | LiteLLM + `/api/sohbet` | ✅ Claude cevap veriyor |
| 7 | MongoDB sohbet geçmişi | ✅ soru-cevap kaydediliyor |
| 8 | AI Memory (özetleme + kalıcı hafıza) | ✅ asistan konuşmayı ve kullanıcıyı hatırlıyor |
| 9 | RabbitMQ + Worker (API anında 202, cevabı Worker üretir) | ✅ |
| 10 | LangFlow doküman asistanı (`"mod": "dokuman"`) | ✅ |
| 11 | Docker imajları, CD, skill, MCP | ✅ |

## Klasörler

- `src/Asistan.Domain`: veri sınıfları (Kullanici, Abonelik)
- `src/Asistan.Application`: iş kuralları ve arayüzler
- `src/Asistan.Infrastructure`: veritabanı ve LiteLLM ile gerçek konuşma
- `src/Asistan.Api`: dışarıya açılan web API
- `src/Asistan.Worker`: arka plan işçisi: kuyruktaki soruları alıp Claude'a sorar, özetleme ve hafıza işini yapar
- `tests/Asistan.Tests`: otomatik testler
- `litellm/config.yaml`: hangi AI modelinin kullanılacağı

## Çalıştırma

```powershell
Copy-Item .env.example .env      # sonra .env içindeki değerleri doldur
docker compose up -d             # altyapıyı başlat
powershell -ExecutionPolicy Bypass -File scripts\langflow-akisi-yukle.ps1   # (bir kez) LangFlow akışını yükle
dotnet test                      # testleri çalıştır
dotnet run --project src/Asistan.Api --launch-profile http      # 1. terminal: API
dotnet run --project src/Asistan.Worker                          # 2. terminal: Worker (soruları o işler!)
# VEYA ikisini birden container olarak (API: localhost:5255):
docker compose --profile uygulama up -d --build
```

> **Worker çalışmıyorsa** sorular kaybolmaz, kuyrukta bekler ve işler `Bekliyor` kalır; Worker açılınca işlenir. Visual Studio'da ikisini birden başlatmak için: Solution'a sağ tık → *Configure Startup Projects* → *Multiple startup projects* → `Asistan.Api` ve `Asistan.Worker` için *Start*.

API açılınca: http://localhost:5254/scalar/v1 (Visual Studio'da `http` veya `https` profiliyle F5 bunu kendiliğinden açar)

> **Önemli:** API'nin bağlantı bilgileri (`Mongo:Url`, `ConnectionStrings:Sql`, `LiteLlm:Key`) Git'te yok; `user-secrets` kasasından okunur ve kasa **sadece `Development` ortamında** yüklenir. Visual Studio'da `http`/`https` profilini seç; komut satırında `--no-launch-profile` kullanma. Eksik ayar varsa uygulama açılırken neyin eksik olduğunu yazan bir hata verir (bkz. `docs/05-sorun-giderme.md` #13 ve #14). Kasa okunamazsa yedek yol: `src/Asistan.Api/appsettings.Local.json` (Git'e girmez, gizli değer içerir).

Örnek istekler: `src/Asistan.Api/Asistan.Api.http`

## Sırlar nerede?

- Docker şifreleri ve API anahtarı: `.env` (Git'e girmez)
- API'nin bağlantı bilgileri: `dotnet user-secrets` (makineye özel)


## Atölye (yaparak öğren)
10 seviyeli, otomatik doğrulamalı alıştırma: `./atolye/atolye.ps1` (ayrıntı: `atolye/README.md`).
