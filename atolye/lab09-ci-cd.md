# Lab 09: CI/CD (GitHub Actions)

**Hedef:** Sıfırdan bir CI iş akışı (workflow) yaz: her push/PR'da derle ve test et.
**Dosya:** `.github/workflows/atolye-ci.yml`  (gerçek örnek: aynı klasördeki `ci.yml`: kopyalama, anlayarak yaz)

Zihinsel model: **CI** (sürekli entegrasyon) = her değişiklikte otomatik derle + test et, kırık kod ana dala girmesin.
**CD** (sürekli teslim) = testten geçen sürümü otomatik paketle/yayınla. Workflow = YAML: **olay (on) → job → step'ler**. Her job temiz bir sanal makinede çalışır.

## Görevler (iskelet ve örnek)

> Örnek iskelet (başka bir proje için; sen .NET 9 için yazacaksın):
> ```yaml
> name: Node CI
> on:
>   push:
>     branches: [main]
>   pull_request:
> permissions:
>   contents: read
> concurrency:
>   group: ${{ github.workflow }}-${{ github.ref }}
>   cancel-in-progress: true
> jobs:
>   test:
>     runs-on: ubuntu-latest
>     timeout-minutes: 15
>     steps:
>       - uses: actions/checkout@v4
>       - uses: actions/setup-node@v4
>         with: { node-version: 22, cache: npm }
>       - run: npm ci
>       - run: npm test
> ```

**G1-G3.** Dosyayı aç. `on:` içinde `push` (main) ve `pull_request`; `jobs.test.runs-on: ubuntu-latest`.
**G4.** `actions/checkout@v4`, `actions/setup-dotnet@v4` (`dotnet-version: 9.0.x`).
**G5.** `dotnet build` ve `dotnet test` adımları (`--no-restore`, `--no-build`, `-c Release` ile hızlandır).
**G6.** NuGet önbelleği: `setup-dotnet` içinde `cache: true` + `cache-dependency-path: '**/*.csproj'` (bağımlılıklar her seferinde inmesin).
**G7.** `permissions: contents: read`: iş akışının token'ı sadece okuma yetkisi alsın (en az yetki ilkesi; saldırı yüzeyi küçülür).
**G8.** `timeout-minutes` (takılan iş faturayı şişirmesin) ve `concurrency` (aynı dalda yeni push eskisini iptal etsin).
**G9.** Parola/anahtar düz yazılmaz; `${{ secrets.ADI }}` (repo Settings > Secrets).
**G10 (isteğe bağlı).** `actionlint` kuruluysa sözdizimi denetimi çalışır: `choco install actionlint`.

> Bu lab YAML'ın doğruluğunu denetler. Gerçek çalıştırma için GitHub'a push + Actions sekmesi gerekir (burada yok).

## Sık hatalar
- Girinti (boşluk) hatası: YAML'da TAB yok, hiza her şey.
- `${{ }}` ifadesi tırnaksız `:` içeriyorsa tırnakla.
- Test çalışmıyor: `dotnet test` çözüm klasöründen çalışmalı (`working-directory`).
- Fork'tan gelen PR'da secret'lar boştur.

## Mülakat soruları
1. CI ile CD farkı? Continuous delivery ile deployment farkı?
2. Neden `permissions` kısıtlanır? `GITHUB_TOKEN` ne?
3. Cache nasıl çalışır, anahtarı (key) neye bağlarsın?
4. Matrix build nedir (aynı job'ı farklı sürüm/OS ile)? (proje: `cd.yml`'de api/worker matrix)
5. Pipeline'da imaj etiketi neden `latest` yerine commit SHA?
6. Ortam (environment) onayı ve `workflow_run` ne işe yarar?
