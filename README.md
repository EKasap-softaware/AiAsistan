# AI Asistan API

Öğrenme projesi: .NET ile yazılmış, Claude'a LiteLLM üzerinden soru soran bir API.

> **Öğrenme rehberi:** her dosyanın ne işe yaradığı, kavramlar, komutlar ve sorun giderme için [docs/00-INDEKS.md](docs/00-INDEKS.md) dosyasından başla.

## Şu an ne var? (Aşama 7/11)

| Aşama | Konu | Durum |
|---|---|---|
| 1 | Solution iskeleti | ✅ |
| 2 | Docker: Postgres, MongoDB, RabbitMQ | ✅ |
| 3 | EF Core + `/api/kullanicilar` | ✅ |
| 4 | Unit testler | ✅ (18 test) |
| 5 | CI (GitHub Actions) | ✅ dosya hazır, Actions sekmesinden doğrulanacak |
| 6 | LiteLLM + `/api/sohbet` | ✅ Claude cevap veriyor |
| 7 | MongoDB sohbet geçmişi | ✅ soru-cevap kaydediliyor |
| 8-11 | Hafıza, kuyruk, LangFlow, CD | ❌ |

## Klasörler

- `src/Asistan.Domain`: veri sınıfları (Kullanici, Abonelik)
- `src/Asistan.Application`: iş kuralları ve arayüzler
- `src/Asistan.Infrastructure`: veritabanı ve LiteLLM ile gerçek konuşma
- `src/Asistan.Api`: dışarıya açılan web API
- `src/Asistan.Worker`: arka plan işçisi (şimdilik boş, Aşama 9'da dolacak)
- `tests/Asistan.Tests`: otomatik testler
- `litellm/config.yaml`: hangi AI modelinin kullanılacağı

## Çalıştırma

```powershell
Copy-Item .env.example .env      # sonra .env içindeki değerleri doldur
docker compose up -d             # altyapıyı başlat
dotnet test                      # testleri çalıştır
dotnet run --project src/Asistan.Api --launch-profile http
```

API açılınca: http://localhost:5254/scalar/v1

Örnek istekler: `src/Asistan.Api/Asistan.Api.http`

## Sırlar nerede?

- Docker şifreleri ve API anahtarı: `.env` (Git'e girmez)
- API'nin bağlantı bilgileri: `dotnet user-secrets` (makineye özel)
