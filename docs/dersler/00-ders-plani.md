# Ders planı: 10 teknoloji, projeden ayrıntılı anlatım

Her ders, **projedeki gerçek dosyalar ve gerçek çıktılar** üzerinden anlatılır: kavram, projede nerede, nasıl çalıştığı, **ölçülen sayılar**, yaşadığımız hatalar ve çözümleri, sonda "bilmen gerekenler". Ödev yok: okuyup anlamak yeter, merak edersen deneyebilirsin.

> Bu klasör (`docs/dersler/`) LangFlow doküman asistanı tarafından **okunmaz** (`/projedocs` klasöründeki üst düzey `.md` dosyaları okunur), yani dersler soru başına maliyeti artırmaz.

| # | Ders | Dosya | Projede en çok bakılacak yerler |
|---|---|---|---|
| 1 | **LangFlow** | [01-langflow.md](01-langflow.md) | `langflow/`, `scripts/`, `LangFlowIstemcisi.cs` |
| 2 | **LiteLLM** | [02-litellm.md](02-litellm.md) | `litellm/config.yaml`, `LiteLlmIstemcisi.cs` |
| 3 | **MongoDB** | [03-mongodb.md](03-mongodb.md) | `Infrastructure/Mongo/`, `MongoAyarlari.cs` |
| 4 | **RabbitMQ** | [04-rabbitmq.md](04-rabbitmq.md) | `Application/Kuyruk/`, `Worker/` |
| 5 | **Docker Container** | [05-docker.md](05-docker.md) | `docker-compose.yml`, Dockerfile'lar, `.dockerignore` |
| 6 | **AI Memory** (kısa/uzun süreli) | [06-ai-memory.md](06-ai-memory.md) | `Application/Bellek/HafizaServisi.cs` |
| 7 | **.NET + EF Core + Solution + Unit test** | [07-dotnet-ef-solution-test.md](07-dotnet-ef-solution-test.md) | `src/`, `tests/`, `Directory.Build.props`, `Persistence/` |
| 8 | **Git / GitHub** | [08-git-github.md](08-git-github.md) | `.git`, commit geçmişi, `.gitignore` |
| 9 | **CI/CD** | [09-ci-cd.md](09-ci-cd.md) | `.github/workflows/`, `AppDbContextFabrikasi.cs` |
| 10 | **Claude CLI / Skills / MCP / `.md`** | [10-claude-cli-skills-mcp.md](10-claude-cli-skills-mcp.md) | `CLAUDE.md`, `.claude/skills/`, `.mcp.json` |

**Önerilen okuma sırası (sıfırdan başlıyorsan):** 7 (.NET/EF/Test: temel) → 8 (Git) → 5 (Docker) → 3 (MongoDB) → 4 (RabbitMQ) → 2 (LiteLLM) → 6 (AI Memory) → 1 (LangFlow) → 9 (CI/CD) → 10 (Claude CLI). Ders numarası, rehberdeki konu sırasıdır; öğrenme sırası bu liste.

Genel referans için ana rehber: [../00-INDEKS.md](../00-INDEKS.md).
