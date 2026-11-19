# AI Asistan API

.NET 9 ile kurulan, kuyruk tabanlı bir AI asistan. Öğrenme projesi: rehberdeki 11 aşamayla ilerliyoruz.

## Mimari (Clean Architecture)
- `src/Asistan.Domain`: Entity ve iş kuralları, hiçbir şeye bağımlı değil
- `src/Asistan.Application`: Arayüzler (ISohbetServisi, IMesajDeposu, ILlmIstemcisi) ve servisler; sadece Domain'e bağlı
- `src/Asistan.Infrastructure`: EF Core, MongoDB, LiteLLM istemcisi
- `src/Asistan.Api`: Controller'lar, DI kaydı
- `src/Asistan.Worker`: RabbitMQ consumer
- `tests/Asistan.Tests`: xUnit testleri

## Komutlar
- Derle: `dotnet build`
- Test: `dotnet test`
- Altyapı (Aşama 2'den sonra): `docker compose up -d`

## Kurallar
- Her yeni servis için unit test yaz (xUnit + Moq + FluentAssertions).
- Application katmanı Infrastructure'a referans veremez.
- Commit mesajları Conventional Commits formatında.
- API anahtarları sadece .env'de; asla koda yazma.
