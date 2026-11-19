# AI Asistan API

.NET 9, Clean Architecture öğrenme projesi. **Ayrı proje:** KSP2 klasöründeki ksp1/CariCep ile kod bağı yok. Ayrıntı: `docs/00-INDEKS.md` (dersler: `docs/dersler/`).

## Mimari
- `src/Asistan.Domain`: veri sınıfları (EF/MongoDB'yi bilmez)
- `src/Asistan.Application`: iş kuralları + arayüzler; sadece Domain'e bağlı
- `src/Asistan.Infrastructure`: PostgreSQL (EF Core), MongoDB, RabbitMQ (MassTransit 8.x), LiteLLM, LangFlow
- `src/Asistan.Api`: kapı. Soruyu kuyruğa bırakır, **202** döner; cevap `GET /api/isler/{isId}`
- `src/Asistan.Worker`: kuyruktaki soruyu işler (sohbet veya `mod: dokuman` → LangFlow), özetleme/hafıza çıkarma
- `tests/Asistan.Tests`: xUnit + Moq + FluentAssertions

## Komutlar
- Derle/test: `dotnet build` (uyarı = hata), `dotnet test`
- Altyapı: `docker compose up -d`. Uygulama container'da: `docker compose --profile uygulama up -d --build`
- Yerelde çalıştır: API `dotnet run --project src/Asistan.Api --launch-profile http` **ve** Worker `dotnet run --project src/Asistan.Worker`. Worker yoksa sorular `Bekliyor` kalır.
- Migration: `dotnet ef migrations add <Ad> -p src/Asistan.Infrastructure -s src/Asistan.Api -o Persistence/Migrations` (araç: `dotnet tool restore`)

## Kurallar
- Application, Infrastructure'a referans veremez. Her yeni servis için unit test yaz.
- Dış sisteme giden her çağrının zaman sınırı olsun. Hata YUTMA (Worker yeniden deneyebilsin).
- Commit mesajları Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `ci:`).
- Yeni entity için `yeni-entity` skill'ini kullan.

## Gizli bilgiler
- Git'e girmez: `.env`, `appsettings.Local.json`, user-secrets. Koda/dokümana/commit'e yazma; commit öncesi `.env` değerlerinin repo dosyalarında geçmediğini tara.
- Yeni gizli ayar eklenince: `.env.example`, user-secrets ve iki `appsettings.Local.json` (Api, Worker) güncellenir.

## MCP
`.mcp.json`: `mongodb` (SALT-OKUNUR kullanıcı + `--readOnly`) ve `github` (token ortam değişkeninden). Üretim verisine bağlama.

## Atölye
`atolye/` kişisel öğrenme laboratuvarı (10 seviye). `atolye/kum/`, `atolye/.ilerleme.json` git'e girmez. Doğrulayıcılar: `atolye/labs/`.
