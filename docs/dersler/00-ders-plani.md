# Ders planı: 10 teknoloji, projeden ayrıntılı anlatım

Her ders, **projedeki gerçek dosyalar ve gerçek çıktılar** üzerinden anlatılır: kavram, projede nerede, nasıl çalıştığı, yaşadığımız hatalar ve çözümleri. Ödev yok: okuyup anlamak yeter, merak edersen deneyebilirsin.

> Bu klasör (`docs/dersler/`) LangFlow doküman asistanı tarafından **okunmaz** (`/projedocs` klasöründeki üst düzey `.md` dosyaları okunur), yani dersler soru başına maliyeti artırmaz.

| # | Ders | Durum | Projede en çok bakılacak yerler |
|---|---|---|---|
| 1 | **LangFlow** | Yazıldı: [01-langflow.md](01-langflow.md) | `langflow/`, `scripts/`, `LangFlowIstemcisi.cs` |
| 2 | LiteLLM | Sırada | `litellm/config.yaml`, `LiteLlmIstemcisi.cs` |
| 3 | MongoDB | Sırada | `Infrastructure/Mongo/`, `MongoAyarlari.cs` |
| 4 | RabbitMQ | Sırada | `Application/Kuyruk/`, `Worker/` |
| 5 | **Docker Container** | Yazıldı: [05-docker.md](05-docker.md) | `docker-compose.yml`, Dockerfile'lar, `.dockerignore` |
| 6 | AI Memory (kısa/uzun süreli) | Sırada | `Application/Bellek/HafizaServisi.cs` |
| 7 | .NET + EF Core + Solution + Unit test | Sırada | `src/`, `tests/`, `Directory.Build.props` |
| 8 | Git / GitHub | Sırada | `.git`, commit geçmişi, `.gitignore` |
| 9 | **CI/CD** | Yazıldı: [09-ci-cd.md](09-ci-cd.md) | `.github/workflows/`, `AppDbContextFabrikasi.cs` |
| 10 | **Claude CLI / Skills / MCP / `.md`** | Yazıldı: [10-claude-cli-skills-mcp.md](10-claude-cli-skills-mcp.md) | `CLAUDE.md`, `.claude/skills/`, `.mcp.json` |

Genel referans için ana rehber: [../00-INDEKS.md](../00-INDEKS.md).
